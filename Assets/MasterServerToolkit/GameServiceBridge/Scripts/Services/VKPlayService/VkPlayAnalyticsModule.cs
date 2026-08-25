using MasterServerToolkit.Json;

namespace MasterServerToolkit.GameService
{
    public class VkPlayAnalyticsModule : BaseServiceModule, IAnalyticsModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public void AnalyticsEvent(MstJson eventData, bool singleton = false)
        {
            Logger.Debug($"VK Play analytics event: {eventData}");
        }

        public void AnalyticsEvent(string eventData, bool singleton = false)
        {
            Logger.Debug($"VK Play analytics event: {eventData}");
        }
    }
}
