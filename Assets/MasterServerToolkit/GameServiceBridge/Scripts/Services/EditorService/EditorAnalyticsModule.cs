using MasterServerToolkit.Json;

namespace MasterServerToolkit.GameService
{
    public class EditorAnalyticsModule : BaseServiceModule, IAnalyticsModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public void AnalyticsEvent(MstJson eventData, bool singleton = false) { }

        public void AnalyticsEvent(string eventData, bool singleton = false) { }
    }
}
