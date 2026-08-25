using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class PasswordResetView : UIView
    {
        [Header("Components"), SerializeField]
        [Tooltip("Input field containing the password reset code received by the user.")]
        private TMP_InputField resetCodeInputField;
        [SerializeField]
        [Tooltip("Input field containing the new password sent to the authentication module.")]
        private TMP_InputField newPasswordInputField;
        [SerializeField]
        [Tooltip("Confirmation input for the new password. The current view exposes this value but does not validate it before sending the reset request.")]
        private TMP_InputField newPasswordConfirmInputField;

        private IDisposable showPasswordResetListener;
        private IDisposable hidePasswordResetListener;

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
            showPasswordResetListener?.Dispose();
            showPasswordResetListener = Mst.Events.AddListener(MstEventKeys.showPasswordResetView, OnShowPasswordResetEventHandler);

            hidePasswordResetListener?.Dispose();
            hidePasswordResetListener = Mst.Events.AddListener(MstEventKeys.hidePasswordResetView, OnHidePasswordResetEventHandler);
        }

        protected override void OnDestroy()
        {
            showPasswordResetListener?.Dispose();
            showPasswordResetListener = null;

            hidePasswordResetListener?.Dispose();
            hidePasswordResetListener = null;

            base.OnDestroy();
        }

        private void OnShowPasswordResetEventHandler(EventPayload message)
        {
            Show();
        }

        private void OnHidePasswordResetEventHandler(EventPayload message)
        {
            Hide();
        }

        /// <summary>
        /// Send request to master server to change password
        /// </summary>
        public void ResetPassword()
        {
            if (!Mst.Options.Has(MstParamKeys.RESET_PASSWORD_EMAIL)) throw new Exception("You have no reset email");

            ViewsManager.Show<LoadingInfoView>(Mst.Localization["ui.loading.passwordChange.message"]);

            Logger.Debug(Mst.Localization["ui.loading.passwordChange.message"]);

            MstTimer.WaitForSeconds(0.1f, () =>
            {
                Mst.Client.Auth.ChangePassword(Mst.Options.AsString(MstParamKeys.RESET_PASSWORD_EMAIL), ResetCode, NewPassword, (isSuccessful, error) =>
                {
                    ViewsManager.Hide<LoadingInfoView>();

                    if (isSuccessful)
                    {
                        ViewsManager.Hide<PasswordResetView>();
                        ViewsManager.Show<SignUpView>();
                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(Mst.Localization["ui.loading.passwordChange.message"], null));
                    }
                    else
                    {
                        string outputMessage = $"{Mst.Localization["ui.notification.passwordChange.error.message"]} {error}";
                        Logger.Error(outputMessage);

                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(outputMessage, () =>
                        {
                            Mst.Events.Invoke(MstEventKeys.showPasswordResetView);
                        }));
                    }
                });
            });
        }

        /// <summary>
        /// Shows sing in view by sending event
        /// </summary>
        public void ShowSignInView()
        {
            ViewsManager.Show<SignUpView>();
        }
    }
}
