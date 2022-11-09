using MasterServerToolkit.MasterServer;
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
        private TMP_InputField usernameInputField;
        [SerializeField]
        private TMP_InputField emailInputField;
        [SerializeField]
        private TMP_InputField passwordInputField;
        [SerializeField]
        private TMP_InputField confirmPasswordInputField;

        [Header("Editor Settings"), SerializeField]
        protected string defaultUsername = "qwerty";
        [SerializeField]
        protected string defaultEmail = "qwerty@mail.com";
        [SerializeField]
        protected string defaultPassword = "qwerty123!@#";
        [SerializeField]
        protected bool useDefaultCredentials = false;

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

            // Listen to show/hide events
            Mst.Events.AddListener(MstEventKeys.showSignUpView, OnShowSignUpEventHandler);
            Mst.Events.AddListener(MstEventKeys.hideSignUpView, OnHideSignUpEventHandler);
            Mst.Events.AddListener(MstEventKeys.setSignUpDefaultCredentials, OnSetDefaultCredentialsEventHandler);
        }

        private void OnShowSignUpEventHandler(EventMessage message)
        {
            Show();
        }

        private void OnHideSignUpEventHandler(EventMessage message)
        {
            Hide();
        }

        private void OnSetDefaultCredentialsEventHandler(EventMessage message)
        {
            if (!message.HasData()) throw new Exception("No message data defined");

            var credentials = message.As<MstProperties>();

            if (credentials.Has(MstDictKeys.USER_NAME) && credentials.Has(MstDictKeys.USER_PASSWORD))
                SetInputFieldsValues(credentials.AsString(MstDictKeys.USER_NAME), credentials.AsString(MstDictKeys.USER_EMAIL), credentials.AsString(MstDictKeys.USER_PASSWORD));
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
            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.SignUp(Username, Email, Password);
            else
                logger.Error($"No instance of {nameof(AuthBehaviour)} found. Please add {nameof(AuthBehaviour)} to scene to be able to use auth logic");
        }

        /// <summary>
        /// Shows sing in view by sending event
        /// </summary>
        public void ShowSignInView()
        {
            Mst.Events.Invoke(MstEventKeys.showSignInView);
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