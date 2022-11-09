using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class PasswordInputDialogBoxView : PopupView
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private TMP_InputField passwordInputField;

        #endregion

        private UnityAction submitCallback;

        protected override void Awake()
        {
            base.Awake();

            Mst.Events.AddListener(MstEventKeys.showPasswordDialogBox, OnShowPasswordDialogBoxEventHandler);
            Mst.Events.AddListener(MstEventKeys.hidePasswordDialogBox, OnHidePasswordDialogBoxEventHandler);
        }

        private void OnHidePasswordDialogBoxEventHandler(EventMessage message)
        {
            Hide();
        }

        private void OnShowPasswordDialogBoxEventHandler(EventMessage message)
        {
            var messageData = message.As<PasswordInputDialoxBoxEventMessage>();

            SetLables(messageData.Message);
            submitCallback = messageData.OkCallback;

            Show();
        }

        public void Submit()
        {
            Mst.Options.Set(Mst.Args.Names.RoomPassword, passwordInputField.text);
            submitCallback?.Invoke();
            Hide();
        }
    }
}