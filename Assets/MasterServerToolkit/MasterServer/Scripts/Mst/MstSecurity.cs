using MasterServerToolkit.Logging;
using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Helper class, which implements means to encrypt and decrypt data
    /// </summary>
    public class MstSecurity : MstBaseClient, IDisposable
    {
        private sealed class PermissionConnectionState
        {
            public object SyncRoot { get; } = new object();
            public Dictionary<string, int> GrantedPermissions { get; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, List<SuccessCallback>> PendingRequests { get; } =
                new Dictionary<string, List<SuccessCallback>>(StringComparer.Ordinal);
            public ConnectionStatusDelegate StatusChangedHandler { get; set; }
            public bool IsLifecycleSubscribed { get; set; }
        }

        private readonly ConcurrentDictionary<IClientSocket, PermissionConnectionState> permissionStates = new();
        private readonly object keyRingSync = new object();
#if !UNITY_WEBGL || UNITY_EDITOR
        private MstSecurityKeyRing serverKeyRing;
#endif

        #region PASSWORD HASHING

        private const string CurrentPasswordHashVersion = "v2";
        private const int MaximumAcceptedPbkdf2Iterations = 1000000;

        // New hashes use PBKDF2-HMAC-SHA256. Legacy three-part PBKDF2-SHA1
        // hashes remain readable and are upgraded after a successful sign-in.
        public const int SALT_BYTE_SIZE = 24;
        public const int HASH_BYTE_SIZE = 32;
        public const int PBKDF2_ITERATIONS = 210000;

        public const int ITERATION_INDEX_IN_HASH = 0;
        public const int SALT_INDEX_IN_HASH = 1;
        public const int PBKDF2_INDEX_IN_HASH = 2;

        #endregion

        /// <summary>
        /// Current permission level
        /// </summary>
        public int CurrentPermissionLevel => GetPermissionLevel(Connection);

        /// <summary>
        /// Fired when permission level is changed
        /// </summary>
        public event Action OnPermissionsLevelChangedEvent;

        public MstSecurity(IClientSocket connection)
            : base(connection)
        {
            Mst.Errors.TryRegister(MstErrorCodes.PERMISSION_DENIED);
            Mst.Errors.TryRegister(MstErrorCodes.REQUEST_HANDLER_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.REQUEST_HANDLER_FAILED);
            Mst.Errors.TryRegister(MstErrorCodes.CONNECTION_UNAUTHENTICATED);
            Mst.Errors.TryRegister(MstErrorCodes.SECURITY_CONTEXT_MISSING);
            Mst.Errors.TryRegister(MstErrorCodes.PERMISSION_REQUEST_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.DEFAULT_PERMISSION_REQUIRED);
            Mst.Errors.TryRegister(MstErrorCodes.ENCRYPTION_CHALLENGE_REQUEST_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.ENCRYPTION_CHALLENGE_UNAVAILABLE);
            Mst.Errors.TryRegister(MstErrorCodes.PERMISSION_KEY_REQUIRED);
            Mst.Errors.TryRegister(MstErrorCodes.PERMISSION_CHALLENGE_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.PERMISSION_PROTOCOL_UNSUPPORTED);
            Mst.Errors.TryRegister(MstErrorCodes.PERMISSION_RESPONSE_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.ENCRYPTION_REQUEST_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.ENCRYPTION_CHALLENGE_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.ENCRYPTION_FAILED);
        }

        /// <summary>
        /// Authenticates a new socket session with the default permission.
        /// </summary>
        public void AuthenticateConnection(IClientSocket connection, SuccessCallback callback)
        {
            RequestPermissionInternal(MstPermissionKeys.Default,
                GetPermissionCredential(connection, MstPermissionKeys.Default),
                callback,
                connection,
                true);
        }

        /// <summary>
        /// Requests a permission for the default connection using the configured credential.
        /// </summary>
        public void RequestPermission(string permissionKey, SuccessCallback callback = null)
        {
            RequestPermission(permissionKey, callback, Connection);
        }

        /// <summary>
        /// Requests a permission for a connection using the configured credential.
        /// </summary>
        public void RequestPermission(string permissionKey, SuccessCallback callback, IClientSocket connection)
        {
            RequestPermission(permissionKey, GetPermissionCredential(connection, permissionKey), callback, connection);
        }

        /// <summary>
        /// Gets the credential configured for a permission key or its built-in default.
        /// </summary>
        /// <param name="permissionKey">Permission key whose credential is required.</param>
        /// <returns>
        /// Configured credential, the built-in credential when the key is omitted, or an empty string
        /// when the provided credential map is invalid.
        /// </returns>
        public string GetPermissionCredential(string permissionKey)
        {
            return GetPermissionCredential(null, permissionKey);
        }

        private string GetPermissionCredential(IClientSocket connection, string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
                return string.Empty;

            var connectionCredentials = connection as IConnectionPermissionCredentials;

            if (connectionCredentials != null &&
                string.Equals(connectionCredentials.PermissionKey, permissionKey, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(connectionCredentials.PermissionCredential))
            {
                return connectionCredentials.PermissionCredential;
            }

            if (Mst.Args.IsProvided(Mst.Args.Names.PermissionCredentials))
            {
                if (!Mst.Args.TryGetJson(Mst.Args.Names.PermissionCredentials, out MstJson credentials) ||
                    !credentials.IsObject ||
                    credentials.Keys == null ||
                    credentials.Values == null)
                {
                    return string.Empty;
                }

                for (int i = 0; i < credentials.Count; i++)
                {
                    if (!string.Equals(credentials.Keys[i], permissionKey, StringComparison.Ordinal))
                        continue;

                    return credentials.Values[i] != null && credentials.Values[i].IsString
                        ? credentials.Values[i].StringValue ?? string.Empty
                        : string.Empty;
                }
            }

            return MstPermissionSecrets.GetDefault(permissionKey);
        }

        /// <summary>
        /// Requests a permission for a connection using an explicit credential.
        /// </summary>
        public void RequestPermission(string permissionKey, string secret, SuccessCallback callback,
            IClientSocket connection)
        {
            RequestPermissionInternal(permissionKey, secret, callback, connection, false);
        }

        /// <summary>
        /// Returns whether the default connection has a permission confirmed by the server.
        /// </summary>
        public bool HasPermission(string permissionKey)
        {
            return HasPermission(Connection, permissionKey);
        }

        /// <summary>
        /// Returns whether a connection has a permission confirmed by the server.
        /// </summary>
        public bool HasPermission(IClientSocket connection, string permissionKey)
        {
            if (connection == null ||
                (!connection.IsConnected && connection.Status != ConnectionStatus.Authenticating) ||
                string.IsNullOrWhiteSpace(permissionKey))
                return false;

            if (!permissionStates.TryGetValue(connection, out PermissionConnectionState state))
                return false;

            lock (state.SyncRoot)
                return state.GrantedPermissions.ContainsKey(permissionKey);
        }

        public int GetPermissionLevel(IClientSocket connection)
        {
            if (connection == null || !permissionStates.TryGetValue(connection, out PermissionConnectionState state))
                return MstPermissionLevels.Default;

            lock (state.SyncRoot)
            {
                int permissionLevel = MstPermissionLevels.Default;

                foreach (int grantedLevel in state.GrantedPermissions.Values)
                    permissionLevel = Math.Max(permissionLevel, grantedLevel);

                return permissionLevel;
            }
        }

        private void RequestPermissionInternal(string permissionKey, string secret, SuccessCallback callback,
            IClientSocket connection, bool allowAuthenticating)
        {
            if (connection == null)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.Invalid,
                    CreateErrorProperties(MstErrorCodes.PERMISSION_KEY_REQUIRED)));
                return;
            }

            if (!connection.IsConnected &&
                !(allowAuthenticating && connection.Status == ConnectionStatus.Authenticating))
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            PermissionConnectionState state = GetOrCreatePermissionState(connection);
            bool startRequest = false;
            bool alreadyGranted = false;

            lock (state.SyncRoot)
            {
                if (state.GrantedPermissions.ContainsKey(permissionKey))
                {
                    alreadyGranted = true;
                }
                else if (state.PendingRequests.TryGetValue(permissionKey,
                             out List<SuccessCallback> pendingCallbacks))
                {
                    if (callback != null)
                        pendingCallbacks.Add(callback);
                }
                else
                {
                    var callbacks = new List<SuccessCallback>();

                    if (callback != null)
                        callbacks.Add(callback);

                    state.PendingRequests.Add(permissionKey, callbacks);
                    startRequest = true;
                }
            }

            if (alreadyGranted)
            {
                callback?.Invoke(true, string.Empty);
                return;
            }

            if (!startRequest)
                return;

            SendPermissionChallengeRequest(connection, state, permissionKey, secret);
        }

        private void SendPermissionChallengeRequest(IClientSocket connection, PermissionConnectionState state,
            string permissionKey, string secret)
        {
            try
            {
                connection.SendMessage(MstOpCodes.ServerAccessChallengeRequest,
                    new ServerAccessChallengeRequestPacket
                    {
                        PermissionKey = permissionKey
                    }, (challengeStatus, challengeResponse) =>
                    {
                        if (challengeStatus != ResponseStatus.Success || challengeResponse == null)
                        {
                            CompletePermissionRequest(connection, state, permissionKey, false,
                                MstPermissionLevels.Default,
                                challengeStatus == ResponseStatus.Success
                                    ? Mst.Errors.Parse(ResponseStatus.Invalid,
                                        CreateErrorProperties(MstErrorCodes.PERMISSION_CHALLENGE_INVALID))
                                    : Mst.Errors.Parse(challengeStatus, challengeResponse));
                            return;
                        }

                        ServerAccessChallengePacket challenge;

                        try
                        {
                            challenge = challengeResponse.AsPacket<ServerAccessChallengePacket>();
                        }
                        catch (Exception exception)
                        {
                            CompletePermissionRequest(connection, state, permissionKey, false,
                                MstPermissionLevels.Default,
                                Mst.Errors.Parse(ResponseStatus.Invalid,
                                    CreateErrorProperties(MstErrorCodes.PERMISSION_CHALLENGE_INVALID)));
                            Logs.Error($"Invalid permission challenge: {exception}", LogChannels.Security);
                            return;
                        }

                        if (challenge == null || challenge.Version != ServerAccessChallengePacket.CurrentVersion)
                        {
                            CompletePermissionRequest(connection, state, permissionKey, false,
                                MstPermissionLevels.Default,
                                challenge == null
                                    ? Mst.Errors.Parse(ResponseStatus.Invalid,
                                        CreateErrorProperties(MstErrorCodes.PERMISSION_CHALLENGE_INVALID))
                                    : Mst.Errors.Parse(ResponseStatus.Invalid,
                                        CreateErrorProperties(MstErrorCodes.PERMISSION_PROTOCOL_UNSUPPORTED)));
                            return;
                        }

                        SendPermissionProof(connection, state, permissionKey, secret, challenge);
                    });
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to request permission challenge: {exception}", LogChannels.Security);
                CompletePermissionRequest(connection, state, permissionKey, false,
                    MstPermissionLevels.Default, Mst.Errors.Parse(
                        connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected));
            }
        }

        private void SendPermissionProof(IClientSocket connection, PermissionConnectionState state,
            string permissionKey, string secret, ServerAccessChallengePacket challenge)
        {
            var accessInfo = new ProvideServerAccessCheckPacket
            {
                ChallengeId = challenge.ChallengeId,
                Proof = CreateAccessProof(
                    secret,
                    connection.Service,
                    permissionKey,
                    challenge.ChallengeId,
                    challenge.Nonce,
                    challenge.ExpiresAtUtcTicks)
            };

            try
            {
                connection.SendMessage(MstOpCodes.ServerAccessRequest, accessInfo, (accessStatus, accessResponse) =>
                {
                    if (accessStatus != ResponseStatus.Success || accessResponse == null)
                    {
                        CompletePermissionRequest(connection, state, permissionKey, false,
                            MstPermissionLevels.Default,
                            accessStatus == ResponseStatus.Success
                                ? Mst.Errors.Parse(ResponseStatus.Invalid,
                                    CreateErrorProperties(MstErrorCodes.PERMISSION_RESPONSE_INVALID))
                                : Mst.Errors.Parse(accessStatus, accessResponse));
                        return;
                    }

                    int permissionLevel;

                    try
                    {
                        permissionLevel = accessResponse.AsInt();
                    }
                    catch (Exception exception)
                    {
                        CompletePermissionRequest(connection, state, permissionKey, false,
                            MstPermissionLevels.Default,
                            Mst.Errors.Parse(ResponseStatus.Invalid,
                                CreateErrorProperties(MstErrorCodes.PERMISSION_RESPONSE_INVALID)));
                        Logs.Error($"Invalid permission response: {exception}", LogChannels.Security);
                        return;
                    }

                    CompletePermissionRequest(connection, state, permissionKey, true,
                        permissionLevel, string.Empty);
                });
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to send permission proof: {exception}", LogChannels.Security);
                CompletePermissionRequest(connection, state, permissionKey, false,
                    MstPermissionLevels.Default, Mst.Errors.Parse(
                        connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected));
            }
        }

        private PermissionConnectionState GetOrCreatePermissionState(IClientSocket connection)
        {
            PermissionConnectionState state = permissionStates.GetOrAdd(connection,
                _ => new PermissionConnectionState());
            bool subscribe;

            lock (state.SyncRoot)
            {
                subscribe = !state.IsLifecycleSubscribed;

                if (subscribe)
                {
                    state.IsLifecycleSubscribed = true;
                    state.StatusChangedHandler = status =>
                        OnPermissionConnectionStatusChanged(connection, status);
                }
            }

            if (subscribe)
            {
                connection.OnConnectionCloseEvent -= OnPermissionConnectionDisconnected;
                connection.OnConnectionCloseEvent += OnPermissionConnectionDisconnected;
                connection.OnStatusChangedEvent += state.StatusChangedHandler;
            }

            return state;
        }

        private void CompletePermissionRequest(IClientSocket connection, PermissionConnectionState state,
            string permissionKey, bool success, int permissionLevel, string error)
        {
            if (!permissionStates.TryGetValue(connection, out PermissionConnectionState currentState) ||
                !ReferenceEquals(currentState, state))
            {
                return;
            }

            List<SuccessCallback> callbacks;
            bool permissionChanged = false;

            lock (state.SyncRoot)
            {
                if (!state.PendingRequests.TryGetValue(permissionKey, out callbacks))
                    return;

                state.PendingRequests.Remove(permissionKey);

                if (success)
                {
                    int clampedLevel = MstPermissionLevels.Clamp(permissionLevel);
                    permissionChanged = !state.GrantedPermissions.TryGetValue(permissionKey,
                                            out int currentLevel) ||
                                        currentLevel != clampedLevel;
                    state.GrantedPermissions[permissionKey] = clampedLevel;
                }
            }

            if (permissionChanged && ReferenceEquals(connection, Connection))
                RaisePermissionsLevelChanged();

            foreach (SuccessCallback pendingCallback in callbacks)
            {
                try
                {
                    pendingCallback?.Invoke(success, error ?? string.Empty);
                }
                catch (Exception exception)
                {
                    Logs.Error($"Permission callback failed for '{permissionKey}': {exception}",
                        LogChannels.Security);
                }
            }
        }

        private void OnPermissionConnectionDisconnected(IClientSocket connection)
        {
            RemovePermissionState(connection, Mst.Errors.Parse(ResponseStatus.NotConnected), true);
        }

        private void OnPermissionConnectionStatusChanged(IClientSocket connection, ConnectionStatus status)
        {
            if (status == ConnectionStatus.Disconnected || status == ConnectionStatus.Connecting)
                RemovePermissionState(connection, Mst.Errors.Parse(ResponseStatus.NotConnected), true);
        }

        private void RemovePermissionState(IClientSocket connection, string error, bool notifyPermissionChange)
        {
            if (connection == null ||
                !permissionStates.TryRemove(connection, out PermissionConnectionState state))
            {
                return;
            }

            connection.OnConnectionCloseEvent -= OnPermissionConnectionDisconnected;
            var callbacks = new List<SuccessCallback>();
            bool hadPermissions;
            ConnectionStatusDelegate statusChangedHandler;

            lock (state.SyncRoot)
            {
                hadPermissions = state.GrantedPermissions.Count > 0;
                statusChangedHandler = state.StatusChangedHandler;

                foreach (List<SuccessCallback> pendingCallbacks in state.PendingRequests.Values)
                    callbacks.AddRange(pendingCallbacks);

                state.PendingRequests.Clear();
                state.GrantedPermissions.Clear();
                state.StatusChangedHandler = null;
                state.IsLifecycleSubscribed = false;
            }

            if (statusChangedHandler != null)
                connection.OnStatusChangedEvent -= statusChangedHandler;

            foreach (SuccessCallback pendingCallback in callbacks)
            {
                try
                {
                    pendingCallback?.Invoke(false, error ?? string.Empty);
                }
                catch (Exception exception)
                {
                    Logs.Error($"Permission disconnect callback failed: {exception}", LogChannels.Security);
                }
            }

            if (notifyPermissionChange && hadPermissions && ReferenceEquals(connection, Connection))
                RaisePermissionsLevelChanged();
        }

        private void RaisePermissionsLevelChanged()
        {
            Action handlers = OnPermissionsLevelChangedEvent;

            if (handlers == null)
                return;

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception exception)
                {
                    Logs.Error($"Permission level change subscriber failed: {exception}", LogChannels.Security);
                }
            }
        }

        /// <summary>
        /// Loads an existing master key ring or creates and persists one when the file is missing.
        /// A server must complete this step before issuing encryption challenges or protecting data.
        /// </summary>
        public bool InitializeServer(string keyRingFile, out string error)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            error = "A master server cannot initialize its key ring in a WebGL player";
            return false;
