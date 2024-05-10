using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public delegate void ClientAuthenticatorProviderDelegate(IClientSocket connection, SuccessCallback callback);
    public delegate void ClientValidattorProviderDelegate(ProvideServerAccessCheckPacket accessCheckOptions, SuccessCallback callback);

    /// <summary>
    /// Helper class, which implements means to encrypt and decrypt data
    /// </summary>
    public class MstSecurity : MstBaseClient
    {
        public delegate void PermissionLevelCallback(int? permissionLevel, string error);

        private class EncryptionData
        {
            public string ClientAesKey { get; set; }
            public RSACryptoServiceProvider ClientsCsp { get; set; }
            public RSAParameters ClientsPublicKey { get; set; }
        }

        /// <summary>
        /// 
        /// </summary>
        private ClientAuthenticatorProviderDelegate clientAuthenticatorProvider;

        /// <summary>
        /// Salt string
        /// </summary>
        private readonly byte[] _salt = Encoding.ASCII.GetBytes("o6806642kbM7c5");

        /// <summary>
        /// List of encripted data
        /// </summary>
        private readonly ConcurrentDictionary<IClientSocket, EncryptionData> _encryptionData = new ConcurrentDictionary<IClientSocket, EncryptionData>();

        /// <summary>
        /// Size of RSA key
        /// </summary>
        public int RsaKeySize { get; set; } = Mst.Args.AsInt("-mstRsaKeySize", 512);

        #region PASSWORD HASHING

        // The following constants may be changed without breaking existing hashes.
        public const int SALT_BYTE_SIZE = 24;
        public const int HASH_BYTE_SIZE = 24;
        public const int PBKDF2_ITERATIONS = 1000;

        public const int ITERATION_INDEX_IN_HASH = 0;
        public const int SALT_INDEX_IN_HASH = 1;
        public const int PBKDF2_INDEX_IN_HASH = 2;

        #endregion

        /// <summary>
        /// Current permission level
        /// </summary>
        public int CurrentPermissionLevel { get; private set; }

        /// <summary>
        /// Fired when permission level is changed
        /// </summary>
        public event Action OnPermissionsLevelChangedEvent;

        public MstSecurity(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// Default client authenticator method used by client to validate its connections to server
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="callback"></param>
        private void DefaultAuthenticator(IClientSocket connection, SuccessCallback callback)
        {
            string deviceIdHash = CreateHash(SystemInfo.deviceUniqueIdentifier);
            string applicationKeyHash = CreateHash(connection.Password);

            var accessInfo = new ProvideServerAccessCheckPacket()
            {
                DeviceId = deviceIdHash,
                Password = applicationKeyHash
            };

            connection.SendMessage(MstOpCodes.ServerAccessRequest, accessInfo, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, response?.AsString());
                    Logger.Error(response.AsString());
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }

        /// <summary>
        /// Sets new authenticator used in <see cref="AuthenticateConnection(IClientSocket, SuccessCallback)"/> method
        /// </summary>
        /// <param name="clientAuthenticator"></param>
        public void SetClientAuthenticator(ClientAuthenticatorProviderDelegate clientAuthenticator)
        {
            clientAuthenticatorProvider = clientAuthenticator;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="callback"></param>
        public void AuthenticateConnection(IClientSocket connection, SuccessCallback callback)
        {
            if (clientAuthenticatorProvider != null)
                clientAuthenticatorProvider.Invoke(connection, callback);
            else
                DefaultAuthenticator(connection, callback);
        }

        /// <summary>
        /// Requests client permission level
        /// </summary>
        /// <param name="key"></param>
        /// <param name="callback"></param>
        public void RequestPermissionLevel(string key, PermissionLevelCallback callback)
        {
            RequestPermissionLevel(key, callback, Connection);
        }

        /// <summary>
        /// Requests client permission level
        /// </summary>
        /// <param name="key"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void RequestPermissionLevel(string key, PermissionLevelCallback callback, IClientSocket connection)
        {
            connection.SendMessage(MstOpCodes.PermissionLevelRequest, key, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown error"));
                }

                CurrentPermissionLevel = response.AsInt();

                OnPermissionsLevelChangedEvent?.Invoke();

                callback.Invoke(CurrentPermissionLevel, null);
            });
        }

        /// <summary>
        /// Should be called on client. Generates RSA public key, 
        /// sends it to master, which returns encrypted AES key. After decrypting AES key,
        /// callback is invoked with the value. You can then use the AES key to encrypt data
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void GetAesKey(Action<string> callback, IClientSocket connection)
        {
            _encryptionData.TryGetValue(connection, out EncryptionData data);

            if (data == null)
            {
                data = new EncryptionData();
                _encryptionData[connection] = data;
                connection.OnConnectionCloseEvent += OnEncryptableConnectionDisconnected;

                data.ClientsCsp = new RSACryptoServiceProvider(RsaKeySize);

                // Generate keys
                data.ClientsPublicKey = data.ClientsCsp.ExportParameters(false);
            }

            if (data.ClientAesKey != null)
            {
                // We already have an aes generated for this connection
                callback.Invoke(data.ClientAesKey);
                return;
            }

            RSAParametersPacket rsaData = new RSAParametersPacket
            {
                exponent = data.ClientsPublicKey.Exponent,
                modulus = data.ClientsPublicKey.Modulus
            };

            // Send the request
            connection.SendMessage(MstOpCodes.AesKeyRequest, rsaData, (status, response) =>
            {
                if (data.ClientAesKey != null)
                {
                    // Aes is already decrypted.
                    callback.Invoke(data.ClientAesKey);
                    return;
                }

                if (status != ResponseStatus.Success)
                {
                    // Failed to get an aes key
                    callback.Invoke(null);
                    return;
                }

                var decrypted = data.ClientsCsp.Decrypt(response.AsBytes(), false);
                data.ClientAesKey = Encoding.Unicode.GetString(decrypted);

                callback.Invoke(data.ClientAesKey);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnEncryptableConnectionDisconnected(IClientSocket client)
        {
            var disconnected = _encryptionData.Keys.Where(c => !c.IsConnected).ToList();

            foreach (var connection in disconnected)
            {
                // Remove encryption data
                _encryptionData.TryRemove(connection, out _);

                // Unsubscribe from event
                connection.OnConnectionCloseEvent -= OnEncryptableConnectionDisconnected;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="sharedSecret"></param>
        /// <returns></returns>
        public byte[] EncryptAES(byte[] rawData, string sharedSecret)
        {
            using (var aesAlg = new RijndaelManaged())
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                // Create a RijndaelManaged object
                aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);

                // Create a decryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // prepend the IV
                    msEncrypt.Write(BitConverter.GetBytes(aesAlg.IV.Length), 0, sizeof(int));
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, csEncrypt))
                        {
                            //Write all data to the stream.
                            writer.Write(rawData.Length);
                            writer.Write(rawData);
                        }
                    }
                    return msEncrypt.ToArray();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="encryptedData"></param>
        /// <param name="sharedSecret"></param>
        /// <returns></returns>
        public byte[] DecryptAES(byte[] encryptedData, string sharedSecret)
        {
            using (var aesAlg = new RijndaelManaged())
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                using (MemoryStream msDecrypt = new MemoryStream(encryptedData))
                {
                    // Get the key
                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                    // Get the initialization vector from the encrypted stream
                    aesAlg.IV = ReadByteArray(msDecrypt);
                    // Create a decrytor to perform the stream transform.
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var reader = new EndianBinaryReader(EndianBitConverter.Big, csDecrypt))
                        {
                            return reader.ReadBytes(reader.ReadInt32());
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Encrypt the given string using AES.  The string can be decrypted using 
        /// DecryptStringAES().  The sharedSecret parameters must match.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <param name="sharedSecret">A password used to generate a key for encryption.</param>
        public string EncryptStringAES(string plainText, string sharedSecret)
        {
            string outStr = null;                       // Encrypted string to return
            RijndaelManaged aesAlg = null;              // RijndaelManaged object used to encrypt the data.

            try
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                // Create a RijndaelManaged object
                aesAlg = new RijndaelManaged();
                aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);

                // Create a decryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // prepend the IV
                    msEncrypt.Write(BitConverter.GetBytes(aesAlg.IV.Length), 0, sizeof(int));
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);

                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                    }

                    outStr = Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
            finally
            {
                // Clear the RijndaelManaged object.
                if (aesAlg != null)
                {
                    aesAlg.Clear();
                }
            }

            // Return the encrypted bytes from the memory stream.
            return outStr;
        }

        /// <summary>
        /// Decrypt the given string.  Assumes the string was encrypted using 
        /// EncryptStringAES(), using an identical sharedSecret.
        /// </summary>
        /// <param name="cipherText">The text to decrypt.</param>
        /// <param name="sharedSecret">A password used to generate a key for decryption.</param>
        public string DecryptStringAES(string cipherText, string sharedSecret)
        {
            // Declare the RijndaelManaged object
            // used to decrypt the data.
            RijndaelManaged aesAlg = null;

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            try
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                // Create the streams used for decryption.                
                byte[] bytes = Convert.FromBase64String(cipherText);
                using (MemoryStream msDecrypt = new MemoryStream(bytes))
                {
                    // Create a RijndaelManaged object
                    // with the specified key and IV.
                    aesAlg = new RijndaelManaged();
                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                    // Get the initialization vector from the encrypted stream
                    aesAlg.IV = ReadByteArray(msDecrypt);
                    // Create a decrytor to perform the stream transform.
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            finally
            {
                // Clear the RijndaelManaged object.
                if (aesAlg != null)
                {
                    aesAlg.Clear();
                }
            }

            return plaintext;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static byte[] ReadByteArray(Stream s)
        {
            byte[] rawLength = new byte[sizeof(int)];
            if (s.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
            {
                throw new SystemException("Stream did not contain properly formatted byte array");
            }

            byte[] buffer = new byte[BitConverter.ToInt32(rawLength, 0)];
            if (s.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new SystemException("Did not read byte array properly");
            }

            return buffer;
        }

        /// <summary>
        /// Creates a salted PBKDF2 hash of the password.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <returns>The hash of the password.</returns>
        public string CreateHash(string password)
        {
            // Generate a random salt
            RNGCryptoServiceProvider csprng = new RNGCryptoServiceProvider();
            byte[] salt = new byte[SALT_BYTE_SIZE];
            csprng.GetBytes(salt);

            // Hash the password and encode the parameters
            byte[] hash = PBKDF2(password, salt, PBKDF2_ITERATIONS, HASH_BYTE_SIZE);
            return PBKDF2_ITERATIONS + ":" + Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Validates a password given a hash of the correct one.
        /// </summary>
        /// <param name="password">The password to check.</param>
        /// <param name="correctHash">A hash of the correct password.</param>
        /// <returns>True if the password is correct. False otherwise.</returns>
        public bool ValidatePassword(string password, string correctHash)
        {
            // Extract the parameters from the hash
            char[] delimiter = { ':' };
            string[] split = correctHash.Split(delimiter);
            int iterations = int.Parse(split[ITERATION_INDEX_IN_HASH]);
            byte[] salt = Convert.FromBase64String(split[SALT_INDEX_IN_HASH]);
            byte[] hash = Convert.FromBase64String(split[PBKDF2_INDEX_IN_HASH]);

            byte[] testHash = PBKDF2(password, salt, iterations, hash.Length);
            return SlowEquals(hash, testHash);
        }

        /// <summary>
        /// Compares two byte arrays in length-constant time. This comparison
        /// method is used so that password hashes cannot be extracted from
        /// on-line systems using a timing attack and then attacked off-line.
        /// </summary>
        /// <param name="a">The first byte array.</param>
        /// <param name="b">The second byte array.</param>
        /// <returns>True if both byte arrays are equal. False otherwise.</returns>
        private static bool SlowEquals(byte[] a, byte[] b)
        {
            uint diff = (uint)a.Length ^ (uint)b.Length;

            for (var i = 0; (i < a.Length) && (i < b.Length); i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }

            return diff == 0;
        }

        /// <summary>
        /// Computes the PBKDF2-SHA1 hash of a password.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <param name="salt">The salt.</param>
        /// <param name="iterations">The PBKDF2 iteration count.</param>
        /// <param name="outputBytes">The length of the hash to generate, in bytes.</param>
        /// <returns>A hash of the password.</returns>
        private static byte[] PBKDF2(string password, byte[] salt, int iterations, int outputBytes)
        {
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt)
            {
                IterationCount = iterations
            };

            return pbkdf2.GetBytes(outputBytes);
        }

        public string CreateSignatureHMAC_SHA256(string secret, string message)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] hmacValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return Convert.ToBase64String(hmacValue);
            }
        }
    }
}