using MasterServerToolkit.Json;
using System;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class DesktopAdvertisementModule : BaseAdvertisementModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = false;
            IsFullScreenVideoReady = false;
            IsReady = true;
        }
    }

    public class DesktopInAppPurchaseModule : BaseInAppPurchaseModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = false;
            IsReady = true;
        }
    }

    public class DesktopLeaderboardsModule : BaseLeaderboardsModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = false;
            IsReady = true;
        }
    }

    public class DesktopAnalyticsModule : BaseServiceModule, IAnalyticsModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public void AnalyticsEvent(MstJson eventData, bool singleton = false)
        {
            Logger.Debug($"{Service.Id} analytics event ignored: {eventData}");
        }

        public void AnalyticsEvent(string eventData, bool singleton = false)
        {
            Logger.Debug($"{Service.Id} analytics event ignored: {eventData}");
        }
    }

    public class DesktopStorageModule : BaseStorageModule
    {
        private const string StorageKeySuffix = "Storage";

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = true;
            LoadFromPlayerPrefs();
            IsReady = true;
            NotifyOnLoad();
        }

        public override void LoadData(StorageDataHandler callback)
        {
            base.LoadData(callback);
            LoadFromPlayerPrefs();
            NotifyOnLoad();
        }

        public override void SaveData(MstJson data, bool saveAsStats = false)
        {
            Data = data ?? MstJson.CreateObject();

            try
            {
                PlayerPrefs.SetString(StorageKey, Data.ToString());
                PlayerPrefs.Save();
                NotifyOnSave(true, string.Empty);
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to save {Service.Id} local storage: {exception}");
                NotifyOnSave(false, exception.Message);
            }
        }

        private void LoadFromPlayerPrefs()
        {
            try
            {
                string rawData = PlayerPrefs.GetString(StorageKey, string.Empty);
                bool hasValidData = !string.IsNullOrWhiteSpace(rawData) && MstJson.IsJson(rawData);
                Data = !hasValidData
                    ? MstJson.CreateObject()
                    : new MstJson(rawData);

                if (!string.IsNullOrWhiteSpace(rawData) && !hasValidData)
                    Logger.Warn($"Ignored invalid {Service.Id} local storage data");
            }
            catch (Exception exception)
            {
                Data = MstJson.CreateObject();
                Logger.Error($"Failed to load {Service.Id} local storage: {exception}");
            }
        }

        private string StorageKey
        {
            get
            {
                string serviceId = (Service?.Id ?? GameServiceId.Desktop).ToString().ToLowerInvariant();
                string playerId = Service?.Player?.Id;
                return string.IsNullOrWhiteSpace(playerId)
                    ? $"{serviceId}{StorageKeySuffix}"
                    : $"{serviceId}{StorageKeySuffix}:{playerId}";
            }
        }
    }

    public class DesktopShareModule : BaseServiceModule, IShareModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public void Review()
        {
            Review(null);
        }

        public void Review(ReviewHandler callback)
        {
            callback?.Invoke(new ReviewResultInfo
            {
                FeedbackSent = false,
                Error = "unsupported"
            });
        }

        public void CanReview(ReviewAvailabilityHandler callback)
        {
            callback?.Invoke(new ReviewAvailabilityInfo
            {
                CanReview = false,
                Reason = "unsupported"
            });
        }

        public void CanShowShortcutPrompt(ShortcutAvailabilityHandler callback)
        {
            callback?.Invoke(new ShortcutAvailabilityInfo
            {
                CanShow = false,
                Reason = "unsupported"
            });
        }

        public void ShowShortcutPrompt(ShortcutPromptHandler callback)
        {
            callback?.Invoke(new ShortcutPromptInfo
            {
                Outcome = string.Empty,
                Error = "unsupported"
            });
        }
    }
}
