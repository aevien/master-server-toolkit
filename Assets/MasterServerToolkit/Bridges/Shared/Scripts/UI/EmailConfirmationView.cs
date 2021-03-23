using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    public class EmailConfirmationView : UIView
    {
        [Header("Components"), SerializeField]
        private TMP_InputField confirmationCodeInputField;

        public string ConfirmationCode
        {
            get
            {
                return confirmationCodeInputField != null ? confirmationCodeInputField.text : string.Empty;
            }
        }

        protected override void Start()
        {
            base.Start();

            // Listen to show/hide events
            Mst.Events.AddEventListener(MstEventKeys.showEmailConfirmationView, OnShowEmailConfirmationEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hideEmailConfirmationView, OnHideEmailConfirmationEventHandler);
        }

        private void OnShowEmailConfirmationEventHandler(EventMessage message)
        {
            Show();
        }

        private void OnHideEmailConfirmationEventHandler(EventMessage message)
        {
            Hide();
        }

        /// <summary>
        /// Sends request to get confirmation code
        /// </summary>
        public void RequestConfirmationCode()
        {
            AuthBehaviour.Instance.RequestConfirmationCode();
        }

        /// <summary>
        /// Sends request to confirm account with confirmation code
        /// </summary>
        public void ConfirmAccount()
        {
            AuthBehaviour.Instance.ConfirmAccount(ConfirmationCode);
        }

        /// <summary>
        /// Sign out user
        /// </summary>
        public void SignOut()
        {
            AuthBehaviour.Instance.SignOut();
        }
    }
}