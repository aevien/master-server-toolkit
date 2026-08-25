using MasterServerToolkit.Json;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class DesktopService : BaseService
    {
        protected virtual GameServiceId ServiceId => GameServiceId.Desktop;
        protected virtual string DefaultLang => "en";

        public override long ServerTime => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public override ServiceDeviceType Device =>
            Application.isMobilePlatform ? ServiceDeviceType.Mobile : ServiceDeviceType.Desktop;

        public override void OnBeforeInit(MstJson options)
        {
            Id = ServiceId;
            AppId = $"{Application.identifier}";
            Lang = ResolveLanguage();

            Player = AddModule<DesktopPlayerModule>();
            IAP = AddModule<DesktopInAppPurchaseModule>();
            Leaderboards = AddModule<DesktopLeaderboardsModule>();
            Ad = AddModule<DesktopAdvertisementModule>();
            Analytics = AddModule<DesktopAnalyticsModule>();
            Storage = AddModule<DesktopStorageModule>();
            Share = AddModule<DesktopShareModule>();

            base.OnBeforeInit(options);
        }

        private string ResolveLanguage()
        {
            if (ServiceId != GameServiceId.Web)
                return DefaultLang;

            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                    return "ru";
                case SystemLanguage.Turkish:
                    return "tr";
                default:
                    return DefaultLang;
            }
        }
    }

    public class WebService : DesktopService
    {
        protected override GameServiceId ServiceId => GameServiceId.Web;
    }
}
