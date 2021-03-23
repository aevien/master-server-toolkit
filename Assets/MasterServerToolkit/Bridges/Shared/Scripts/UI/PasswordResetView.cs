using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Games
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
            Mst.Events.AddEventListener(MstEventKeys.showPasswordResetView, OnShowPasswordResetEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hidePasswordResetView, OnHidePasswordResetEventHandler);
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

            AuthBehaviour.Instance.ResetPassword(Mst.Options.AsString(MstDictKeys.RESET_PASSWORD_EMAIL), ResetCode, NewPassword);
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