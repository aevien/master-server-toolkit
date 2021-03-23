using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    [RequireComponent(typeof(UIView))]
    public class YesNoDialogBoxView : PopupViewComponent
    {
        public override void OnOwnerStart()
        {
            Mst.Events.AddEventListener(MstEventKeys.showYesNoDialogBox, OnShowDialogBoxEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hideYesNoDialogBox, OnHideDialogBoxEventHandler);
        }

        private void OnShowDialogBoxEventHandler(EventMessage message)
        {
            var messageData = message.GetData<YesNoDialogBoxEventMessage>();

            SetLables(messageData.Message);

            SetButtonsClick(() =>
            {
                messageData.YesCallback?.Invoke();
                Owner.Hide();
            }, () =>
            {
                messageData.NoCallback?.Invoke();
                Owner.Hide();
            });

            Owner.Show();
        }

        private void OnHideDialogBoxEventHandler(EventMessage message)
        {
            Owner.Hide();
        }
    }
}
