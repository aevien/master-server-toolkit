using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Games
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
            Mst.Events.AddEventListener(MstEventKeys.showPasswordResetCodeView, OnShowPasswordResetCodeEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hidePasswordResetCodeView, OnHidePasswordResetCodeEventHandler);
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
            AuthBehaviour.Instance.RequestResetPasswordCode(Email);
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
