namespace MasterServerToolkit.GameService
{
    public class VkPlayAdvertisementModule : BaseAdvertisementModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = false;
            IsFullScreenVideoReady = false;
            IsReady = true;
        }
    }
}
