using MasterServerToolkit.Networking;
using MasterServerToolkit.Logging;
using System;
using System.Globalization;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AuthClient : MstBaseClient
    {
        private const string AccountBlockedLocalizationKey =
            "ui.error.auth.account_blocked.message";
        private const string AccountBlockedUntilLocalizationKey =
            "ui.error.auth.account_blocked_until.message";
        private const string AccountBlockedDefaultReasonLocalizationKey =
            "ui.error.auth.account_blocked.default_reason";

        private static readonly string[] LocalizedErrorCodes =
        {
            MstErrorCodes.AUTH_INTERNAL_ERROR,
            MstErrorCodes.AUTH_OPERATION_IN_PROGRESS,
            MstErrorCodes.PEER_ALREADY_AUTHENTICATED,
            MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED,
            MstErrorCodes.AUTH_SESSION_CONFLICT,
            MstErrorCodes.ACCOUNT_SWITCH_REQUIRES_LEAVING_ROOM,
            MstErrorCodes.TARGET_ACCOUNT_REQUIRES_LEAVING_ROOM,
            MstErrorCodes.AUTH_SECURITY_CONTEXT_MISSING,
            MstErrorCodes.AUTH_PAYLOAD_INVALID,
            MstErrorCodes.AUTHENTICATION_REQUIRED,
            MstErrorCodes.AUTH_PERMISSION_DENIED,
            MstErrorCodes.USER_IS_NOT_LOGGED_IN,
            MstErrorCodes.INVALID_EMAIL,
            MstErrorCodes.INVALID_USERNAME,
            MstErrorCodes.INVALID_PASSWORD,
            MstErrorCodes.INVALID_CREDENTIALS,
            MstErrorCodes.USERNAME_ALREADY_EXISTS,
            MstErrorCodes.EMAIL_ALREADY_EXISTS,
            MstErrorCodes.AUTH_SERVICE_BUSY,
            MstErrorCodes.ACCOUNT_REGISTRATION_FAILED,
            MstErrorCodes.UNSUPPORTED_AUTH_CREDENTIALS,
            MstErrorCodes.PHONE_SIGN_IN_UNSUPPORTED,
            MstErrorCodes.GUEST_LOGIN_DISABLED,
            MstErrorCodes.AUTH_TOKEN_INVALID_OR_EXPIRED,
            MstErrorCodes.PASSWORD_RESET_CODE_REQUIRED,
            MstErrorCodes.PASSWORD_RESET_CODE_INVALID,
            MstErrorCodes.PASSWORD_RESET_CODE_EXPIRED,
            MstErrorCodes.PASSWORD_RESET_CODE_ATTEMPTS_EXCEEDED,
            MstErrorCodes.EMAIL_SERVICE_UNAVAILABLE,
            MstErrorCodes.GUEST_EMAIL_CONFIRMATION_FORBIDDEN,
            MstErrorCodes.EMAIL_CONFIRMATION_CODE_INVALID,
            MstErrorCodes.EMAIL_CONFIRMATION_CODE_EXPIRED,
            MstErrorCodes.EMAIL_CONFIRMATION_CODE_ATTEMPTS_EXCEEDED,
            MstErrorCodes.EMAIL_CONFIRMATION_FAILED,
            MstErrorCodes.EMAIL_DELIVERY_FAILED,
            MstErrorCodes.EMAIL_CONFIRMATION_CODE_SAVE_FAILED,
            MstErrorCodes.EMAIL_SIGN_IN_REQUEST_INVALID,
            MstErrorCodes.EMAIL_SIGN_IN_CODE_INVALID_OR_EXPIRED,
            MstErrorCodes.EMAIL_SIGN_IN_STATE_FAILED,
            MstErrorCodes.BRIDGE_PLAYER_ID_REQUIRED,
            MstErrorCodes.BRIDGE_GUEST_IDENTITY_NOT_ALLOWED,
            MstErrorCodes.BRIDGE_VALIDATION_FAILED,
            MstErrorCodes.BRIDGE_AUTH_FAILED
        };

        /// <summary>
        /// Handles completion of an authentication operation that creates an account session.
        /// </summary>
        /// <param name="status">Exact server response status, or a locally assigned failure status.</param>
        /// <param name="accountInfo">Authenticated account on success; otherwise <c>null</c>.</param>
        /// <param name="error">User-facing error returned by the server or generated locally.</param>
        public delegate void AccountCallback(ResponseStatus status, AccountInfoPacket accountInfo, string error);

        /// <summary>
        /// Check if user is signed in
        /// </summary>
        public bool IsSignedIn => Account != null;

        /// <summary>
        /// Check if user is now logging in
        /// </summary>
        public bool IsNowSigningIn { get; protected set; }

        /// <summary>
        /// Remember user after he logged in
        /// </summary>
        public bool RememberMe { get; set; } = false;

        /// <summary>
        /// Current useraccount info
        /// </summary>
        public AccountInfoPacket Account { get; protected set; }

        /// <summary>
        /// Invokes when successfully signed in
        /// </summary>
        public event Action OnSignedInEvent;

        /// <summary>
        /// Invokes when successfully signed up
        /// </summary>
        public event Action OnSignedUpEvent;

        /// <summary>
        /// Invokes when successfully signed out
        /// </summary>
        public event Action OnSignedOutEvent;

        /// <summary>
        /// Invokes when email successfully confirmed
        /// </summary>
        public event Action OnEmailConfirmedEvent;

        /// <summary>
        /// Invokes when password successfully changed
        /// </summary>
        public event Action OnPasswordChangedEvent;

        /// <summary>
        /// Invokes when extra properties successfully changed
        /// </summary>
        public event Action OnExtraChangedEvent;

        public AuthClient(IClientSocket connection) : base(connection)
        {
            RegisterErrorFormatters();
        }

        private void RegisterErrorFormatters()
        {
            Mst.Errors.TryRegister(MstErrorCodes.ACCOUNT_BLOCKED, FormatAccountBlockedError);

            foreach (string code in LocalizedErrorCodes)
                Mst.Errors.TryRegister(code);
        }

        private static string FormatAccountBlockedError(MstProperties properties)
        {
            string reason = properties.AsString(MstErrorPropertyKeys.REASON);

            if (string.IsNullOrWhiteSpace(reason))
                reason = Mst.Errors.Localize(AccountBlockedDefaultReasonLocalizationKey);

            string expiresAtValue = properties.AsString(MstErrorPropertyKeys.EXPIRES_AT);

            if (!DateTime.TryParseExact(
                    expiresAtValue,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime expiresAt))
            {
                string blockedTemplate =
                    Mst.Errors.Localize(AccountBlockedLocalizationKey);
                return string.Format(CultureInfo.CurrentCulture, blockedTemplate, reason);
            }

            string blockedUntilTemplate = Mst.Errors.Localize(AccountBlockedUntilLocalizationKey);
            string localExpiration = expiresAt
                .ToLocalTime()
                .ToString("g", CultureInfo.CurrentCulture);

            return string.Format(
                CultureInfo.CurrentCulture,
                blockedUntilTemplate,
                localExpiration,
                reason);
        }

        private string ParseLocalError(ResponseStatus status, string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return Mst.Errors.Parse(status, properties);
        }

        /// <summary>
        /// Save authentication token
        /// </summary>
        private void SaveAuthToken(string token)
        {
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, token);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Removes the authentication token previously saved for automatic sign-in.
        /// This does not sign out the active account or remove any other local preferences.
        /// </summary>
        public void ClearAuthToken()
        {
            if (PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN))
            {
                PlayerPrefs.DeleteKey(MstParamKeys.USER_AUTH_TOKEN);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Check if we have auth token after last login in player prefs
        /// </summary>
        /// <returns></returns>
        public bool HasAuthToken()
        {
            if (PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN))
            {
                string key = PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN);
                return !string.IsNullOrEmpty(key);
            }

            return false;
        }

        /// <summary>
        /// Sends a registration request to server
        /// </summary>
        /// <param name="credentials"></param>
        /// <param name="callback"></param>
        public void SignUp(MstProperties credentials, AccountCallback callback)
        {
            SignUp(credentials, callback, Connection);
        }

        /// <summary>
        /// Sends a registration request to given connection
        /// </summary>
        public void SignUp(MstProperties credentials, AccountCallback callback, IClientSocket connection)
        {
            if (IsNowSigningIn)
            {
                callback?.Invoke(
                    ResponseStatus.Conflict,
                    null,
                    ParseLocalError(
                        ResponseStatus.Conflict,
                        MstErrorCodes.AUTH_OPERATION_IN_PROGRESS));
                return;
            }

            if (IsSignedIn && !Account.IsGuest)
            {
                callback?.Invoke(
                    ResponseStatus.Conflict,
                    null,
                    ParseLocalError(
                        ResponseStatus.Conflict,
                        MstErrorCodes.PEER_ALREADY_AUTHENTICATED));
                return;
            }

            if (!connection.IsConnected)
            {
                callback?.Invoke(
                    ResponseStatus.NotConnected,
                    null,
                    Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            bool replacesGuestSession = IsSignedIn && Account.IsGuest;
            bool replacesSavedGuestToken = replacesGuestSession && HasAuthToken();
            IsNowSigningIn = true;
            bool isCompleted = false;

            bool Complete(ResponseStatus status, AccountInfoPacket account, string error = null)
            {
                if (isCompleted)
                    return false;

                isCompleted = true;
                IsNowSigningIn = false;
                callback?.Invoke(status, account, error);
                return true;
            }

            try
            {
                Mst.Security.EncryptForMaster(
                    credentials.ToBytes(),
                    MstSecurityPurposes.AuthSignUp,
                    MstOpCodes.SignUp,
                    (encryptedData, encryptionError) =>
                {
                    if (encryptedData == null)
                    {
                        if (!string.IsNullOrEmpty(encryptionError))
                            Logs.Error($"Failed to prepare sign-up request: {encryptionError}", LogChannels.Security);

                        Complete(
                            ResponseStatus.Error,
                            null,
                            ParseLocalError(
                                ResponseStatus.Error,
                                MstErrorCodes.AUTH_SECURITY_CONTEXT_MISSING));
                        return;
                    }

                    try
                    {
                        connection.SendMessage(MstOpCodes.SignUp, encryptedData, (status, response) =>
                        {
                            if (status != ResponseStatus.Success)
                            {
                                Complete(status, null, Mst.Errors.Parse(status, response));
                                return;
                            }

                            AccountInfoPacket account;

                            try
                            {
                                account = response.AsPacket<AccountInfoPacket>();
                            }
                            catch (Exception exception)
                            {
                                Complete(
                                    ResponseStatus.Error,
                                    null,
                                    Mst.Errors.Parse(ResponseStatus.Error));
                                Logs.Error($"Failed to decode sign-up response: {exception.Message}", LogChannels.Security);
                                return;
                            }

                            if (account == null)
                            {
                                Complete(
                                    ResponseStatus.Error,
                                    null,
                                    Mst.Errors.Parse(ResponseStatus.Error));
                                Logs.Error("Failed to decode sign-up response: account info is missing", LogChannels.Security);
                                return;
                            }

                            Account = account;

                            if (!string.IsNullOrEmpty(Account.Token) &&
                                (RememberMe || Account.IsGuest || replacesSavedGuestToken))
                            {
                                try
                                {
                                    SaveAuthToken(Account.Token);
                                }
                                catch (Exception exception)
                                {
                                    Logs.Error($"Sign-up succeeded, but the auth token could not be saved: {exception}", LogChannels.Security);
                                }
                            }

                            if (Complete(ResponseStatus.Success, Account))
                            {
                                OnSignedUpEvent?.Invoke();
                                OnSignedInEvent?.Invoke();
                            }
                        });
                    }
                    catch (Exception exception)
                    {
                        if (isCompleted)
                        {
                            Logs.Error($"Sign-up completion callback failed: {exception}", LogChannels.Security);
                            return;
                        }

                        Logs.Error($"Failed to send sign-up request: {exception}", LogChannels.Security);
                        Complete(
                            connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected,
                            null,
                            Mst.Errors.Parse(
                                connection.IsConnected
                                    ? ResponseStatus.Error
                                    : ResponseStatus.NotConnected));
                    }
                },
                connection);
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to start sign-up security handshake: {exception}", LogChannels.Security);
                Complete(
                    ResponseStatus.Error,
                    null,
                    ParseLocalError(
                        ResponseStatus.Error,
                        MstErrorCodes.AUTH_SECURITY_CONTEXT_MISSING));
            }
        }

        private void ClearAuthTokenIfMatches(string rejectedToken)
        {
            if (string.IsNullOrEmpty(rejectedToken) || !HasAuthToken())
                return;

            string savedToken = PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN);

            if (string.Equals(savedToken, rejectedToken, StringComparison.Ordinal))
                ClearAuthToken();
        }

        private bool SavedAuthTokenMatches(string token)
        {
            return !string.IsNullOrEmpty(token) &&
                   HasAuthToken() &&
                   string.Equals(
                       PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                       token,
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// Initiates a log out. In the process, disconnects and connects
        /// back to the server to ensure no state data is left on the server.
        /// </summary>
        public void SignOut()
        {
            SignOut(Connection);
        }

        /// <summary>
        /// Initiates a log out. In the process, disconnects and connects
        /// back to the server to ensure no state data is left on the server.
        /// </summary>
        public void SignOut(IClientSocket connection)
        {
            if (!IsSignedIn)
            {
                return;
            }

            Account = null;
            ClearAuthToken();

            if (connection.IsConnected)
            {
                connection.SendMessage(MstOpCodes.SignOut);
            }

            OnSignedOutEvent?.Invoke();
        }

        /// <summary>
        /// Sends a request to server, to log in as a guest
        /// </summary>
        /// <param name="callback"></param>
        public void SignInAsGuest(AccountCallback callback)
        {
            SignInAsGuest(callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to log in as a guest
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void SignInAsGuest(AccountCallback callback, IClientSocket connection)
        {
            if (HasAuthToken())
            {
                SignInWithToken(PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN), callback, connection);
            }
            else
            {
                var credentials = new MstProperties();
                credentials.Add(MstParamKeys.USER_IS_GUEST, true);
                SignIn(credentials, callback, connection);
            }
        }

        /// <summary>
        /// Sends a request to server, to log in with auth token
        /// </summary>
        /// <param name="callback"></param>
        public void SignInWithToken(AccountCallback callback)
        {
            SignInWithToken(callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to log in with auth token
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void SignInWithToken(AccountCallback callback, IClientSocket connection)
        {
            if (!HasAuthToken())
            {
                callback?.Invoke(
                    ResponseStatus.TokenExpired,
                    null,
                    ParseLocalError(
                        ResponseStatus.TokenExpired,
                        MstErrorCodes.AUTH_TOKEN_INVALID_OR_EXPIRED));
                return;
            }

            SignInWithToken(PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN), callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given token
        /// </summary>
        public void SignInWithToken(string token, AccountCallback callback)
        {
            SignInWithToken(token, callback, Connection);
        }

        /// <summary>
        /// Sends a login request, using given token
        /// </summary>
        public void SignInWithToken(string token, AccountCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_AUTH_TOKEN, token);
            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithLoginAndPassword(string username, string password, AccountCallback callback)
        {
            SignInWithLoginAndPassword(username, password, callback, Connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithLoginAndPassword(string username, string password, AccountCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_NAME, username);
            credentials.Add(MstParamKeys.USER_PASSWORD, password);
            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Requests a one-time email sign-in code.
        /// </summary>
        public void SignInWithEmail(string email, SuccessCallback callback)
        {
            SignInWithEmail(email, callback, Connection);
        }

        /// <summary>
        /// Requests a one-time email sign-in code.
        /// </summary>
        public void SignInWithEmail(string email, SuccessCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_EMAIL, email);
            SignIn(
                credentials,
                (status, _, error) => callback?.Invoke(status == ResponseStatus.Success, error),
                connection,
                false);
        }

        /// <summary>
        /// Completes email sign-in with the one-time code delivered to the address.
        /// </summary>
        public void ConfirmEmailSignIn(string email, string code, AccountCallback callback)
        {
            ConfirmEmailSignIn(email, code, callback, Connection);
        }

        /// <summary>
        /// Completes email sign-in with the one-time code delivered to the address.
        /// </summary>
        public void ConfirmEmailSignIn(
            string email,
            string code,
            AccountCallback callback,
            IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_EMAIL, email);
            credentials.Add(MstParamKeys.USER_EMAIL_SIGN_IN_CODE, code);
            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithPhoneNumber(string phoneNumber, AccountCallback callback)
        {
            SignInWithPhoneNumber(phoneNumber, callback, Connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithPhoneNumber(string phoneNumber, AccountCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_PHONE_NUMBER, phoneNumber);
            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Sends a generic login request
        /// </summary>
        /// <param name="data"></param>
        /// <param name="callback"></param>
        public void SignIn(MstProperties data, AccountCallback callback)
        {
            SignIn(data, callback, Connection);
        }

        /// <summary>
        /// Sends a generic login request
        /// </summary>
        /// <param name="credentials"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void SignIn(MstProperties credentials, AccountCallback callback, IClientSocket connection)
        {
            SignIn(credentials, callback, connection, true);
        }

        protected virtual void SignIn(MstProperties credentials, AccountCallback callback, IClientSocket connection, bool expectsAccountInfo)
        {
            if (IsNowSigningIn)
            {
                callback?.Invoke(
                    ResponseStatus.Conflict,
                    null,
                    ParseLocalError(
                        ResponseStatus.Conflict,
                        MstErrorCodes.AUTH_OPERATION_IN_PROGRESS));
                return;
            }

            if (!connection.IsConnected)
            {
                callback?.Invoke(
                    ResponseStatus.NotConnected,
                    null,
                    Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            IsNowSigningIn = true;

            bool isCompleted = false;
            bool isTokenSignIn = credentials.Has(MstParamKeys.USER_AUTH_TOKEN);
            string attemptedToken = isTokenSignIn
                ? credentials.AsString(MstParamKeys.USER_AUTH_TOKEN)
                : null;

            bool Complete(ResponseStatus status, AccountInfoPacket account, string error = null)
            {
                if (isCompleted)
                    return false;

                isCompleted = true;
                IsNowSigningIn = false;
                callback?.Invoke(status, account, error);
                return true;
            }

            try
            {
                credentials.Set(MstParamKeys.USER_REMEMBER_ME, RememberMe);

                Mst.Security.EncryptForMaster(
                    credentials.ToBytes(),
                    MstSecurityPurposes.AuthSignIn,
                    MstOpCodes.SignIn,
                    (encryptedData, encryptionError) =>
                {
                    if (encryptedData == null)
                    {
                        if (!string.IsNullOrEmpty(encryptionError))
                            Logs.Error($"Failed to prepare sign-in request: {encryptionError}", LogChannels.Security);

                        Complete(
                            ResponseStatus.Error,
                            null,
                            ParseLocalError(
                                ResponseStatus.Error,
                                MstErrorCodes.AUTH_SECURITY_CONTEXT_MISSING));
                        return;
                    }

                    try
                    {
                        connection.SendMessage(MstOpCodes.SignIn, encryptedData, (status, response) =>
                        {
                            if (status != ResponseStatus.Success)
                            {
                                if (isTokenSignIn &&
                                    (status == ResponseStatus.TokenExpired ||
                                     status == ResponseStatus.Invalid ||
                                     status == ResponseStatus.Unauthorized))
                                {
                                    ClearAuthTokenIfMatches(attemptedToken);
                                }

                                Complete(status, null, Mst.Errors.Parse(status, response));
                                return;
                            }

                            if (!expectsAccountInfo)
                            {
                                Complete(ResponseStatus.Success, null);
                                return;
                            }

                            AccountInfoPacket account;

                            try
                            {
                                account = response.AsPacket<AccountInfoPacket>();
                            }
                            catch (Exception exception)
                            {
                                Complete(
                                    ResponseStatus.Error,
                                    null,
                                    Mst.Errors.Parse(ResponseStatus.Error));
                                Logs.Error($"Failed to decode sign-in response: {exception.Message}", LogChannels.Security);
                                return;
                            }

                            if (account == null)
                            {
                                Complete(
                                    ResponseStatus.Error,
                                    null,
                                    Mst.Errors.Parse(ResponseStatus.Error));
                                Logs.Error("Failed to decode sign-in response: account info is missing", LogChannels.Security);
                                return;
                            }

                            Account = account;

                            bool hasToken = !string.IsNullOrEmpty(Account.Token);

                            // A successful token sign-in rotates the server token. Replace the
                            // exact saved token even when RememberMe is currently disabled.
                            bool replacesSavedToken =
                                isTokenSignIn &&
                                SavedAuthTokenMatches(attemptedToken);

                            if (hasToken &&
                                (RememberMe || Account.IsGuest || replacesSavedToken))
                            {
                                try
                                {
                                    SaveAuthToken(Account.Token);
                                }
                                catch (Exception exception)
                                {
                                    Logs.Error($"Sign-in succeeded, but the auth token could not be saved: {exception}", LogChannels.Security);
                                }
                            }

                            if (Complete(ResponseStatus.Success, Account))
                                OnSignedInEvent?.Invoke();
                        });
                    }
                    catch (Exception exception)
                    {
                        if (isCompleted)
                        {
                            Logs.Error($"Sign-in completion callback failed: {exception}", LogChannels.Security);
                            return;
                        }

                        Logs.Error($"Failed to send sign-in request: {exception}", LogChannels.Security);
                        Complete(
                            connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected,
                            null,
                            Mst.Errors.Parse(
                                connection.IsConnected
                                    ? ResponseStatus.Error
                                    : ResponseStatus.NotConnected));
                    }
                },
                connection);
            }
            catch (Exception exception)
            {
                if (isCompleted)
                {
                    Logs.Error($"Sign-in completion callback failed: {exception}", LogChannels.Security);
                    return;
                }

                Logs.Error($"Failed to start sign-in request: {exception}", LogChannels.Security);
                Complete(
                    ResponseStatus.Error,
                    null,
                    ParseLocalError(
                        ResponseStatus.Error,
                        MstErrorCodes.AUTH_SECURITY_CONTEXT_MISSING));
            }
        }

        /// <summary>
        /// Sends an e-mail confirmation code to the server
        /// </summary>
        /// <param name="code"></param>
        /// <param name="callback"></param>
        public void ConfirmEmail(string code, SuccessCallback callback)
        {
            ConfirmEmail(code, callback, Connection);
        }

        /// <summary>
        /// Sends an e-mail confirmation code to the server
        /// </summary>
        public void ConfirmEmail(string code, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (!IsSignedIn)
            {
                callback.Invoke(
                    false,
                    ParseLocalError(
                        ResponseStatus.Unauthorized,
                        MstErrorCodes.AUTHENTICATION_REQUIRED));
                return;
            }

            connection.SendMessage(MstOpCodes.ConfirmEmail, code, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback.Invoke(true);
                OnEmailConfirmedEvent?.Invoke();
            });
        }

        /// <summary>
        /// Sends a request to server, to ask for an e-mail confirmation code
        /// </summary>
        /// <param name="callback"></param>
        public void RequestEmailConfirmationCode(SuccessCallback callback)
        {
            RequestEmailConfirmationCode(callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to ask for an e-mail confirmation code
        /// </summary>
        public void RequestEmailConfirmationCode(SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (!IsSignedIn)
            {
                callback.Invoke(
                    false,
                    ParseLocalError(
                        ResponseStatus.Unauthorized,
                        MstErrorCodes.AUTHENTICATION_REQUIRED));
                return;
            }

            connection.SendMessage(MstOpCodes.GetEmailConfirmationCode, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback.Invoke(true);
            });
        }

        /// <summary>
        /// Sends a request to server, to ask for a password reset
        /// </summary>
        public void RequestPasswordReset(string email, SuccessCallback callback)
        {
            RequestPasswordReset(email, callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to ask for a password reset
        /// </summary>
        public void RequestPasswordReset(string email, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.GetPasswordResetCode, email, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback.Invoke(true);
            });
        }

        /// <summary>
        /// Sends a new password to server
        /// </summary>
        /// <param name="email"></param>
        /// <param name="code"></param>
        /// <param name="newPassword"></param>
        /// <param name="callback"></param>
        public void ChangePassword(string email, string code, string newPassword, SuccessCallback callback)
        {
            var options = new MstProperties();
            options.Add(MstParamKeys.RESET_PASSWORD_EMAIL, email);
            options.Add(MstParamKeys.RESET_PASSWORD_CODE, code);
            options.Add(MstParamKeys.RESET_PASSWORD, newPassword);

            ChangePassword(options, callback, Connection);
        }

        /// <summary>
        /// Sends a new password to server
        /// </summary>
        public void ChangePassword(MstProperties data, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.ChangePassword, data.ToBytes(), (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                ClearAuthToken();
                callback.Invoke(true);
                OnPasswordChangedEvent?.Invoke();
            });
        }

        /// <summary>
        /// Sets one client-writable account metadata value and persists it through the server.
        /// Directly changing Account.ExtraProperties only changes the local client cache.
        /// </summary>
        /// <param name="key">Metadata key to set.</param>
        /// <param name="value">Metadata value to set.</param>
        /// <param name="callback"></param>
        public void SetProperty(string key, string value, SuccessCallback callback)
        {
            SetProperty(key, value, callback, Connection);
        }

        /// <summary>
        /// Sets one client-writable account metadata value and persists it through the server.
        /// Directly changing Account.ExtraProperties only changes the local client cache.
        /// </summary>
        /// <param name="key">Metadata key to set.</param>
        /// <param name="value">Metadata value to set.</param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void SetProperty(string key, string value, SuccessCallback callback, IClientSocket connection)
        {
            var properties = new MstProperties();
            properties.Set(key, value);
            SetProperties(properties, callback, connection);
        }

        /// <summary>
        /// Sets multiple client-writable account metadata values and persists them through the server.
        /// Extra properties are account metadata, not authoritative gameplay or entitlement state.
        /// </summary>
        /// <param name="properties">Metadata values to save on the account.</param>
        /// <param name="callback"></param>
        public void SetProperties(MstProperties properties, SuccessCallback callback)
        {
            SetProperties(properties, callback, Connection);
        }

        /// <summary>
        /// Sets multiple client-writable account metadata values and persists them through the server.
        /// Extra properties are account metadata, not authoritative gameplay or entitlement state.
        /// </summary>
        /// <param name="properties">Metadata values to save on the account.</param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void SetProperties(MstProperties properties, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            var data = properties.ToBytes();

            connection.SendMessage(MstOpCodes.SetProperties, data, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                foreach (var property in properties)
                {
                    Account.ExtraProperties.Set(property.Key, property.Value);
                }

                callback.Invoke(true);
                OnExtraChangedEvent?.Invoke();
            });
        }
    }
}
