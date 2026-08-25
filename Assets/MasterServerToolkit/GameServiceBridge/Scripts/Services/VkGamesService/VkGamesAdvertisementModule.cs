using MasterServerToolkit.Json;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class VkGamesAdvertisementModule : BaseAdvertisementModule
    {
        private const float AvailabilityRetryDelaySeconds = 30f;

        [DllImport("__Internal")] private static extern int Gb_Vk_IsReady();
        [DllImport("__Internal")] private static extern void Gb_Vk_CheckInterstitial();
        [DllImport("__Internal")] private static extern void Gb_Vk_ShowInterstitial();
        [DllImport("__Internal")] private static extern void Gb_Vk_ShowRewarded();
        [DllImport("__Internal")] private static extern void Gb_Vk_ShowBanner();
        [DllImport("__Internal")] private static extern void Gb_Vk_HideBanner();

        private Coroutine cooldownRoutine;
        private Coroutine initRoutine;
        private Coroutine availabilityRetryRoutine;
        private bool isBannerHidePending;
        private bool isBannerShowPending;
        private bool isAvailabilityCheckPending;
        private bool isInterstitialAvailable;

        public override bool IsFullScreenVideoReady =>
            !IsVisible && cooldownRoutine == null && isInterstitialAvailable;

        public override void OnBeforeInit(IService service)
        {
            IsSupported = true;
            base.OnBeforeInit(service);
        }

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            Service.OnReadyEvent += Service_OnReadyEvent;
            Service.OnPauseEvent += Service_OnPauseEvent;
            IsReady = true;
            initRoutine ??= StartCoroutine(InitializeAvailabilityCoroutine());
        }

        public override void ShowFullScreenVideo(FullScreenVideoHandler callback)
        {
            bool wasReady = IsFullScreenVideoReady;
            base.ShowFullScreenVideo(callback);
            if (!wasReady)
                return;

            isInterstitialAvailable = false;
            Gb_Vk_ShowInterstitial();
        }

        public override void ShowRewardedVideo(RewardedVideoHandler callback)
        {
            if (IsVisible)
            {
                callback?.Invoke(RewardedVideoStatus.Error);
                return;
            }

            base.ShowRewardedVideo(callback);
            Gb_Vk_ShowRewarded();
        }

        public override void ShowBanner(BannerAdvertisementHandler callback)
        {
            if (isBannerShowPending || isBannerHidePending)
            {
                callback?.Invoke(BannerAdvertisementStatus.Error);
                return;
            }

            if (IsBannerVisible)
            {
                callback?.Invoke(BannerAdvertisementStatus.Shown);
                return;
            }

            if (IsVisible)
            {
                callback?.Invoke(BannerAdvertisementStatus.Error);
                return;
            }

            isBannerShowPending = true;
            base.ShowBanner(callback);
            Gb_Vk_ShowBanner();
        }

        public override void HideBanner(BannerAdvertisementHandler callback)
        {
            if (isBannerShowPending || isBannerHidePending)
            {
                callback?.Invoke(BannerAdvertisementStatus.Error);
                return;
            }

            if (!IsBannerVisible)
            {
                callback?.Invoke(BannerAdvertisementStatus.Hidden);
                return;
            }

            isBannerHidePending = true;
            base.HideBanner(callback);
            Gb_Vk_HideBanner();
        }

        private void Service_OnReadyEvent(bool isReady)
        {
            if (isReady)
                RefreshInterstitialAvailability();
        }

        private IEnumerator InitializeAvailabilityCoroutine()
        {
            yield return null;
            float elapsed = 0f;

            while (Gb_Vk_IsReady() != 1 && elapsed < Service.WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            initRoutine = null;
            if (Gb_Vk_IsReady() == 1)
                RefreshInterstitialAvailability();
        }

        private void Service_OnPauseEvent(bool isPaused)
        {
            if (!isPaused)
                RefreshInterstitialAvailability();
        }

        private void RefreshInterstitialAvailability()
        {
            if (isAvailabilityCheckPending || cooldownRoutine != null || IsVisible)
                return;

            isInterstitialAvailable = false;
            isAvailabilityCheckPending = true;
            Gb_Vk_CheckInterstitial();
        }

        private void ScheduleAvailabilityRefresh()
        {
            if (availabilityRetryRoutine == null && cooldownRoutine == null)
                availabilityRetryRoutine = StartCoroutine(AvailabilityRetryCoroutine());
        }

        private IEnumerator AvailabilityRetryCoroutine()
        {
            yield return new WaitForSecondsRealtime(AvailabilityRetryDelaySeconds);
            availabilityRetryRoutine = null;
            RefreshInterstitialAvailability();
        }

        private void StartCooldown()
        {
            if (cooldownRoutine == null)
                cooldownRoutine = StartCoroutine(CooldownCoroutine());
        }

        private IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSecondsRealtime(
                Service.Options[nameof(VkGamesSdkSettings.interstitialAdInterval)].FloatValue);
            cooldownRoutine = null;
            RefreshInterstitialAvailability();
        }

        protected void Vk_OnInterstitialAvailability(string json)
        {
            var response = MstJson.IsJson(json) ? new MstJson(json) : MstJson.CreateObject();

            isAvailabilityCheckPending = false;
            isInterstitialAvailable = response.HasField("available") && response["available"].BoolValue;

            if (isInterstitialAvailable && availabilityRetryRoutine != null)
            {
                StopCoroutine(availabilityRetryRoutine);
                availabilityRetryRoutine = null;
            }
            else if (!isInterstitialAvailable)
            {
                ScheduleAvailabilityRefresh();
            }
        }

        protected void Vk_OnFullScreenVideoStatus(string status)
        {
            FullScreenVideoStatus value = Enum.TryParse(status, true, out FullScreenVideoStatus parsed)
                ? parsed
                : FullScreenVideoStatus.Error;

            if (value == FullScreenVideoStatus.Opened)
            {
                isInterstitialAvailable = false;
            }
            else if (value == FullScreenVideoStatus.Closed)
            {
                StartCooldown();
            }
            else if (value == FullScreenVideoStatus.Error)
            {
                ScheduleAvailabilityRefresh();
            }

            NotifyOnFullScreenVideoStatus(value);
        }

        protected void Vk_OnRewardedVideoStatus(string status) =>
            NotifyOnRewardedVideoStatus(Enum.TryParse(status, true, out RewardedVideoStatus value) ? value : RewardedVideoStatus.Error);

        protected void Vk_OnBannerAdvertisementStatus(string status)
        {
            isBannerShowPending = false;
            isBannerHidePending = false;
            NotifyOnBannerAdvertisementStatus(
                Enum.TryParse(status, true, out BannerAdvertisementStatus value)
                    ? value
                    : BannerAdvertisementStatus.Error);
        }

        private void OnDestroy()
        {
            if (Service == null)
                return;

            Service.OnReadyEvent -= Service_OnReadyEvent;
            Service.OnPauseEvent -= Service_OnPauseEvent;
        }
    }
}
