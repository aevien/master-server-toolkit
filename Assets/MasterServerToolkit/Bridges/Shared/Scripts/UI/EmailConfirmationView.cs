using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
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
            Mst.Events.AddListener(MstEventKeys.showEmailConfirmationView, OnShowEmailConfirmationEventHandler);
            Mst.Events.AddListener(MstEventKeys.hideEmailConfirmationView, OnHideEmailConfirmationEventHandler);
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
            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.RequestConfirmationCode();
            else
                logger.Error($"No instance of {nameof(AuthBehaviour)} found. Please add {nameof(AuthBehaviour)} to scene to be able to use auth logic");
        }

        /// <summary>
        /// Sends request to confirm account with confirmation code
        /// </summary>
        public void ConfirmAccount()
        {
            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.ConfirmAccount(ConfirmationCode);
            else
                logger.Error($"No instance of {nameof(AuthBehaviour)} found. Please add {nameof(AuthBehaviour)} to scene to be able to use auth logic");
        }

        /// <summary>
        /// Sign out user
        /// </summary>
        public void SignOut()
        {
            if (AuthBehaviour.Instance)
                AuthBehaviour.Instance.SignOut();
            else
                logger.Error($"No instance of {nameof(AuthBehaviour)} found. Please add {nameof(AuthBehaviour)} to scene to be able to use auth logic");
        }
    }
}