#else
            if (!MstSecurityKeyRing.TryLoadOrCreate(
                    keyRingFile,
                    out MstSecurityKeyRing keyRing,
                    out error))
            {
                return false;
            }

            MstSecurityKeyRing previousKeyRing;

            lock (keyRingSync)
            {
                previousKeyRing = serverKeyRing;
                serverKeyRing = keyRing;
            }

            previousKeyRing?.Dispose();

            return true;
#endif
        }

        /// <summary>
        /// Encrypts data with the active server data key and binds it to a purpose.
        /// </summary>
        public byte[] Encrypt(byte[] data, string purpose)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException(
                "Server-side protected data encryption is not available in WebGL");
#else
            ValidatePlaintext(data);
            MstSecurityKeyRing keyRing = GetServerKeyRing();
            MstSecurityKeyRing.DataKey activeKey = keyRing.ActiveDataKey;
            byte[] derivedKey = MstManagedCryptoBackend.DeriveKey(activeKey.Key, purpose);
            byte[] nonce = MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.NonceSize);

            try
            {
                byte[] associatedData =
                    MstSecurityProtocol.CreateDataAssociatedData(purpose, activeKey.Id);
                byte[] cipherText = MstManagedCryptoBackend.EncryptAead(
                    derivedKey,
                    nonce,
                    data,
                    associatedData,
                    out byte[] authenticationTag);

                return new MstProtectedDataPacket
                {
                    KeyId = activeKey.Id,
                    Nonce = nonce,
                    CipherText = cipherText,
                    AuthenticationTag = authenticationTag
                }.ToBytes();
            }
            finally
            {
                Array.Clear(derivedKey, 0, derivedKey.Length);
            }
