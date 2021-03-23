using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Games
{
    [RequireComponent(typeof(UIView))]
    public class PasswordInputDialogBoxView : PopupViewComponent
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private TMP_InputField passwordInputField;

        #endregion

        private UnityAction submitCallback;

        public override void OnOwnerStart()
        {
            passwordInputField = Owner.ChildComponent<TMP_InputField>("passwordInputField");
            Mst.Events.AddEventListener(MstEventKeys.showPasswordDialogBox, OnShowPasswordDialogBoxEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hidePasswordDialogBox, OnHidePasswordDialogBoxEventHandler);
        }

        private void OnHidePasswordDialogBoxEventHandler(EventMessage message)
        {
            Owner.Hide();
        }

        private void OnShowPasswordDialogBoxEventHandler(EventMessage message)
        {
            var messageData = message.GetData<PasswordInputDialoxBoxEventMessage>();

            SetLables(messageData.Message);
            submitCallback = messageData.OkCallback;

            Owner.Show();
        }

        public void Submit()
        {
            Mst.Options.Set(MstDictKeys.ROOM_PASSWORD, passwordInputField.text);
            submitCallback?.Invoke();
            Owner.Hide();
        }
    }
}