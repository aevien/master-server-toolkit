using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    [RequireComponent(typeof(UIView))]
    public class OkDialogBoxView : PopupViewComponent
    {
        public override void OnOwnerStart()
        {
            Mst.Events.AddEventListener(MstEventKeys.showOkDialogBox, OnShowDialogBoxEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hideOkDialogBox, OnHideDialogBoxEventHandler);
        }

        private void OnShowDialogBoxEventHandler(EventMessage message)
        {
            var messageData = message.GetData<OkDialogBoxEventMessage>();

            SetLables(messageData.Message);

            SetButtonsClick(() =>
            {
                messageData.OkCallback?.Invoke();
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