#endif
        }

        /// <summary>
        /// Encrypts UTF-8 text with the active server data key and binds it to a purpose.
        /// </summary>
        public string Encrypt(string value, string purpose)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return MstSecurityProtocol.ToBase64Url(
                Encrypt(Encoding.UTF8.GetBytes(value), purpose));
        }

        /// <summary>
        /// Decrypts protected data only when its key, purpose and authentication tag are valid.
        /// </summary>
        public bool TryDecrypt(byte[] encryptedData, string purpose, out byte[] data)
        {
            data = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            try
            {
                if (encryptedData == null ||
                    encryptedData.Length == 0 ||
                    encryptedData.Length > MstNetworkLimits.MaxAuthenticationCiphertextByteCount)
                {
                    return false;
                }

                MstSecurityProtocol.ValidatePurpose(purpose);
                MstProtectedDataPacket packet =
                    SerializablePacket.FromBytes<MstProtectedDataPacket>(encryptedData);
                MstSecurityKeyRing keyRing = GetServerKeyRing();

                if (!keyRing.TryGetDataKey(packet.KeyId, out MstSecurityKeyRing.DataKey dataKey))
                    return false;

                byte[] derivedKey = MstManagedCryptoBackend.DeriveKey(dataKey.Key, purpose);

                try
                {
                    byte[] associatedData =
                        MstSecurityProtocol.CreateDataAssociatedData(purpose, packet.KeyId);
                    return MstManagedCryptoBackend.TryDecryptAead(
                        derivedKey,
                        packet.Nonce,
                        packet.CipherText,
                        packet.AuthenticationTag,
                        associatedData,
                        out data);
                }
                finally
                {
                    Array.Clear(derivedKey, 0, derivedKey.Length);
                }
            }
            catch
            {
                data = null;
                return false;
            }
#endif
        }

        /// <summary>
        /// Decrypts protected UTF-8 text only when its key, purpose and authentication tag are valid.
        /// </summary>
        public bool TryDecrypt(string encryptedValue, string purpose, out string value)
        {
            value = null;

            if (!MstSecurityProtocol.TryFromBase64Url(encryptedValue, out byte[] encryptedData) ||
                !TryDecrypt(encryptedData, purpose, out byte[] plainData))
            {
                return false;
            }

            try
            {
                value = Encoding.UTF8.GetString(plainData);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Encrypts client data for one target master request. The callback is invoked on the
        /// same Unity thread that processes the socket response.
        /// </summary>
        public void EncryptForMaster(
            byte[] data,
            string purpose,
            ushort targetOpCode,
            Action<byte[], string> callback,
            IClientSocket connection = null)
        {
            connection ??= Connection;

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            try
            {
                ValidatePlaintext(data);
                MstSecurityProtocol.ValidatePurpose(purpose);

                if (targetOpCode == 0)
                    throw new ArgumentOutOfRangeException(nameof(targetOpCode));

                if (connection == null || !connection.IsConnected)
                {
                    callback(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                    return;
                }

                var request = new MstSealChallengeRequestPacket
                {
                    TargetOpCode = targetOpCode,
                    Purpose = purpose
                };

                connection.SendMessage(
                    MstOpCodes.SealChallengeRequest,
                    request,
                    (status, response) =>
                    {
                        if (status != ResponseStatus.Success || response == null)
                        {
                            callback(null, status == ResponseStatus.Success
                                ? Mst.Errors.Parse(ResponseStatus.Invalid,
                                    CreateErrorProperties(MstErrorCodes.ENCRYPTION_CHALLENGE_INVALID))
                                : Mst.Errors.Parse(status, response));
                            return;
                        }

                        try
                        {
                            MstSealChallengePacket challenge =
                                response.AsPacket<MstSealChallengePacket>();

                            byte[] associatedData = MstSecurityProtocol.CreateAssociatedData(
                                purpose,
                                targetOpCode,
                                challenge.KeyId,
                                challenge.ChallengeId,
                                challenge.ChallengeNonce);

                            EncryptForMasterWithChallenge(
                                data,
                                challenge,
                                associatedData,
                                callback);
                        }
                        catch (Exception exception)
                        {
                            Logs.Error(
                                $"Failed to prepare encrypted master request: {exception}",
                                LogChannels.Security);
                            callback(null, Mst.Errors.Parse(ResponseStatus.Invalid,
                                CreateErrorProperties(MstErrorCodes.ENCRYPTION_CHALLENGE_INVALID)));
                        }
                    });
            }
            catch (Exception exception)
            {
                Logs.Error(
                    $"Failed to start master encryption request: {exception}",
                    LogChannels.Security);
                callback(null, Mst.Errors.Parse(ResponseStatus.Invalid,
                    CreateErrorProperties(MstErrorCodes.ENCRYPTION_REQUEST_INVALID)));
            }
        }

        /// <summary>
        /// Encrypts UTF-8 text for one target master request.
        /// </summary>
        public void EncryptForMaster(
            string value,
            string purpose,
            ushort targetOpCode,
            Action<string, string> callback,
            IClientSocket connection = null)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            EncryptForMaster(
                Encoding.UTF8.GetBytes(value),
                purpose,
                targetOpCode,
                (encryptedData, error) =>
                {
                    callback(
                        encryptedData == null
                            ? null
                            : MstSecurityProtocol.ToBase64Url(encryptedData),
                        error);
                },
                connection);
        }

        internal bool TryCreateSealChallenge(
            SecurityInfoPeerExtension extension,
            MstSealChallengeRequestPacket request,
            out MstSealChallengePacket challenge,
            out string error)
        {
            challenge = null;
            error = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            error = "Master encryption is unavailable in WebGL";
            return false;
#else
            try
            {
                if (extension == null)
                {
                    error = "Security context is missing";
                    return false;
                }

                if (request == null)
                {
                    error = "Encryption challenge request is missing";
                    return false;
                }

                MstSecurityKeyRing keyRing = GetServerKeyRing();
                MstSecurityKeyRing.SealKey sealKey = keyRing.ActiveSealKey;
                long expiresAtUtcTicks =
                    DateTime.UtcNow.Add(MstSecurityProtocol.ChallengeLifetime).Ticks;

                for (int attempt = 0; attempt < 4; attempt++)
                {
                    byte[] challengeId =
                        MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.ChallengeIdSize);
                    byte[] challengeNonce =
                        MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.ChallengeNonceSize);

                    if (!extension.TryIssueSealChallenge(
                            request.Purpose,
                            request.TargetOpCode,
                            sealKey.Id,
                            challengeId,
                            challengeNonce,
                            expiresAtUtcTicks))
                    {
                        continue;
                    }

                    challenge = new MstSealChallengePacket
                    {
                        KeyId = sealKey.Id,
                        PublicKeySpki = (byte[])sealKey.PublicKeySpki.Clone(),
                        ChallengeId = challengeId,
                        ChallengeNonce = challengeNonce
                    };
                    return true;
                }

                error = "Too many pending encryption challenges";
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
#endif
        }

        internal bool TryDecryptFromClient(
            byte[] encryptedData,
            string purpose,
            ushort targetOpCode,
            IPeer peer,
            out byte[] data)
        {
            data = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            try
            {
                if (encryptedData == null ||
                    encryptedData.Length == 0 ||
                    encryptedData.Length > MstNetworkLimits.MaxAuthenticationCiphertextByteCount ||
                    peer == null)
                {
                    return false;
                }

                MstSecurityProtocol.ValidatePurpose(purpose);
                MstSealedMessagePacket packet =
                    SerializablePacket.FromBytes<MstSealedMessagePacket>(encryptedData);
                SecurityInfoPeerExtension extension =
                    peer.GetExtension<SecurityInfoPeerExtension>();

                if (extension == null ||
                    !extension.TryConsumeSealChallenge(
                        packet.ChallengeId,
                        out string challengePurpose,
                        out ushort challengeTargetOpCode,
                        out string challengeKeyId,
                        out byte[] challengeNonce))
                {
                    return false;
                }

                if (!string.Equals(challengePurpose, purpose, StringComparison.Ordinal) ||
                    challengeTargetOpCode != targetOpCode ||
                    !string.Equals(challengeKeyId, packet.KeyId, StringComparison.Ordinal))
                {
                    return false;
                }

                MstSecurityKeyRing keyRing = GetServerKeyRing();

                if (!keyRing.TryGetSealKey(
                        packet.KeyId,
                        out MstSecurityKeyRing.SealKey sealKey) ||
                    !MstManagedCryptoBackend.TryUnwrapKey(
                        sealKey.PrivateKeyPkcs8,
                        packet.EncryptedDataKey,
                        out byte[] dataKey))
                {
                    return false;
                }

                try
                {
                    byte[] associatedData = MstSecurityProtocol.CreateAssociatedData(
                        purpose,
                        targetOpCode,
                        packet.KeyId,
                        packet.ChallengeId,
                        challengeNonce);
                    return MstManagedCryptoBackend.TryDecryptAead(
                        dataKey,
                        packet.Nonce,
                        packet.CipherText,
                        packet.AuthenticationTag,
                        associatedData,
                        out data);
                }
                finally
                {
                    Array.Clear(dataKey, 0, dataKey.Length);
                }
            }
            catch
            {
                data = null;
                return false;
            }
#endif
        }

        private void EncryptForMasterWithChallenge(
            byte[] data,
            MstSealChallengePacket challenge,
            byte[] associatedData,
            Action<byte[], string> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MstWebGlCryptoBridge.EncryptForMaster(
                challenge.PublicKeySpki,
                data,
                associatedData,
                result =>
                {
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Logs.Error($"WebGL master encryption failed: {result.Error}",
                            LogChannels.Security);
                        InvokeEncryptionCallback(callback, null,
                            Mst.Errors.Parse(ResponseStatus.Error,
                                CreateErrorProperties(MstErrorCodes.ENCRYPTION_FAILED)));
                        return;
                    }

                    try
                    {
                        byte[] packet = new MstSealedMessagePacket
                        {
                            KeyId = challenge.KeyId,
                            ChallengeId = challenge.ChallengeId,
                            EncryptedDataKey = result.EncryptedDataKey,
                            Nonce = result.Nonce,
                            CipherText = result.CipherText,
                            AuthenticationTag = result.AuthenticationTag
                        }.ToBytes();
                        InvokeEncryptionCallback(callback, packet, null);
                    }
                    catch (Exception exception)
                    {
                        Logs.Error($"Failed to create WebGL encrypted request packet: {exception}",
                            LogChannels.Security);
                        InvokeEncryptionCallback(callback, null,
                            Mst.Errors.Parse(ResponseStatus.Error,
                                CreateErrorProperties(MstErrorCodes.ENCRYPTION_FAILED)));
                    }
                });
#else
            byte[] dataKey =
                MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.DataKeySize);

            try
            {
                byte[] nonce =
                    MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.NonceSize);
                byte[] cipherText = MstManagedCryptoBackend.EncryptAead(
                    dataKey,
                    nonce,
                    data,
                    associatedData,
                    out byte[] authenticationTag);
                byte[] encryptedDataKey =
                    MstManagedCryptoBackend.WrapKey(challenge.PublicKeySpki, dataKey);
                byte[] packet = new MstSealedMessagePacket
                {
                    KeyId = challenge.KeyId,
                    ChallengeId = challenge.ChallengeId,
                    EncryptedDataKey = encryptedDataKey,
                    Nonce = nonce,
                    CipherText = cipherText,
                    AuthenticationTag = authenticationTag
                }.ToBytes();
                InvokeEncryptionCallback(callback, packet, null);
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to encrypt master request: {exception}", LogChannels.Security);
                InvokeEncryptionCallback(callback, null,
                    Mst.Errors.Parse(ResponseStatus.Error,
                        CreateErrorProperties(MstErrorCodes.ENCRYPTION_FAILED)));
            }
            finally
            {
                Array.Clear(dataKey, 0, dataKey.Length);
            }
