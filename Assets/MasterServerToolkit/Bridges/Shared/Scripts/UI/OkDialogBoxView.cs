using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class OkDialogBoxView : PopupView
    {
        protected override void OnStartShow()
        {
            base.OnStartShow();

            var messageData = Payload.As<OkDialogBoxEventMessage>();

            SetLabels(messageData.Message);

            SetButtonsClick(() =>
            {
                messageData.OkCallback?.Invoke();
                Hide();
            });

            SetMessageIcon((int)messageData.MessageType);
            Show();
        }

        private void SetMessageIcon(int index)
        {
            for (int i = 0; i < helpers.Length; i++)
                helpers[i].SetActive(i == index);
        }
    }

    public class OkDialogBoxEventMessage : DialogBoxEventMessage
    {
        public OkDialogBoxEventMessage() : base() { }

        public OkDialogBoxEventMessage(string message) : base(message)
        {
            OkCallback = null;
        }

        public OkDialogBoxEventMessage(string message, UnityAction okCallback) : base(message)
        {
            OkCallback = okCallback;
        }

        public UnityAction OkCallback { get; set; }
    }
}
