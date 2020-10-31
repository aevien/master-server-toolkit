#if MIRROR
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.Bridges.Mirror
{
    public class MirrorRoomClientStarter : BaseClientBehaviour
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
                MirrorRoomClient.Instance.StartClient();
            });
        }
    }
}
#endif