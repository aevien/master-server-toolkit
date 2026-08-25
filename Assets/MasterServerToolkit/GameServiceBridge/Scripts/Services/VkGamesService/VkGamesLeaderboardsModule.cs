namespace MasterServerToolkit.GameService
{
    public class VkGamesLeaderboardsModule : BaseLeaderboardsModule
    {
        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = false;
            IsReady = true;
        }
    }
}
