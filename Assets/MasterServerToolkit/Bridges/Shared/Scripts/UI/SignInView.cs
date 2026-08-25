using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class SignInView : UIView
    {
        [Header("Components"), SerializeField]
        [Tooltip("Input field whose current text is submitted as the account username.")]
        private TMP_InputField usernameInputField;
        [SerializeField]
        [Tooltip("Input field whose current text is submitted as the account password.")]
        private TMP_InputField passwordInputField;

        private IDisposable setDefaultCredentialsListener;

#if UNITY_EDITOR
        [Header("Editor Settings"), SerializeField]
        [Tooltip("Editor-only username copied into the username field during Awake when Use Default Credentials is enabled.")]
        protected string defaultUsername = "qwerty";
        [SerializeField]
        [Tooltip("Editor-only password copied into the password field during Awake when Use Default Credentials is enabled.")]
        protected string defaultPassword = "qwerty123!@#";
        [SerializeField]
        [Tooltip("When enabled in the Unity Editor, fills the sign-in fields from the editor-only default credentials during Awake. This setting is excluded from player builds.")]
        protected bool useDefaultCredentials = true; 
#endif

        public string Username
        {
            get
            {
                return usernameInputField != null ? usernameInputField.text : string.Empty;
            }
        }

        public string Password
        {
            get
            {
                return passwordInputField != null ? passwordInputField.text : string.Empty;
            }
        }

        protected override void Awake()
        {
            base.Awake();

#if UNITY_EDITOR
            if (useDefaultCredentials)
            {
                usernameInputField.text = defaultUsername;
                passwordInputField.text = defaultPassword;
            }
#endif
            setDefaultCredentialsListener?.Dispose();
            setDefaultCredentialsListener = Mst.Events.AddListener(MstEventKeys.setSignInDefaultCredentials, OnSetDefaultCredentialsEventHandler);
        }

        protected override void OnDestroy()
        {
            setDefaultCredentialsListener?.Dispose();
            setDefaultCredentialsListener = null;

            base.OnDestroy();
        }

        private void OnSetDefaultCredentialsEventHandler(EventPayload message)
        {
            if (!message.HasData()) throw new Exception("No message data defined");

            var credentials = message.As<MstProperties>();

            if (credentials.Has(MstParamKeys.USER_NAME) && credentials.Has(MstParamKeys.USER_PASSWORD))
                SetInputFieldsValues(credentials.AsString(MstParamKeys.USER_NAME), credentials.AsString(MstParamKeys.USER_PASSWORD));
        }

        /// <summary>
        /// Sets default credentials
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public void SetInputFieldsValues(string username, string password)
        {
            if (usernameInputField)
                usernameInputField.text = username;

            if (passwordInputField)
                passwordInputField.text = password;
        }

        /// <summary>
        /// Sends sign in request to master server
        /// </summary>
        public void SignIn()
        {
            ViewsManager.Show<LoadingInfoView>(Mst.Localization["ui.loading.signIn.message"]);

            Logger.Debug(Mst.Localization["ui.loading.signIn.message"]);

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.SignInWithLoginAndPassword(Username, Password, (status, accountInfo, error) =>
                {
                    ViewsManager.Hide<LoadingInfoView>();

                    if (status == ResponseStatus.Success && accountInfo != null)
                    {
                        if (accountInfo.IsEmailConfirmed)
                        {
                            Logger.Debug($"{Mst.Localization["ui.notification.signIn.success.message"]} {Mst.Client.Auth.Account}");
                            ViewsManager.Show<MainMenuView>();
                        }
                        else
                        {
                            Mst.Events.Invoke(MstEventKeys.showEmailConfirmationView, Mst.Client.Auth.Account.Email);
                        }

                        ViewsManager.Hide<SignInView>();
                    }
                    else
                    {
                        string outputMessage = $"{Mst.Localization["ui.notification.signIn.error.message"]} {error}";
                        Logger.Error(outputMessage);

                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            ViewsManager.Show<SignInView>();
                        }));
                    }
                });
            });
        }

        /// <summary>
        /// Sends sign in as guest request to master server
        /// </summary>
        public void SignInAsGuest()
        {
            ViewsManager.Show<LoadingInfoView>(Mst.Localization["ui.loading.signIn.message"]);

            Logger.Debug(Mst.Localization["ui.loading.signIn.message"]);

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.SignInAsGuest((status, accountInfo, error) =>
                {
                    ViewsManager.Hide<LoadingInfoView>();

                    if (status == ResponseStatus.Success && accountInfo != null)
                    {
                        ViewsManager.Hide<SignInView>();
                        Logger.Debug($"{Mst.Localization["ui.notification.signIn.success.message"]}\n{Mst.Client.Auth.Account}");
                    }
                    else
                    {
                        string outputMessage = $"{Mst.Localization["ui.notification.signIn.error.message"]} {error}";
                        Logger.Error(outputMessage);

                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            ViewsManager.Show<SignInView>();
                        }));
                    }
                });
            });
        }

        /// <summary>
        /// Shows sing up view by sending event
        /// </summary>
        public void ShowSignUpView()
        {
            ViewsManager.Show<SignUpView>();
            Hide();
        }

        /// <summary>
        /// Shows reset password code view by sending event
        /// </summary>
        public void ShowResetPasswordCodeView()
        {
            Mst.Events.Invoke(MstEventKeys.showPasswordResetCodeView);
            Hide();
        }

        /// <summary>
        /// Quits the application
        /// </summary>
        public void Quit()
        {
            Mst.Runtime.Quit();
        }
    }
}
