using MasterServerToolkit.UI;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomHudView : UIView
    {
        public void Disconnect()
        {
#if MIRROR
            RoomClientManager.Disconnect();
#endif
        }
    }
}
