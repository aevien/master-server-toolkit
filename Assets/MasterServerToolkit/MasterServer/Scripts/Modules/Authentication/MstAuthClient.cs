using MasterServerToolkit.Networking;
using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstAuthClient : MstBaseClient
    {
        public delegate void SignInCallback(AccountInfoPacket accountInfo, string error);

        /// <summary>
        /// Check if user is signed in
        /// </summary>
        public bool IsSignedIn => AccountInfo != null;

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
        public AccountInfoPacket AccountInfo { get; protected set; }

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

        public MstAuthClient(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// Save authentication token
        /// </summary>
        private void SaveAuthToken(string token)
        {
            PlayerPrefs.SetString(MstDictKeys.USER_AUTH_TOKEN, token);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Generated device id
        /// </summary>
        /// <returns></returns>
        public string DeviceId()
        {
            return DeviceId(false);
        }

        /// <summary>
        /// Generated device id. Not unity's device id if user is guest
        /// </summary>
        /// <param name="isGuest"></param>
        /// <returns></returns>
        public string DeviceId(bool isGuest)
        {
            if (!isGuest && SystemInfo.deviceUniqueIdentifier != SystemInfo.unsupportedIdentifier)
            {
                return SystemInfo.deviceUniqueIdentifier;
            }
            else
            {
                if (PlayerPrefs.HasKey(isGuest ? MstDictKeys.USER_GUEST_DEVICE_ID : MstDictKeys.USER_DEVICE_ID))
                {
                    return PlayerPrefs.GetString(isGuest ? MstDictKeys.USER_GUEST_DEVICE_ID : MstDictKeys.USER_DEVICE_ID);
                }
                else
                {
                    string deviceId = Mst.Helper.CreateGuidStringN();
                    PlayerPrefs.SetString(isGuest ? MstDictKeys.USER_GUEST_DEVICE_ID : MstDictKeys.USER_DEVICE_ID, deviceId);
                    PlayerPrefs.Save();
                    return deviceId;
                }
            }
        }

        /// <summary>
        /// Clear authentication token
        /// </summary>
        private void ClearAuthToken()
        {
            if (HasAuthToken())
            {
                PlayerPrefs.DeleteKey(MstDictKeys.USER_AUTH_TOKEN);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Check if we have auth token after last login in player prefs
        /// </summary>
        /// <returns></returns>
        public bool HasAuthToken()
        {
            if (PlayerPrefs.HasKey(MstDictKeys.USER_AUTH_TOKEN))
            {
                string key = PlayerPrefs.GetString(MstDictKeys.USER_AUTH_TOKEN);
                return !string.IsNullOrEmpty(key);
            }

            return false;
        }

        /// <summary>
        /// Sends a registration request to server
        /// </summary>
        /// <param name="credentials"></param>
        /// <param name="callback"></param>
        public void SignUp(MstProperties credentials, SuccessCallback callback)
        {
            SignUp(credentials, callback, Connection);
        }

        /// <summary>
        /// Sends a registration request to given connection
        /// </summary>
        public void SignUp(MstProperties credentials, SuccessCallback callback, IClientSocket connection)
        {
            if (IsNowSigningIn)
            {
                callback.Invoke(false, Mst.Localization["signUpInProgress"]);
                return;
            }

            if (IsSignedIn)
            {
                callback.Invoke(false, Mst.Localization["signedInStatusOk"]);
                return;
            }

            if (!connection.IsConnected)
            {
                callback.Invoke(false, Mst.Localization["connectionStatusDisconnected"]);
                return;
            }

            credentials.Set(MstDictKeys.USER_DEVICE_ID, DeviceId());
            credentials.Set(MstDictKeys.USER_DEVICE_NAME, SystemInfo.deviceName);

            // We first need to get an aes key 
            // so that we can encrypt our login data
            Mst.Security.GetAesKey(aesKey =>
            {
                if (aesKey == null)
                {
                    callback.Invoke(false, Mst.Localization["signUpSecurityError"]);
                    return;
                }

                var encryptedData = Mst.Security.EncryptAES(credentials.ToBytes(), aesKey);

                connection.SendMessage(MstOpCodes.SignUp, encryptedData, (status, response) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        callback.Invoke(false, response.AsString(Mst.Localization["unknownError"]));
                        return;
                    }

                    callback.Invoke(true, null);

                    OnSignedUpEvent?.Invoke();
                });
            }, connection);
        }

        /// <summary>
        /// Initiates a log out. In the process, disconnects and connects
        /// back to the server to ensure no state data is left on the server.
        /// </summary>
        /// <param name="permanent">If you wish to delete auth token</param>
        public void SignOut(bool permanent = false)
        {
            SignOut(Connection, permanent);
        }

        /// <summary>
        /// Initiates a log out. In the process, disconnects and connects
        /// back to the server to ensure no state data is left on the server.
        /// </summary>
        public void SignOut(IClientSocket connection, bool permanent = false)
        {
            if (!IsSignedIn)
            {
                return;
            }

            AccountInfo = null;

            if (permanent)
                ClearAuthToken();

            if (!connection.IsConnected)
            {
                return;
            }

            connection.SendMessage(MstOpCodes.SignOut);

            if (connection != null)
            {
                connection.Reconnect();
            }

            OnSignedOutEvent?.Invoke();
        }

        /// <summary>
        /// Sends a request to server, to log in as a guest
        /// </summary>
        /// <param name="callback"></param>
        public void SignInAsGuest(SignInCallback callback)
        {
            SignInAsGuest(callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to log in as a guest
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void SignInAsGuest(SignInCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstDictKeys.USER_IS_GUEST);
            credentials.Add(MstDictKeys.USER_DEVICE_NAME, SystemInfo.deviceName);
            credentials.Add(MstDictKeys.USER_DEVICE_ID, DeviceId(true));

            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Sends a request to server, to log in with auth token
        /// </summary>
        /// <param name="callback"></param>
        public void SignInWithToken(SignInCallback callback)
        {
            SignInWithToken(callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to log in with auth token
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void SignInWithToken(SignInCallback callback, IClientSocket connection)
        {
            if (!HasAuthToken())
            {
                callback.Invoke(null, Mst.Localization["signInTokenExpired"]);
                return;
            }

            SignInWithToken(PlayerPrefs.GetString(MstDictKeys.USER_AUTH_TOKEN), callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given token
        /// </summary>
        public void SignInWithToken(string token, SignInCallback callback)
        {
            SignInWithToken(token, callback, Connection);
        }

        /// <summary>
        /// Sends a login request, using given token
        /// </summary>
        public void SignInWithToken(string token, SignInCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstDictKeys.USER_AUTH_TOKEN, token);

            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithLoginAndPassword(string username, string password, SignInCallback callback)
        {
            SignInWithLoginAndPassword(username, password, callback, Connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithLoginAndPassword(string username, string password, SignInCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstDictKeys.USER_NAME, username);
            credentials.Add(MstDictKeys.USER_PASSWORD, password);

            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithEmail(string email, SignInCallback callback)
        {
            SignInWithEmail(email, callback, Connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithEmail(string email, SignInCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstDictKeys.USER_EMAIL, email);

            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithPhoneNumber(string phoneNumber, SignInCallback callback)
        {
            SignInWithPhoneNumber(phoneNumber, callback, Connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignInWithPhoneNumber(string phoneNumber, SignInCallback callback, IClientSocket connection)
        {
            var credentials = new MstProperties();
            credentials.Add(MstDictKeys.USER_PHONE_NUMBER, phoneNumber);

            SignIn(credentials, callback, connection);
        }

        /// <summary>
        /// Sends a generic login request
        /// </summary>
        /// <param name="data"></param>
        /// <param name="callback"></param>
        public void SignIn(MstProperties data, SignInCallback callback)
        {
            SignIn(data, callback, Connection);
        }

        /// <summary>
        /// Sends a generic login request
        /// </summary>
        public void SignIn(MstProperties data, SignInCallback callback, IClientSocket connection)
        {
            if (IsNowSigningIn)
                return;

            if (!connection.IsConnected)
            {
                callback.Invoke(null, Mst.Localization["connectionStatusDisconnected"]);
                return;
            }

            IsNowSigningIn = true;

            // We first need to get an aes key 
            // so that we can encrypt our login data
            Mst.Security.GetAesKey(aesKey =>
            {
                if (string.IsNullOrEmpty(aesKey))
                {
                    IsNowSigningIn = false;
                    callback.Invoke(null, Mst.Localization["signInSecurityError"]);
                    return;
                }

                var encryptedData = Mst.Security.EncryptAES(data.ToBytes(), aesKey);

                connection.SendMessage(MstOpCodes.SignIn, encryptedData, (status, response) =>
                {
                    IsNowSigningIn = false;

                    if (status != ResponseStatus.Success)
                    {
                        ClearAuthToken();

                        callback.Invoke(null, response.AsString(Mst.Localization["unknownError"]));
                        return;
                    }

                    AccountInfo = response.AsPacket<AccountInfoPacket>();

                    // If RememberMe is checked on and we are not guset, then save auth token
                    if (RememberMe && !AccountInfo.IsGuest && !string.IsNullOrEmpty(AccountInfo.Token))
                    {
                        SaveAuthToken(AccountInfo.Token);
                    }
                    else
                    {
                        ClearAuthToken();
                    }

                    callback?.Invoke(AccountInfo, null);
                    OnSignedInEvent?.Invoke();
                });
            }, connection);
        }

        /// <summary>
        /// Binds social media account to your mst account
        /// </summary>
        /// <param name="socialMediaCredentials"></param>
        /// <param name="callback"></param>
        public void BindSocialMediaAccount(MstProperties socialMediaCredentials, SignInCallback callback)
        {
            BindSocialMediaAccount(socialMediaCredentials, callback, Connection);
        }

        /// <summary>
        /// Binds social media account to your mst account
        /// </summary>
        /// <param name="socialMediaCredentials"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void BindSocialMediaAccount(MstProperties socialMediaCredentials, SignInCallback callback, IClientSocket connection)
        {
            SignIn(socialMediaCredentials, callback, connection);
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
                callback.Invoke(false, Mst.Localization["connectionStatusDisconnected"]);
                return;
            }

            if (!IsSignedIn)
            {
                callback.Invoke(false, Mst.Localization["signedInStatusFail"]);
                return;
            }

            connection.SendMessage(MstOpCodes.ConfirmEmail, code, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString(Mst.Localization["accountConfirmationErrorResult"]));
                    return;
                }

                callback.Invoke(true, null);

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
                callback.Invoke(false, Mst.Localization["connectionStatusDisconnected"]);
                return;
            }

            if (!IsSignedIn)
            {
                callback.Invoke(false, Mst.Localization["signedInStatusFail"]);
                return;
            }

            connection.SendMessage(MstOpCodes.GetEmailConfirmationCode, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString(Mst.Localization["confirmationCodeSendingErrorResult"]));
                    return;
                }

                callback.Invoke(true, null);
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
                callback.Invoke(false, Mst.Localization["connectionStatusDisconnected"]);
                return;
            }

            connection.SendMessage(MstOpCodes.GetPasswordResetCode, email, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString(Mst.Localization["changePasswordCodeErrorResult"]));
                    return;
                }

                callback.Invoke(true, null);
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
            options.Add(MstDictKeys.RESET_PASSWORD_EMAIL, email);
            options.Add(MstDictKeys.RESET_PASSWORD_CODE, code);
            options.Add(MstDictKeys.RESET_PASSWORD, newPassword);

            ChangePassword(options, callback, Connection);
        }

        /// <summary>
        /// Sends a new password to server
        /// </summary>
        public void ChangePassword(MstProperties data, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, Mst.Localization["connectionStatusDisconnected"]);
                return;
            }

            connection.SendMessage(MstOpCodes.ChangePassword, data.ToBytes(), (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString(Mst.Localization["changePasswordErrorResult"]));
                    return;
                }

                callback.Invoke(true, null);
                OnPasswordChangedEvent?.Invoke();
            });
        }

        /// <summary>
        /// Binds extra properties to this account
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="callback"></param>
        public void BindExtraProperties(MstProperties properties, SuccessCallback callback)
        {
            BindExtraProperties(properties, callback, Connection);
        }

        /// <summary>
        /// Binds extra properties to this account
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void BindExtraProperties(MstProperties properties, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, Mst.Localization["connectionStatusDisconnected"]);
                return;
            }

            var data = properties.ToBytes();

            connection.SendMessage(MstOpCodes.BindExtraProperties, data, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString(Mst.Localization["accounSaveExtraErrorResult"]));
                    return;
                }

                foreach (var property in properties)
                {
                    AccountInfo.ExtraProperties.Set(property.Key, property.Value);
                }

                callback.Invoke(true, null);
                OnPasswordChangedEvent?.Invoke();
            });
        }
    }
}