#if FISHNET
using MasterServerToolkit.UI;

namespace MasterServerToolkit.Bridges.FishNetworking
{
    public class RoomHudView : UIView
    {
        public void Disconnect()
        {
            RoomClientManager.Disconnect();
        }
    }
}
#endif