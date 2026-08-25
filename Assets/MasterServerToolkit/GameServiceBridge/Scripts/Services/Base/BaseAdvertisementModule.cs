namespace MasterServerToolkit.GameService
{
    public abstract class BaseAdvertisementModule : BaseServiceModule, IAdvertisementModule
    {
        public bool IsVisible { get; protected set; }
        public override bool IsSupported { get; protected set; }
        public virtual bool IsFullScreenVideoReady { get; protected set; }
        public virtual bool IsBannerVisible { get; protected set; }

        protected FullScreenVideoHandler fullScreenVideoCallback;
        protected RewardedVideoHandler rewardedVideoCallback;
        protected BannerAdvertisementHandler bannerAdvertisementCallback;

        public virtual void ShowFullScreenVideo(FullScreenVideoHandler callback)
        {
            if (IsSupported && IsFullScreenVideoReady)
            {
                IsVisible = true;
                fullScreenVideoCallback = callback;
            }
            else
            {
                IsVisible = false;
                callback?.Invoke(FullScreenVideoStatus.Error);
            }
        }

        public virtual void ShowRewardedVideo(RewardedVideoHandler callback)
        {
            if (IsSupported)
            {
                IsVisible = true;
                rewardedVideoCallback = callback;
            }
            else
            {
                IsVisible = false;
                callback?.Invoke(RewardedVideoStatus.Error);
            }
        }

        public virtual void ShowBanner(BannerAdvertisementHandler callback)
        {
            if (IsSupported)
            {
                bannerAdvertisementCallback = callback;
            }
            else
            {
                IsBannerVisible = false;
                callback?.Invoke(BannerAdvertisementStatus.Error);
            }
        }

        public virtual void HideBanner(BannerAdvertisementHandler callback)
        {
            if (IsSupported)
            {
                bannerAdvertisementCallback = callback;
            }
            else
            {
                IsBannerVisible = false;
                callback?.Invoke(BannerAdvertisementStatus.Error);
            }
        }

        protected void NotifyOnFullScreenVideoStatus(FullScreenVideoStatus status)
        {
            fullScreenVideoCallback?.Invoke(status);

            switch (status)
            {
                case FullScreenVideoStatus.Closed:
                case FullScreenVideoStatus.Error:
                    fullScreenVideoCallback = null;

                    if (rewardedVideoCallback == null)
                    {
                        IsVisible = IsBannerVisible;
                    }

                    break;
            }
        }

        protected void NotifyOnRewardedVideoStatus(RewardedVideoStatus status)
        {
            rewardedVideoCallback?.Invoke(status);

            switch (status)
            {
                case RewardedVideoStatus.Rewarded:
                case RewardedVideoStatus.Closed:
                case RewardedVideoStatus.Error:
                    rewardedVideoCallback = null;

                    if (fullScreenVideoCallback == null)
                    {
                        IsVisible = IsBannerVisible;
                    }

                    break;
            }
        }

        protected void NotifyOnBannerAdvertisementStatus(BannerAdvertisementStatus status)
        {
            bannerAdvertisementCallback?.Invoke(status);

            switch (status)
            {
                case BannerAdvertisementStatus.Shown:
                    IsVisible = true;
                    IsBannerVisible = true;
                    break;

                case BannerAdvertisementStatus.Hidden:
                case BannerAdvertisementStatus.Error:
                    IsBannerVisible = false;
                    bannerAdvertisementCallback = null;

                    if (fullScreenVideoCallback == null && rewardedVideoCallback == null)
                    {
                        IsVisible = false;
                    }

                    break;
            }
        }
    }
}
