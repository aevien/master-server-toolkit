using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class PasswordResetCodeView : UIView
    {
        [Header("Components"), SerializeField]
        [Tooltip("Input field containing the account email for which a password reset code will be requested.")]
        private TMP_InputField emailInputField;

        private IDisposable showPasswordResetCodeListener;
        private IDisposable hidePasswordResetCodeListener;

        public string Email
        {
            get
            {
                return emailInputField != null ? emailInputField.text : string.Empty;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // Listen to show/hide events
            showPasswordResetCodeListener?.Dispose();
            showPasswordResetCodeListener = Mst.Events.AddListener(MstEventKeys.showPasswordResetCodeView, OnShowPasswordResetCodeEventHandler);

            hidePasswordResetCodeListener?.Dispose();
            hidePasswordResetCodeListener = Mst.Events.AddListener(MstEventKeys.hidePasswordResetCodeView, OnHidePasswordResetCodeEventHandler);
        }

        protected override void OnDestroy()
        {
            showPasswordResetCodeListener?.Dispose();
            showPasswordResetCodeListener = null;

            hidePasswordResetCodeListener?.Dispose();
            hidePasswordResetCodeListener = null;

            base.OnDestroy();
        }

        private void OnShowPasswordResetCodeEventHandler(EventPayload message)
        {
            Show();
        }

        private void OnHidePasswordResetCodeEventHandler(EventPayload message)
        {
            Hide();
        }

        /// <summary>
        /// Sends request to master to generate rest password code and send it to user email
        /// </summary>
        public void RequestResetPasswordCode()
        {
            //if (AuthBehaviour.Instance)
            //    AuthBehaviour.Instance.RequestResetPasswordCode(Email);
            //else
            //    logger.Error($"No instance of {nameof(AuthBehaviour)} found. Please add {nameof(AuthBehaviour)} to scene to be able to use auth logic");
        }

        /// <summary>
        /// Shows sing in view by sending event
        /// </summary>
        public void ShowSignInView()
        {
            //Mst.Events.Invoke(MstEventKeys.showSignInView);
        }
    }
}
