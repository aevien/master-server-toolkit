using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class DemoView : UIView
    {
        [SerializeField]
        [Tooltip("Duration in seconds before the loading-popup demo hides LoadingInfoView. Use a positive value to keep the popup visible; 0 requests an immediate timer callback.")]
        private float loadingPopupTime = 2f;

        public void OnClickShowBanner()
        {
            ViewsManager.Show<AdBannerView>( new AdBannerEventMessage()
            {
                Message = "Hello! I'm an advertisement banner!"
            });
        }

        public void OnClickShowYesNo()
        {
            ViewsManager.Show<OkDialogBoxView>(new YesNoDialogBoxEventMessage()
            {
                Message = "Hello! I'm an yes-no popup!"
            });
        }

        public void OnClickShowOk()
        {
            ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage()
            {
                Message = "Hello! I'm an ok popup!"
            });
        }

        public void OnClickShowLoading()
        {
            ViewsManager.Show<LoadingInfoView>($"Now loading, please wait {loadingPopupTime:F0} second(s)");

            MstTimer.WaitForSeconds(loadingPopupTime, () =>
            {
                ViewsManager.Hide<LoadingInfoView>();
            });
        }

        public void OnClickShowToast()
        {
            Mst.Client.Notifications.Notice("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua");
        }
    }
}