#endif
        }

        private static void InvokeEncryptionCallback(
            Action<byte[], string> callback,
            byte[] encryptedData,
            string error)
        {
            try
            {
                callback(encryptedData, error);
            }
            catch (Exception exception)
            {
                Logs.Error(
                    $"Master encryption callback failed: {exception}",
                    LogChannels.Security);
            }
        }

        private static void ValidatePlaintext(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length > MstNetworkLimits.MaxAuthenticationPlaintextByteCount)
            {
                throw new InvalidDataException(
                    $"Plaintext length {data.Length} exceeds the allowed limit " +
                    $"{MstNetworkLimits.MaxAuthenticationPlaintextByteCount}");
            }
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private MstSecurityKeyRing GetServerKeyRing()
        {
            lock (keyRingSync)
            {
                return serverKeyRing ??
                    throw new InvalidOperationException(
                        "MST server security key ring is not initialized");
            }
        }
#endif

        private static MstProperties CreateErrorProperties(string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return properties;
        }

        /// <summary>
        /// Creates a versioned PBKDF2-HMAC-SHA256 hash of the password.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <returns>The hash of the password.</returns>
        public string CreateHash(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            // Generate a random salt
            byte[] salt = new byte[SALT_BYTE_SIZE];

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(salt);

            // Hash the password and encode the parameters
            byte[] hash = PBKDF2Sha256(password, salt, PBKDF2_ITERATIONS, HASH_BYTE_SIZE);
            return string.Join(
                "$",
                CurrentPasswordHashVersion,
                PBKDF2_ITERATIONS,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        /// <summary>
        /// Validates a password given a hash of the correct one.
        /// </summary>
        /// <param name="password">The password to check.</param>
        /// <param name="correctHash">A hash of the correct password.</param>
        /// <returns>True if the password is correct. False otherwise.</returns>
        public bool ValidatePassword(string password, string correctHash)
        {
            if (password == null || string.IsNullOrWhiteSpace(correctHash))
                return false;

            try
            {
                if (correctHash.StartsWith(CurrentPasswordHashVersion + "$", StringComparison.Ordinal))
                    return ValidateCurrentPasswordHash(password, correctHash);

                return ValidateLegacyPasswordHash(password, correctHash);
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is ArgumentException ||
                exception is CryptographicException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns whether a successfully validated password should be stored with
        /// the current hash format.
        /// </summary>
        public bool NeedsPasswordRehash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash) ||
                !passwordHash.StartsWith(CurrentPasswordHashVersion + "$", StringComparison.Ordinal))
                return true;

            string[] parts = passwordHash.Split('$');
            return parts.Length != 4 ||
                   !int.TryParse(parts[1], out int iterations) ||
                   iterations < PBKDF2_ITERATIONS;
        }

        /// <summary>
        /// Compares two byte arrays in length-constant time. This comparison
        /// method is used so that password hashes cannot be extracted from
        /// on-line systems using a timing attack and then attacked off-line.
        /// </summary>
        /// <param name="a">The first byte array.</param>
        /// <param name="b">The second byte array.</param>
        /// <returns>True if both byte arrays are equal. False otherwise.</returns>
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null)
                return false;

            uint diff = (uint)a.Length ^ (uint)b.Length;

            for (var i = 0; (i < a.Length) && (i < b.Length); i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }

            return diff == 0;
        }

        public static byte[] CreateAccessProof(string secret, string service, string permissionKey,
            byte[] challengeId, byte[] nonce, long expiresAtUtcTicks)
        {
            byte[] payload = BuildAccessProofPayload(service, permissionKey, challengeId, nonce, expiresAtUtcTicks);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? string.Empty));
            return hmac.ComputeHash(payload);
        }

        private static byte[] BuildAccessProofPayload(string service, string permissionKey,
            byte[] challengeId, byte[] nonce, long expiresAtUtcTicks)
        {
            using var stream = new MemoryStream();
            using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, stream))
            {
                writer.Write(ServerAccessChallengePacket.CurrentVersion);
                writer.Write(service ?? string.Empty);
                writer.Write(permissionKey ?? string.Empty);
                writer.Write(challengeId ?? Array.Empty<byte>());
                writer.Write(nonce ?? Array.Empty<byte>());
                writer.Write(expiresAtUtcTicks);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// Creates a cryptographically secure random secret encoded as Base64.
        /// </summary>
        public static string CreateRandomSecret(int byteCount = 32)
        {
            if (byteCount < 16 || byteCount > 1024)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            byte[] bytes = new byte[byteCount];

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);

            return Convert.ToBase64String(bytes);
        }

        public void Dispose()
        {
            foreach (IClientSocket connection in permissionStates.Keys)
                RemovePermissionState(connection, "Security service disposed", false);

#if UNITY_WEBGL && !UNITY_EDITOR
            MstWebGlCryptoBridge.CancelAll("Security service disposed");
#else
            MstSecurityKeyRing keyRing;

            lock (keyRingSync)
            {
                keyRing = serverKeyRing;
                serverKeyRing = null;
            }

            keyRing?.Dispose();
#endif

            OnPermissionsLevelChangedEvent = null;
        }

        /// <summary>
        /// Computes the legacy PBKDF2-SHA1 hash of a password.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <param name="salt">The salt.</param>
        /// <param name="iterations">The PBKDF2 iteration count.</param>
        /// <param name="outputBytes">The length of the hash to generate, in bytes.</param>
        /// <returns>A hash of the password.</returns>
        private static byte[] PBKDF2(string password, byte[] salt, int iterations, int outputBytes)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt)
            {
                IterationCount = iterations
            })
            {
                return pbkdf2.GetBytes(outputBytes);
            }
        }

        private static byte[] PBKDF2Sha256(
            string password,
            byte[] salt,
            int iterations,
            int outputBytes)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(outputBytes);
            }
        }

        private static bool ValidateCurrentPasswordHash(string password, string passwordHash)
        {
            string[] parts = passwordHash.Split('$');

            if (parts.Length != 4 ||
                parts[0] != CurrentPasswordHashVersion ||
                !int.TryParse(parts[1], out int iterations) ||
                !IsAcceptedIterationCount(iterations))
                return false;

            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] hash = Convert.FromBase64String(parts[3]);

            if (!ArePasswordHashParametersValid(salt, hash))
                return false;

            byte[] testHash = PBKDF2Sha256(password, salt, iterations, hash.Length);
            return FixedTimeEquals(hash, testHash);
        }

        private static bool ValidateLegacyPasswordHash(string password, string passwordHash)
        {
            string[] parts = passwordHash.Split(':');

            if (parts.Length != 3 ||
                !int.TryParse(parts[ITERATION_INDEX_IN_HASH], out int iterations) ||
                !IsAcceptedIterationCount(iterations))
                return false;

            byte[] salt = Convert.FromBase64String(parts[SALT_INDEX_IN_HASH]);
            byte[] hash = Convert.FromBase64String(parts[PBKDF2_INDEX_IN_HASH]);

            if (!ArePasswordHashParametersValid(salt, hash))
                return false;

            byte[] testHash = PBKDF2(password, salt, iterations, hash.Length);
            return FixedTimeEquals(hash, testHash);
        }

        private static bool IsAcceptedIterationCount(int iterations)
        {
            return iterations > 0 && iterations <= MaximumAcceptedPbkdf2Iterations;
        }

        private static bool ArePasswordHashParametersValid(byte[] salt, byte[] hash)
        {
            return salt.Length >= 8 &&
                   salt.Length <= 64 &&
                   hash.Length >= 16 &&
                   hash.Length <= 64;
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
