using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class YandexGamesAdvertisementModule : BaseAdvertisementModule
    {
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ShowFullScreenAdv();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ShowRewardedVideo();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ShowStickyBanner();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_HideStickyBanner();

        protected Coroutine interstitialAdCoroutine;

        public override bool IsFullScreenVideoReady => interstitialAdCoroutine == null;

        public override void OnBeforeInit(IService service)
        {
            IsSupported = true;
            base.OnBeforeInit(service);
        }

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public override void ShowFullScreenVideo(FullScreenVideoHandler callback)
        {
            base.ShowFullScreenVideo(callback);

            if (IsFullScreenVideoReady)
                interstitialAdCoroutine = StartCoroutine(coroutine());

            IEnumerator coroutine()
            {
                Gb_Yg_ShowFullScreenAdv();
                yield return new WaitForSecondsRealtime(Service.Options.GetField(nameof(YandexGameSdkSettings.interstitialAdInterval)).FloatValue);
                interstitialAdCoroutine = null;
            }
        }

        public override void ShowRewardedVideo(RewardedVideoHandler callback)
        {
            base.ShowRewardedVideo(callback);
            Gb_Yg_ShowRewardedVideo();
        }

        public override void ShowBanner(BannerAdvertisementHandler callback)
        {
            base.ShowBanner(callback);
            Gb_Yg_ShowStickyBanner();
        }

        public override void HideBanner(BannerAdvertisementHandler callback)
        {
            base.HideBanner(callback);
            Gb_Yg_HideStickyBanner();
        }

        #region WEB_CALLBACKS

        protected void Yg_OnFullScreenVideoStatus(string status)
        {
            NotifyOnFullScreenVideoStatus(Enum.TryParse<FullScreenVideoStatus>(status, true, out var result)
                ? result
                : FullScreenVideoStatus.Error);
        }

        protected void Yg_OnRewardedVideoStatus(string status)
        {
            NotifyOnRewardedVideoStatus(Enum.TryParse<RewardedVideoStatus>(status, true, out var result)
                ? result
                : RewardedVideoStatus.Error);
        }

        protected void Yg_OnBannerAdvertisementStatus(string status)
        {
            NotifyOnBannerAdvertisementStatus(Enum.TryParse<BannerAdvertisementStatus>(status, true, out var result)
                ? result
                : BannerAdvertisementStatus.Error);
        }

        #endregion
    }
}
