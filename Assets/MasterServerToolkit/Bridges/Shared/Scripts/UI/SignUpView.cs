using Aevien.UI;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Games
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
            Mst.Events.AddEventListener(MstEventKeys.showSignUpView, OnShowSignUpEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hideSignUpView, OnHideSignUpEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.setSignUpDefaultCredentials, OnSetDefaultCredentialsEventHandler);
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

            var credentials = message.GetData<MstProperties>();

            if (credentials.Has(MstDictKeys.userName) && credentials.Has(MstDictKeys.userPassword))
                SetInputFieldsValues(credentials.AsString(MstDictKeys.userName), credentials.AsString(MstDictKeys.userEmail), credentials.AsString(MstDictKeys.userPassword));
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
            AuthBehaviour.Instance.SignUp(Username, Email, Password);
        }
        
        /// <summary>
        /// Shows sing in view by sending event
        /// </summary>
        public void ShowSignInView()
        {
            Mst.Events.Invoke(MstEventKeys.showSignInView);
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