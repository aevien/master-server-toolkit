using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class PasswordResetCodeView : UIView
    {
        [Header("Components"), SerializeField]
        private TMP_InputField emailInputField;

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
            Mst.Events.AddListener(MstEventKeys.showPasswordResetCodeView, OnShowPasswordResetCodeEventHandler);
            Mst.Events.AddListener(MstEventKeys.hidePasswordResetCodeView, OnHidePasswordResetCodeEventHandler);
        }

        private void OnShowPasswordResetCodeEventHandler(EventMessage message)
        {
            Show();
        }

        private void OnHidePasswordResetCodeEventHandler(EventMessage message)
        {
            Hide();
        }

        /// <summary>
        /// Sends request to master to generate rest password code and send it to user email
        /// </summary>
        public void RequestResetPasswordCode()
        {
            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.RequestResetPasswordCode(Email);
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
