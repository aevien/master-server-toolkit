using MasterServerToolkit.Json;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MasterServerToolkit.GameService
{
    public class VkGamesAnalyticsModule : BaseServiceModule, IAnalyticsModule
    {
        [DllImport("__Internal")] private static extern void MstAnalyticsEvent(string eventData);
        private readonly HashSet<string> singletonEvents = new();

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = true;
            IsReady = true;
        }

        public void AnalyticsEvent(MstJson eventData, bool singleton = false) => AnalyticsEvent(eventData?.ToString(), singleton);

        public void AnalyticsEvent(string eventData, bool singleton = false)
        {
            if (string.IsNullOrEmpty(eventData) || (singleton && !singletonEvents.Add(eventData)))
                return;
            MstAnalyticsEvent(eventData);
        }
    }
}
