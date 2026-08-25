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
        [Tooltip("Required password input. Submit stores its current text as the MST room password before invoking the dialog callback.")]
        private TMP_InputField passwordInputField;

        #endregion

        private UnityAction submitCallback;

        protected override void OnStartShow()
        {
            base.OnStartShow();
            var messageData = Payload.As<PasswordInputDialoxBoxEventMessage>();
            SetLabels(messageData.Message);
            submitCallback = messageData.OkCallback;
        }

        public void Submit()
        {
            Mst.Options.Set(Mst.Args.Names.RoomPassword, passwordInputField.text);
            submitCallback?.Invoke();
            Hide();
        }
    }

    public class PasswordInputDialoxBoxEventMessage
    {
        public PasswordInputDialoxBoxEventMessage() { }

        public PasswordInputDialoxBoxEventMessage(string message)
        {
            Message = message;
            OkCallback = null;
        }

        public PasswordInputDialoxBoxEventMessage(string message, UnityAction submitCallback)
        {
            Message = message;
            OkCallback = submitCallback;
        }

        public string Message { get; set; }
        public UnityAction OkCallback { get; set; }
    }
}
