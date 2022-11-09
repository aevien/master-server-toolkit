using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class PasswordResetView : UIView
    {
        [Header("Components"), SerializeField]
        private TMP_InputField resetCodeInputField;
        [SerializeField]
        private TMP_InputField newPasswordInputField;
        [SerializeField]
        private TMP_InputField newPasswordConfirmInputField;

        public string ResetCode
        {
            get
            {
                return resetCodeInputField != null ? resetCodeInputField.text : string.Empty;
            }
        }

        public string NewPassword
        {
            get
            {
                return newPasswordInputField != null ? newPasswordInputField.text : string.Empty;
            }
        }

        public string NewPasswordConfirm
        {
            get
            {
                return newPasswordConfirmInputField != null ? newPasswordConfirmInputField.text : string.Empty;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // Listen to show/hide events
            Mst.Events.AddListener(MstEventKeys.showPasswordResetView, OnShowPasswordResetEventHandler);
            Mst.Events.AddListener(MstEventKeys.hidePasswordResetView, OnHidePasswordResetEventHandler);
        }

        private void OnShowPasswordResetEventHandler(EventMessage message)
        {
            Show();
        }

        private void OnHidePasswordResetEventHandler(EventMessage message)
        {
            Hide();
        }

        /// <summary>
        /// Send request to master server to change password
        /// </summary>
        public void ResetPassword()
        {
            if (!Mst.Options.Has(MstDictKeys.RESET_PASSWORD_EMAIL)) throw new Exception("You have no reset email");

            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.ResetPassword(Mst.Options.AsString(MstDictKeys.RESET_PASSWORD_EMAIL), ResetCode, NewPassword);
            else
                logger.Error($"No instance of {nameof(AuthBehaviour)} found. Please add {nameof(AuthBehaviour)} to scene to be able to use auth logic");
        }

        /// <summary>
        /// Shows sing in view by sending event
        /// </summary>
        public void ShowSignInView()
        {
            Mst.Events.Invoke(MstEventKeys.showSignInView);
        }
    }
}