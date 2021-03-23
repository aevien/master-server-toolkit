#if MIRROR
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.Bridges.MirrorNetworkingOld
{
    public class RoomClientStarter : BaseClientBehaviour
    {
        protected override void OnDestroy()
        {
            base.OnDestroy();
            Connection?.RemoveConnectionListener(OnConnectedToMasterServerEventHandler);
        }

        protected override void OnInitialize()
        {
            if (Mst.Options.Has(MstDictKeys.AUTOSTART_ROOM_CLIENT))
            {
                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Connecting to room... Please wait!");
                Connection.AddConnectionListener(OnConnectedToMasterServerEventHandler);
            }
        }

        protected virtual void OnConnectedToMasterServerEventHandler()
        {
            MstTimer.WaitForEndOfFrame(() =>
            {
                RoomClient.Instance.StartClient();
            });
        }
    }
}
#endif