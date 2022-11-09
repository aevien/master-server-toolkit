using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;

namespace MasterServerToolkit.Bridges
{
    public class LoadingInfoView : PopupView
    {
        protected override void Awake()
        {
            base.Awake();

            Mst.Events.AddListener(MstEventKeys.showLoadingInfo, OnShowLoadingInfoEventHandler);
            Mst.Events.AddListener(MstEventKeys.hideLoadingInfo, OnHideLoadingInfoEventHandler);
        }

        private void OnShowLoadingInfoEventHandler(EventMessage message)
        {
            SetLables(message.As<string>());
            Show();
        }

        private void OnHideLoadingInfoEventHandler(EventMessage message)
        {
            Hide();
        }
    }
}