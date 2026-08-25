using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class EmailConfirmationView : UIView
    {
        [Header("Components"), SerializeField]
        [Tooltip("Input field containing the email confirmation code sent to the signed-in account.")]
        private TMP_InputField confirmationCodeInputField;

        private IDisposable showEmailConfirmationListener;
        private IDisposable hideEmailConfirmationListener;

        public string ConfirmationCode
        {
            get
            {
                return confirmationCodeInputField != null ? confirmationCodeInputField.text : string.Empty;
            }
        }

        protected void Start()
        {
            // Listen to show/hide events
            showEmailConfirmationListener?.Dispose();
            showEmailConfirmationListener = Mst.Events.AddListener(MstEventKeys.showEmailConfirmationView, OnShowEmailConfirmationEventHandler);

            hideEmailConfirmationListener?.Dispose();
            hideEmailConfirmationListener = Mst.Events.AddListener(MstEventKeys.hideEmailConfirmationView, OnHideEmailConfirmationEventHandler);
        }

        protected override void OnDestroy()
        {
            showEmailConfirmationListener?.Dispose();
            showEmailConfirmationListener = null;

            hideEmailConfirmationListener?.Dispose();
            hideEmailConfirmationListener = null;

            base.OnDestroy();
        }

        private void OnShowEmailConfirmationEventHandler(EventPayload message)
        {
            Show();
        }

        private void OnHideEmailConfirmationEventHandler(EventPayload message)
        {
            Hide();
        }

        /// <summary>
        /// Sends request to get confirmation code
        /// </summary>
        public void RequestConfirmationCode()
        {
            ViewsManager.Show<LoadingInfoView>(Mst.Localization["ui.loading.accountConfirmation.sendCode.message"]);

            Logger.Debug(Mst.Localization["ui.loading.accountConfirmation.sendCode.message"]);

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.RequestEmailConfirmationCode((isSuccessful, error) =>
                {
                    ViewsManager.Hide<LoadingInfoView>();

                    if (isSuccessful)
                    {
                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage($"{Mst.Localization["ui.notification.accountConfirmation.sendCode.success.message"]} '{Mst.Client.Auth.Account.Email}'", null));
                    }
                    else
                    {
                        string outputMessage = $"{Mst.Localization["ui.notification.accountConfirmation.sendCode.error.message"]}: {error}";
                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(outputMessage, null));
                        Logger.Error(outputMessage);
                    }
                });
            });
        }

        /// <summary>
        /// Sends request to confirm account with confirmation code
        /// </summary>
        public void ConfirmAccount()
        {
            ViewsManager.Show<LoadingInfoView>(Mst.Localization["ui.loading.accountConfirmation.message"]);

            Logger.Debug(Mst.Localization["ui.loading.accountConfirmation.message"]);

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.ConfirmEmail(ConfirmationCode, (isSuccessful, error) =>
                {
                    ViewsManager.Hide<LoadingInfoView>();

                    if (isSuccessful)
                    {
                        Mst.Events.Invoke(MstEventKeys.hideEmailConfirmationView);
                        ViewsManager.Show<MainMenuView>();
                    }
                    else
                    {
                        string outputMessage = $"{Mst.Localization["ui.notification.accountConfirmation.error.message"]} {error}";
                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(outputMessage, null));
                        Logger.Error(outputMessage);
                    }
                });
            });
        }

        /// <summary>
        /// Sign out user
        /// </summary>
        public void SignOut()
        {
            Logger.Debug("Sign out");
            Mst.Client.Auth.SignOut();

            ViewsManager.HideAllViews();
            ViewsManager.Show<SignInView>();
        }
    }
}
