#if !UNITY_WEBGL || UNITY_EDITOR
using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MasterServerToolkit.MasterServer
{
    internal sealed class MstSecurityKeyRing : IDisposable
    {
        private sealed class KeyRingDocument
        {
            public int version = 1;
            public string activeDataKeyId;
            public string activeSealKeyId;
            public DataKeyDocument[] dataKeys;
            public SealKeyDocument[] sealKeys;
        }

        private sealed class DataKeyDocument
        {
            public string id;
            public string key;
        }

        private sealed class SealKeyDocument
        {
            public string id;
            public string publicKeySpki;
            public string privateKeyPkcs8;
        }

        internal sealed class DataKey
        {
            public string Id { get; }
            public byte[] Key { get; }

            public DataKey(string id, byte[] key)
            {
                Id = id;
                Key = key;
            }
        }

        internal sealed class SealKey
        {
            public string Id { get; }
            public byte[] PublicKeySpki { get; }
            public byte[] PrivateKeyPkcs8 { get; }

            public SealKey(string id, byte[] publicKeySpki, byte[] privateKeyPkcs8)
            {
                Id = id;
                PublicKeySpki = publicKeySpki;
                PrivateKeyPkcs8 = privateKeyPkcs8;
            }
        }

        private readonly Dictionary<string, DataKey> dataKeys;
        private readonly Dictionary<string, SealKey> sealKeys;

        public DataKey ActiveDataKey { get; }
        public SealKey ActiveSealKey { get; }

        private MstSecurityKeyRing(
            Dictionary<string, DataKey> dataKeys,
            Dictionary<string, SealKey> sealKeys,
            string activeDataKeyId,
            string activeSealKeyId)
        {
            this.dataKeys = dataKeys;
            this.sealKeys = sealKeys;
            ActiveDataKey = dataKeys[activeDataKeyId];
            ActiveSealKey = sealKeys[activeSealKeyId];
        }

        public bool TryGetDataKey(string keyId, out DataKey key)
        {
            return dataKeys.TryGetValue(keyId, out key);
        }

        public bool TryGetSealKey(string keyId, out SealKey key)
        {
            return sealKeys.TryGetValue(keyId, out key);
        }

        public void Dispose()
        {
            foreach (DataKey dataKey in dataKeys.Values)
                Array.Clear(dataKey.Key, 0, dataKey.Key.Length);

            foreach (SealKey sealKey in sealKeys.Values)
            {
                Array.Clear(sealKey.PublicKeySpki, 0, sealKey.PublicKeySpki.Length);
                Array.Clear(sealKey.PrivateKeyPkcs8, 0, sealKey.PrivateKeyPkcs8.Length);
            }
        }

        public static bool TryLoadOrCreate(
            string filePath,
            out MstSecurityKeyRing keyRing,
            out string error)
        {
            keyRing = null;
            error = null;

            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    error = "MST security key ring file path is empty";
                    return false;
                }

                string fullPath = Path.GetFullPath(filePath);

                if (!File.Exists(fullPath))
                {
                    KeyRingDocument generated = CreateDocument();
                    WriteDocumentAtomically(fullPath, generated);
                }

                string jsonText = File.ReadAllText(fullPath, Encoding.UTF8);

                if (!MstJson.IsJson(jsonText))
                {
                    error = $"Invalid MST security key ring '{fullPath}': malformed JSON";
                    return false;
                }

                if (!TryReadDocument(
                        new MstJson(jsonText),
                        out KeyRingDocument document,
                        out error) ||
                    !TryCreateFromDocument(document, out keyRing, out error))
                {
                    error = $"Invalid MST security key ring '{fullPath}': {error}";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"Failed to load MST security key ring: {exception.Message}";
                return false;
            }
        }

        private static bool TryReadDocument(
            MstJson json,
            out KeyRingDocument document,
            out string error)
        {
            document = null;
            error = null;

            if (json == null || !json.IsObject)
            {
                error = "root must be a JSON object";
                return false;
            }

            MstJson versionJson = json.GetField("version");

            if (versionJson == null || !versionJson.IsNumber || !versionJson.IsInteger)
            {
                error = "version must be an integer";
                return false;
            }

            if (versionJson.LongValue < int.MinValue || versionJson.LongValue > int.MaxValue)
            {
                error = "version is outside the supported integer range";
                return false;
            }

            if (!TryReadRequiredString(json, "activeDataKeyId", out string activeDataKeyId, out error) ||
                !TryReadRequiredString(json, "activeSealKeyId", out string activeSealKeyId, out error) ||
                !TryReadDataKeyDocuments(json.GetField("dataKeys"), out DataKeyDocument[] dataKeys, out error) ||
                !TryReadSealKeyDocuments(json.GetField("sealKeys"), out SealKeyDocument[] sealKeys, out error))
            {
                return false;
            }

            document = new KeyRingDocument
            {
                version = versionJson.IntValue,
                activeDataKeyId = activeDataKeyId,
                activeSealKeyId = activeSealKeyId,
                dataKeys = dataKeys,
                sealKeys = sealKeys
            };
            return true;
        }

        private static bool TryReadDataKeyDocuments(
            MstJson json,
            out DataKeyDocument[] documents,
            out string error)
        {
            documents = null;
            error = null;

            if (json == null || !json.IsArray)
            {
                error = "dataKeys must be an array";
                return false;
            }

            documents = new DataKeyDocument[json.Count];

            for (int i = 0; i < documents.Length; i++)
            {
                MstJson item = json[i];

                if (item == null || !item.IsObject)
                {
                    error = $"dataKeys[{i}] must be an object";
                    return false;
                }

                if (!TryReadRequiredString(item, "id", out string id, out error) ||
                    !TryReadRequiredString(item, "key", out string key, out error))
                {
                    error = $"dataKeys[{i}] is invalid: {error}";
                    return false;
                }

                documents[i] = new DataKeyDocument
                {
                    id = id,
                    key = key
                };
            }

            return true;
        }

        private static bool TryReadSealKeyDocuments(
            MstJson json,
            out SealKeyDocument[] documents,
            out string error)
        {
            documents = null;
            error = null;

            if (json == null || !json.IsArray)
            {
                error = "sealKeys must be an array";
                return false;
            }

            documents = new SealKeyDocument[json.Count];

            for (int i = 0; i < documents.Length; i++)
            {
                MstJson item = json[i];

                if (item == null || !item.IsObject)
                {
                    error = $"sealKeys[{i}] must be an object";
                    return false;
                }

                if (!TryReadRequiredString(item, "id", out string id, out error) ||
                    !TryReadRequiredString(item, "publicKeySpki", out string publicKey, out error) ||
                    !TryReadRequiredString(item, "privateKeyPkcs8", out string privateKey, out error))
                {
                    error = $"sealKeys[{i}] is invalid: {error}";
                    return false;
                }

                documents[i] = new SealKeyDocument
                {
                    id = id,
                    publicKeySpki = publicKey,
                    privateKeyPkcs8 = privateKey
                };
            }

            return true;
        }

        private static bool TryReadRequiredString(
            MstJson json,
            string fieldName,
            out string value,
            out string error)
        {
            value = null;
            error = null;
            MstJson field = json.GetField(fieldName);

            if (field == null || !field.IsString || string.IsNullOrWhiteSpace(field.StringValue))
            {
                error = $"{fieldName} must be a non-empty string";
                return false;
            }

            value = field.StringValue;
            return true;
        }

        private static bool TryCreateFromDocument(
            KeyRingDocument document,
            out MstSecurityKeyRing keyRing,
            out string error)
        {
            keyRing = null;
            error = null;

            if (document == null || document.version != 1)
            {
                error = "unsupported or missing key ring version";
                return false;
            }

            var dataKeys = new Dictionary<string, DataKey>(StringComparer.Ordinal);
            var sealKeys = new Dictionary<string, SealKey>(StringComparer.Ordinal);

            if (document.dataKeys == null || document.dataKeys.Length == 0)
            {
                error = "at least one data key is required";
                return false;
            }

            foreach (DataKeyDocument dataKeyDocument in document.dataKeys)
            {
                if (!TryReadDataKey(dataKeyDocument, out DataKey dataKey, out error))
                    return false;

                if (!dataKeys.TryAdd(dataKey.Id, dataKey))
                {
                    error = $"duplicate data key id '{dataKey.Id}'";
                    return false;
                }
            }

            if (document.sealKeys == null || document.sealKeys.Length == 0)
            {
                error = "at least one seal key is required";
                return false;
            }

            foreach (SealKeyDocument sealKeyDocument in document.sealKeys)
            {
                if (!TryReadSealKey(sealKeyDocument, out SealKey sealKey, out error))
                    return false;

                if (!sealKeys.TryAdd(sealKey.Id, sealKey))
                {
                    error = $"duplicate seal key id '{sealKey.Id}'";
                    return false;
                }
            }

            if (!dataKeys.ContainsKey(document.activeDataKeyId))
            {
                error = $"active data key '{document.activeDataKeyId}' was not found";
                return false;
            }

            if (!sealKeys.ContainsKey(document.activeSealKeyId))
            {
                error = $"active seal key '{document.activeSealKeyId}' was not found";
                return false;
            }

            keyRing = new MstSecurityKeyRing(
                dataKeys,
                sealKeys,
                document.activeDataKeyId,
                document.activeSealKeyId);
            return true;
        }

        private static bool TryReadDataKey(
            DataKeyDocument document,
            out DataKey dataKey,
            out string error)
        {
            dataKey = null;
            error = null;

            if (document == null || string.IsNullOrWhiteSpace(document.id))
            {
                error = "data key id is required";
                return false;
            }

            try
            {
                MstSecurityProtocol.ValidateKeyId(document.id);
                byte[] key = Convert.FromBase64String(document.key ?? string.Empty);

                if (key.Length != MstSecurityProtocol.DataKeySize)
                {
                    error = $"data key '{document.id}' must contain 32 bytes";
                    return false;
                }

                dataKey = new DataKey(document.id, key);
                return true;
            }
            catch (Exception exception)
            {
                error = $"data key '{document.id}' is invalid: {exception.Message}";
                return false;
            }
        }

        private static bool TryReadSealKey(
            SealKeyDocument document,
            out SealKey sealKey,
            out string error)
        {
            sealKey = null;
            error = null;

            if (document == null || string.IsNullOrWhiteSpace(document.id))
            {
                error = "seal key id is required";
                return false;
            }

            try
            {
                MstSecurityProtocol.ValidateKeyId(document.id);
                byte[] publicKey = Convert.FromBase64String(document.publicKeySpki ?? string.Empty);
                byte[] privateKey = Convert.FromBase64String(document.privateKeyPkcs8 ?? string.Empty);
                byte[] probe = MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.DataKeySize);
                byte[] wrapped = MstManagedCryptoBackend.WrapKey(publicKey, probe);
                bool pairMatches = MstManagedCryptoBackend.TryUnwrapKey(
                    privateKey,
                    wrapped,
                    out byte[] unwrapped) &&
                    MstSecurity.FixedTimeEquals(probe, unwrapped);

                Array.Clear(probe, 0, probe.Length);

                if (unwrapped != null)
                    Array.Clear(unwrapped, 0, unwrapped.Length);

                if (!pairMatches)
                {
                    error = $"seal key '{document.id}' public/private key pair does not match";
                    return false;
                }

                sealKey = new SealKey(document.id, publicKey, privateKey);
                return true;
            }
            catch (Exception exception)
            {
                error = $"seal key '{document.id}' is invalid: {exception.Message}";
                return false;
            }
        }

        private static KeyRingDocument CreateDocument()
        {
            byte[] dataKey = MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.DataKeySize);
            MstManagedCryptoBackend.GenerateRsaKeyPair(
                out byte[] publicKey,
                out byte[] privateKey);
            string dataKeyId = MstManagedCryptoBackend.CreateKeyId(dataKey, "data");
            string sealKeyId = MstManagedCryptoBackend.CreateKeyId(publicKey, "seal");

            return new KeyRingDocument
            {
                version = 1,
                activeDataKeyId = dataKeyId,
                activeSealKeyId = sealKeyId,
                dataKeys = new[]
                {
                    new DataKeyDocument
                    {
                        id = dataKeyId,
                        key = Convert.ToBase64String(dataKey)
                    }
                },
                sealKeys = new[]
                {
                    new SealKeyDocument
                    {
                        id = sealKeyId,
                        publicKeySpki = Convert.ToBase64String(publicKey),
                        privateKeyPkcs8 = Convert.ToBase64String(privateKey)
                    }
                }
            };
        }

        private static void WriteDocumentAtomically(string fullPath, KeyRingDocument document)
        {
            string directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    CreateJsonDocument(document).ToString(true),
                    new UTF8Encoding(false));

                try
                {
                    File.Move(temporaryPath, fullPath);
                }
                catch (IOException) when (File.Exists(fullPath))
                {
                    // Another process created the shared key ring first.
                    // The caller validates and loads that complete file below.
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static MstJson CreateJsonDocument(KeyRingDocument document)
        {
            MstJson json = MstJson.CreateObject();
            json.AddField("version", document.version);
            json.AddField("activeDataKeyId", document.activeDataKeyId);
            json.AddField("activeSealKeyId", document.activeSealKeyId);

            MstJson dataKeys = MstJson.CreateArray();

            foreach (DataKeyDocument dataKey in document.dataKeys)
            {
                MstJson item = MstJson.CreateObject();
                item.AddField("id", dataKey.id);
                item.AddField("key", dataKey.key);
                dataKeys.Add(item);
            }

            json.AddField("dataKeys", dataKeys);
            MstJson sealKeys = MstJson.CreateArray();

            foreach (SealKeyDocument sealKey in document.sealKeys)
            {
                MstJson item = MstJson.CreateObject();
                item.AddField("id", sealKey.id);
                item.AddField("publicKeySpki", sealKey.publicKeySpki);
                item.AddField("privateKeyPkcs8", sealKey.privateKeyPkcs8);
                sealKeys.Add(item);
            }

            json.AddField("sealKeys", sealKeys);
            return json;
        }
    }
}
#endif
