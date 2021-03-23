using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    [RequireComponent(typeof(UIView))]
    public class LoadingInfoView : PopupViewComponent
    {
        public override void OnOwnerAwake()
        {
            base.OnOwnerAwake();

            Mst.Events.AddEventListener(MstEventKeys.showLoadingInfo, OnShowLoadingInfoEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hideLoadingInfo, OnHideLoadingInfoEventHandler);
        }

        private void OnShowLoadingInfoEventHandler(EventMessage message)
        {
            SetLables(message.GetData<string>());
            Owner.Show();
        }

        private void OnHideLoadingInfoEventHandler(EventMessage message)
        {
            Owner.Hide();
        }
    }
}