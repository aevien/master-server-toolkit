namespace MasterServerToolkit.GameService
{
    public class VkPlayInAppPurchaseModule : BaseInAppPurchaseModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = false;
            IsReady = true;
        }
    }
}
