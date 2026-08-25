using MasterServerToolkit.Bridges;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;

namespace MasterServerToolkit.GameService
{
    public class EditorAdvertisementModule : BaseServiceModule, IAdvertisementModule
    {
        private bool interstitialIsVisible = false;
        private bool rewardedIsVisible = false;
        private bool bannerIsVisible = false;

        private FullScreenVideoHandler fullScreenVideoHandler;
        private RewardedVideoHandler rewardedVideoHandler;
        private BannerAdvertisementHandler bannerAdvertisementHandler;

        public bool IsVisible => interstitialIsVisible || rewardedIsVisible || bannerIsVisible;
        public override bool IsSupported { get; protected set; } = true;
        public bool IsFullScreenVideoReady => true;
        public bool IsBannerVisible => bannerIsVisible;

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public void ShowFullScreenVideo(FullScreenVideoHandler callback)
        {
            fullScreenVideoHandler = callback;
            interstitialIsVisible = true;
            fullScreenVideoHandler?.Invoke(FullScreenVideoStatus.Opened);

            Service.Logger.Debug("ShowFullScreenVideo");

            if (ViewsManager.TryGetView<AdBannerView>(out var banner))
            {
                ViewsManager.Show<AdBannerView>(new AdBannerEventMessage()
                {
                    Message = "I'm an interstitial ad banner!",
                    SuccessCallback = () =>
                    {
                        Service.Logger.Debug("HideFullScreenVideo");
                        interstitialIsVisible = false;
                        fullScreenVideoHandler?.Invoke(FullScreenVideoStatus.Closed);
                    },
                    FailedCallback = () =>
                    {
                        interstitialIsVisible = false;
                        fullScreenVideoHandler?.Invoke(FullScreenVideoStatus.Error);
                    }
                });
            }
            else
            {
                MstTimer.WaitForSeconds(1f, () =>
                {
                    Service.Logger.Debug("HideFullScreenVideo");
                    interstitialIsVisible = false;
                    fullScreenVideoHandler?.Invoke(FullScreenVideoStatus.Closed);
                });
            }
        }

        public void ShowRewardedVideo(RewardedVideoHandler callback)
        {
            rewardedVideoHandler = callback;
            rewardedIsVisible = true;
            rewardedVideoHandler?.Invoke(RewardedVideoStatus.Opened);

            Service.Logger.Debug("ShowRewardedVideo");

            if (ViewsManager.TryGetView<AdBannerView>(out var banner))
            {
                ViewsManager.Show<AdBannerView>(new AdBannerEventMessage()
                {
                    Message = "I'm a rewarded video ad banner!",
                    SuccessCallback = () =>
                    {
                        Service.Logger.Debug("HideRewardedVideo");
                        rewardedIsVisible = false;
                        rewardedVideoHandler?.Invoke(RewardedVideoStatus.Rewarded);
                    },
                    FailedCallback = () =>
                    {
                        Service.Logger.Debug("HideRewardedVideo");
                        rewardedIsVisible = false;
                        rewardedVideoHandler?.Invoke(RewardedVideoStatus.Error);
                    }
                });
            }
            else
            {
                MstTimer.WaitForSeconds(1f, () =>
                {
                    Service.Logger.Debug("HideRewardedVideo");
                    rewardedIsVisible = false;
                    rewardedVideoHandler?.Invoke(RewardedVideoStatus.Rewarded);
                });
            }
        }

        public void ShowBanner(BannerAdvertisementHandler callback)
        {
            bannerAdvertisementHandler = callback;
            bannerIsVisible = true;
            bannerAdvertisementHandler?.Invoke(BannerAdvertisementStatus.Shown);

            Service.Logger.Debug("ShowBanner");
        }

        public void HideBanner(BannerAdvertisementHandler callback)
        {
            bannerAdvertisementHandler = callback;
            bannerIsVisible = false;
            bannerAdvertisementHandler?.Invoke(BannerAdvertisementStatus.Hidden);

            Service.Logger.Debug("HideBanner");
        }
    }
}
