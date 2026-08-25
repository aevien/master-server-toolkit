using MasterServerToolkit.Json;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MasterServerToolkit.GameService
{
    public class YandexGamesAnalyticsModule : BaseServiceModule, IAnalyticsModule
    {
        [DllImport("__Internal")]
        private static extern void MstAnalyticsEvent(string eventData);

        private readonly HashSet<string> analyticsSingltons = new();

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = true;
            IsReady = true;
        }

        public virtual void AnalyticsEvent(MstJson eventData, bool singleton = false)
        {
            AnalyticsEvent(eventData.ToString(), singleton);
        }

        public virtual void AnalyticsEvent(string eventData, bool singleton = false)
        {
            if (string.IsNullOrEmpty(eventData))
                return;

            if (singleton)
            {
                if (analyticsSingltons.Contains(eventData))
                {
                    return;
                }
                else
                {
                    analyticsSingltons.Add(eventData);
                }
            }

            MstAnalyticsEvent(eventData);
        }
    }
}
