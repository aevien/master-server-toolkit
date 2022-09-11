using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Games
{
    public class AuthBehaviour : BaseClientBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        protected ClientToMasterConnector clientToMasterConnector;

        [Header("Settings"), SerializeField]
        protected bool rememberUser = true;

        [Header("Editor Settings"), SerializeField]
        protected string defaultUsername = "qwerty";
        [SerializeField]
        protected string defaultEmail = "qwerty@mail.com";
        [SerializeField]
        protected string defaultPassword = "qwerty123!@#";
        [SerializeField]
        protected bool useDefaultCredentials = false;

        public UnityEvent OnSignedUpEvent;
        public UnityEvent OnSignedInEvent;
        public UnityEvent OnSignedOutEvent;
        public UnityEvent OnEmailConfirmedEvent;
        public UnityEvent OnPasswordChangedEvent;

        #endregion

        protected static AuthBehaviour _instance;
        protected string outputMessage = string.Empty;

        /// <summary>
        /// Master server connector
        /// </summary>
        public virtual ClientToMasterConnector Connector
        {
            get
            {
                if (!clientToMasterConnector)
                    clientToMasterConnector = FindObjectOfType<ClientToMasterConnector>();

                if (!clientToMasterConnector)
                {
                    var connectorObject = new GameObject("--CLIENT_TO_MASTER_CONNECTOR");
                    clientToMasterConnector = connectorObject.AddComponent<ClientToMasterConnector>();
                }

                return clientToMasterConnector;
            }
        }

        public static AuthBehaviour Instance
        {
            get
            {
                if (!_instance) Logs.Error("Instance of AuthBehaviour is not found");
                return _instance;
            }
        }

        protected override void Awake()
        {
            if (_instance)
            {
                Destroy(_instance.gameObject);
                return;
            }

            _instance = this;

            base.Awake();

            defaultUsername = Mst.Args.AsString("-mstDefaultUsername", defaultUsername);
            defaultEmail = Mst.Args.AsString("-mstDefaultEmail", defaultEmail);
            defaultPassword = Mst.Args.AsString("-mstDefaultPassword", defaultPassword);
            rememberUser = Mst.Args.AsBool("-mstRememberUser", rememberUser);
            useDefaultCredentials = Mst.Args.AsBool("-mstUseDefaultCredentials", useDefaultCredentials);

            // Listen to auth events
            Mst.Client.Auth.OnSignedInEvent += Auth_OnSignedInEvent;
            Mst.Client.Auth.OnSignedOutEvent += Auth_OnSignedOutEvent;
            Mst.Client.Auth.OnSignedUpEvent += Auth_OnSignedUpEvent;
            Mst.Client.Auth.OnEmailConfirmedEvent += Auth_OnEmailConfirmedEvent;
            Mst.Client.Auth.OnPasswordChangedEvent += Auth_OnPasswordChangedEvent;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // unregister from connection events
            Connection?.RemoveConnectionOpenListener(OnClientConnectedToServer);
            Connection?.RemoveConnectionCloseListener(OnClientDisconnectedFromServer);

            // Unregister from listening to auth events
            Mst.Client.Auth.OnSignedInEvent -= Auth_OnSignedInEvent;
            Mst.Client.Auth.OnSignedOutEvent -= Auth_OnSignedOutEvent;
            Mst.Client.Auth.OnSignedUpEvent -= Auth_OnSignedUpEvent;
            Mst.Client.Auth.OnEmailConfirmedEvent -= Auth_OnEmailConfirmedEvent;
            Mst.Client.Auth.OnPasswordChangedEvent -= Auth_OnPasswordChangedEvent;
        }

        protected override void OnInitialize()
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Connecting to master... Please wait!");

            // If we want to use default credentials for signin or signup views
            if (useDefaultCredentials && Mst.Runtime.IsEditor)
            {
                var credentials = new MstProperties();
                credentials.Set(MstDictKeys.USER_NAME, defaultUsername);
                credentials.Set(MstDictKeys.USER_PASSWORD, defaultPassword);
                credentials.Set(MstDictKeys.USER_EMAIL, defaultEmail);

                Mst.Events.Invoke(MstEventKeys.setSignInDefaultCredentials, credentials);
                Mst.Events.Invoke(MstEventKeys.setSignUpDefaultCredentials, credentials);
            }

            Mst.Client.Auth.RememberMe = rememberUser;

            // Listen to connection events
            Connection.AddConnectionOpenListener(OnClientConnectedToServer);
            Connection.AddConnectionCloseListener(OnClientDisconnectedFromServer, false);
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnClientConnectedToServer(IClientSocket client)
        {
            Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

            if (Mst.Client.Auth.IsSignedIn)
            {
                Auth_OnSignedInEvent();
            }
            else
            {
                if (Mst.Client.Auth.HasAuthToken())
                {
                    SignInWithToken();
                }
                else
                {
                    Mst.Events.Invoke(MstEventKeys.showSignInView);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnClientDisconnectedFromServer(IClientSocket client)
        {
            Connection?.RemoveConnectionOpenListener(OnClientConnectedToServer);
            Connection?.RemoveConnectionCloseListener(OnClientDisconnectedFromServer);

            Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
            Mst.Events.Invoke(MstEventKeys.showOkDialogBox,
                new OkDialogBoxEventMessage("The connection to the server has been lost. "
                + "Please try again or contact to the developers of the game or your internet provider.",
                () =>
                {
                    SignOut();
                    OnInitialize();
                }));
        }

        /// <summary>
        /// Invokes when user signed in
        /// </summary>
        protected virtual void Auth_OnSignedInEvent()
        {
            if (Mst.Client.Auth.AccountInfo.IsEmailConfirmed || Mst.Client.Auth.AccountInfo.IsGuest)
                OnSignedInEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when user signed up
        /// </summary>
        protected virtual void Auth_OnSignedUpEvent()
        {
            OnSignedUpEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when user signed out
        /// </summary>
        protected virtual void Auth_OnSignedOutEvent()
        {
            OnSignedOutEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when user changed his password
        /// </summary>
        protected virtual void Auth_OnPasswordChangedEvent()
        {
            OnPasswordChangedEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when user has confirmed his email
        /// </summary>
        protected virtual void Auth_OnEmailConfirmedEvent()
        {
            OnEmailConfirmedEvent?.Invoke();
        }

        /// <summary>
        /// Sends sign in request to master server
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public virtual void SignIn(string username, string password)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Signing in... Please wait!");

            logger.Debug("Signing in... Please wait!");

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.SignInWithLoginAndPassword(username, password, (accountInfo, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (accountInfo != null)
                    {
                        if (accountInfo.IsEmailConfirmed)
                        {
                            logger.Debug($"You are successfully logged in as {Mst.Client.Auth.AccountInfo}");
                        }
                        else
                        {
                            Mst.Events.Invoke(MstEventKeys.showEmailConfirmationView, Mst.Client.Auth.AccountInfo.Email);
                        }

                        Mst.Events.Invoke(MstEventKeys.hideSignInView);
                    }
                    else
                    {
                        string outputMessage = $"An error occurred while signing in: {error}";
                        logger.Error(outputMessage);

                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            Mst.Events.Invoke(MstEventKeys.showSignInView);
                        }));
                    }
                }, Connection);
            });
        }

        /// <summary>
        /// Sends sign in as guest request to master server
        /// </summary>
        public virtual void SignInAsGuest()
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Signing in as guest... Please wait!");

            logger.Debug("Signing in as guest... Please wait!");

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.SignInAsGuest((accountInfo, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (accountInfo != null)
                    {
                        Mst.Events.Invoke(MstEventKeys.hideSignInView);

                        logger.Debug($"You are successfully logged in as {Mst.Client.Auth.AccountInfo}");
                    }
                    else
                    {
                        string outputMessage = $"An error occurred while signing in: {error}";
                        logger.Error(outputMessage);

                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            Mst.Events.Invoke(MstEventKeys.showSignInView);
                        }));
                    }
                });
            });
        }

        /// <summary>
        /// Sends request to master server to signed in with token
        /// </summary>
        public virtual void SignInWithToken()
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Signing in... Please wait!");

            logger.Debug("Signing in... Please wait!");

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.SignInWithToken((accountInfo, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (accountInfo != null)
                    {
                        if (accountInfo.IsGuest || accountInfo.IsEmailConfirmed)
                        {
                            logger.Debug($"You are successfully logged in. {Mst.Client.Auth.AccountInfo}");
                        }
                        else
                        {
                            Mst.Events.Invoke(MstEventKeys.showEmailConfirmationView, Mst.Client.Auth.AccountInfo.Email);
                        }
                    }
                    else
                    {
                        outputMessage = $"An error occurred while signing in: {error}";
                        logger.Error(outputMessage);

                        Mst.Events.Invoke(MstEventKeys.showSignInView);
                    }
                });
            });
        }

        /// <summary>
        /// Sends sign up request to master server
        /// </summary>
        public virtual void SignUp(string username, string useremail, string userpassword)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Signing up... Please wait!");

            logger.Debug("Signing up... Please wait!");

            var credentials = new MstProperties();
            credentials.Set(MstDictKeys.USER_NAME, username);
            credentials.Set(MstDictKeys.USER_EMAIL, useremail);
            credentials.Set(MstDictKeys.USER_PASSWORD, userpassword);

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.SignUp(credentials, (isSuccessful, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        Mst.Events.Invoke(MstEventKeys.hideSignUpView);
                        Mst.Events.Invoke(MstEventKeys.showSignInView);
                        Mst.Events.Invoke(MstEventKeys.setSignInDefaultCredentials, credentials);

                        logger.Debug($"You have successfuly signed up. Now you may sign in");
                    }
                    else
                    {
                        string outputMessage = $"An error occurred while signing up: {error}";
                        logger.Error(outputMessage);

                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            Mst.Events.Invoke(MstEventKeys.showSignUpView);
                        }));
                    }
                });
            });
        }

        /// <summary>
        /// Send request to master server to change password
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="resetCode"></param>
        /// <param name="newPassword"></param>
        public virtual void ResetPassword(string userEmail, string resetCode, string newPassword)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Changing password... Please wait!");

            logger.Debug("Changing password... Please wait!");

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.ChangePassword(userEmail, resetCode, newPassword, (isSuccessful, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        Mst.Events.Invoke(MstEventKeys.hidePasswordResetView);
                        Mst.Events.Invoke(MstEventKeys.showSignInView);
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("You have successfuly changed your password. Now you can sign in.", null));
                    }
                    else
                    {
                        string outputMessage = $"An error occurred while changing password: {error}";
                        logger.Error(outputMessage);

                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            Mst.Events.Invoke(MstEventKeys.showPasswordResetView);
                        }));
                    }
                });
            });
        }

        /// <summary>
        /// Sends request to master to generate rest password code and send it to user email
        /// </summary>
        /// <param name="userEmail"></param>
        public virtual void RequestResetPasswordCode(string userEmail)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Sending reset password code... Please wait!");

            logger.Debug("Sending reset password code... Please wait!");

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.RequestPasswordReset(userEmail, (isSuccessful, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        Mst.Options.Set(MstDictKeys.RESET_PASSWORD_EMAIL, userEmail);

                        Mst.Events.Invoke(MstEventKeys.hidePasswordResetCodeView);
                        Mst.Events.Invoke(MstEventKeys.showPasswordResetView);
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage($"We have sent an email with reset code to your address '{userEmail}'", null));
                    }
                    else
                    {
                        string outputMessage = $"An error occurred while password reset code: {error}";
                        logger.Error(outputMessage);

                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            Mst.Events.Invoke(MstEventKeys.showPasswordResetCodeView);
                        }));
                    }
                });
            });
        }

        /// <summary>
        /// Sign out user
        /// </summary>
        public virtual void SignOut()
        {
            logger.Debug("Sign out");
            Mst.Client.Auth.SignOut(true);

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                ViewsManager.HideAllViews();
                Mst.Events.Invoke(MstEventKeys.showSignInView);
            });
        }

        /// <summary>
        /// Sends request to get confirmation code
        /// </summary>
        public virtual void RequestConfirmationCode()
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Sending confirmation code... Please wait!");

            logger.Debug("Sending confirmation code... Please wait!");

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.RequestEmailConfirmationCode((isSuccessful, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage($"We have sent an email with confirmation code to your address '{Mst.Client.Auth.AccountInfo.Email}'", null));
                    }
                    else
                    {
                        string outputMessage = $"An error occurred while requesting confirmation code: {error}";
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        /// <summary>
        /// Sends request to confirm account with confirmation code
        /// </summary>
        public virtual void ConfirmAccount(string confirmationCode)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Confirming your account... Please wait!");

            logger.Debug("Sending confirmation code... Please wait!");

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.ConfirmEmail(confirmationCode, (isSuccessful, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        Mst.Events.Invoke(MstEventKeys.hideEmailConfirmationView);
                    }
                    else
                    {
                        string outputMessage = $"An error occurred while confirming yor account: {error}";
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        /// <summary>
        /// Quits the application
        /// </summary>
        public virtual void Quit()
        {
            Mst.Runtime.Quit();
        }
    }
}