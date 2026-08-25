using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class SignUpView : UIView
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("Input field whose current text is submitted as the new account username.")]
        private TMP_InputField usernameInputField;
        [SerializeField]
        [Tooltip("Input field whose current text is submitted as the new account email address.")]
        private TMP_InputField emailInputField;
        [SerializeField]
        [Tooltip("Input field whose current text is submitted as the new account password.")]
        private TMP_InputField passwordInputField;
        [SerializeField]
        [Tooltip("Password confirmation input populated by editor defaults and SetInputFieldsValues. The current view does not validate it before submitting registration.")]
        private TMP_InputField confirmPasswordInputField;

        private IDisposable setDefaultCredentialsListener;

#if UNITY_EDITOR
        [Header("Editor Settings"), SerializeField]
        [Tooltip("Editor-only username copied into the sign-up form during Awake when Use Default Credentials is enabled.")]
        protected string defaultUsername = "qwerty";
        [SerializeField]
        [Tooltip("Editor-only email copied into the sign-up form during Awake when Use Default Credentials is enabled.")]
        protected string defaultEmail = "qwerty@mail.com";
        [SerializeField]
        [Tooltip("Editor-only password copied into both password fields during Awake when Use Default Credentials is enabled.")]
        protected string defaultPassword = "qwerty123!@#";
        [SerializeField]
        [Tooltip("When enabled in the Unity Editor, fills the sign-up fields from the editor-only default credentials during Awake. This setting is excluded from player builds.")]
        protected bool useDefaultCredentials = true;
#endif

#endregion

        public string Username
        {
            get
            {
                return usernameInputField != null ? usernameInputField.text : string.Empty;
            }
        }

        public string Email
        {
            get
            {
                return emailInputField != null ? emailInputField.text : string.Empty;
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
                emailInputField.text = defaultEmail;
                passwordInputField.text = defaultPassword;
                confirmPasswordInputField.text = defaultPassword;
            }
#endif
            setDefaultCredentialsListener?.Dispose();
            setDefaultCredentialsListener = Mst.Events.AddListener(MstEventKeys.setSignUpDefaultCredentials, OnSetDefaultCredentialsEventHandler);
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
                SetInputFieldsValues(credentials.AsString(MstParamKeys.USER_NAME), credentials.AsString(MstParamKeys.USER_EMAIL), credentials.AsString(MstParamKeys.USER_PASSWORD));
        }

        /// <summary>
        /// Sets default credentials
        /// </summary>
        /// <param name="username"></param>
        /// <param name="email"></param>
        /// <param name="password"></param>
        public void SetInputFieldsValues(string username, string email, string password)
        {
            usernameInputField.text = username;
            emailInputField.text = email;
            passwordInputField.text = password;
            confirmPasswordInputField.text = password;
        }

        /// <summary>
        /// Sends sign up request to master server
        /// </summary>
        public void SignUp()
        {
            ViewsManager.Show<LoadingInfoView>(Mst.Localization["ui.loading.signUp.message"]);

            Logger.Debug(Mst.Localization["ui.loading.signUp.message"]);

            var credentials = new MstProperties();
            credentials.Set(MstParamKeys.USER_NAME, Username);
            credentials.Set(MstParamKeys.USER_EMAIL, Email);
            credentials.Set(MstParamKeys.USER_PASSWORD, Password);

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.SignUp(credentials, (status, accountInfo, error) =>
                {
                    ViewsManager.Hide<LoadingInfoView>();

                    if (status == ResponseStatus.Success && accountInfo != null)
                    {
                        ViewsManager.Hide<SignUpView>();

                        if (accountInfo.IsEmailConfirmed)
                            ViewsManager.Show<MainMenuView>();
                        else
                            Mst.Events.Invoke(MstEventKeys.showEmailConfirmationView, accountInfo.Email);

                        Logger.Debug(Mst.Localization["ui.notification.signUp.success.message"]);
                    }
                    else
                    {
                        string outputMessage = $"{Mst.Localization["ui.notification.signUp.error.message"]} {error}";
                        Logger.Error(outputMessage);

                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            ViewsManager.Show<SignUpView>();
                        }));
                    }
                });
            });
        }

        /// <summary>
        /// Shows sing in view by sending event
        /// </summary>
        public void ShowSignInView()
        {
            ViewsManager.Show<SignInView>();
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
