using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;

namespace MasterServerToolkit.Bridges
{
    public class YesNoDialogBoxView : PopupView
    {
        protected override void Awake()
        {
            base.Awake();

            Mst.Events.AddListener(MstEventKeys.showYesNoDialogBox, OnShowDialogBoxEventHandler);
            Mst.Events.AddListener(MstEventKeys.hideYesNoDialogBox, OnHideDialogBoxEventHandler);
        }

        private void OnShowDialogBoxEventHandler(EventMessage message)
        {
            var messageData = message.As<YesNoDialogBoxEventMessage>();

            SetLables(messageData.Message);

            SetButtonsClick(() =>
            {
                messageData.YesCallback?.Invoke();
                Hide();
            }, () =>
            {
                messageData.NoCallback?.Invoke();
                Hide();
            });

            Show();
        }

        private void OnHideDialogBoxEventHandler(EventMessage message)
        {
            Hide();
        }
    }
}
