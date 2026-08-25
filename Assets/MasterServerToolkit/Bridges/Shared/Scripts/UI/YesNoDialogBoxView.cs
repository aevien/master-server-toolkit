using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System;
using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class YesNoDialogBoxView : PopupView
    {
        protected override void OnStartShow()
        {
            base.OnStartShow();

            try
            {
                var messageData = Payload.As<YesNoDialogBoxEventMessage>();

                SetLabels(messageData.Message);

                SetButtonsClick(() =>
                {
                    messageData.YesCallback?.Invoke();
                    Hide();
                }, () =>
                {
                    messageData.NoCallback?.Invoke();
                    Hide();
                });

                SetMessageIcon((int)messageData.MessageType);
                Show();
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }

        private void OnShowEventHandler(EventPayload message)
        {
            
        }

        private void OnHideEventHandler(EventPayload message)
        {
            Hide();
        }

        private void SetMessageIcon(int index)
        {
            for (int i = 0; i < helpers.Length; i++)
                helpers[i].SetActive(i == index);
        }
    }

    public class YesNoDialogBoxEventMessage : DialogBoxEventMessage
    {
        public YesNoDialogBoxEventMessage() : base() { }

        public YesNoDialogBoxEventMessage(string message) : base(message)
        {
            NoCallback = null;
        }

        public YesNoDialogBoxEventMessage(string message, UnityAction yesCallback, UnityAction noCallback) : base(message)
        {
            YesCallback = yesCallback;
            NoCallback = noCallback;
        }

        public UnityAction YesCallback { get; set; }
        public UnityAction NoCallback { get; set; }
    }
}
