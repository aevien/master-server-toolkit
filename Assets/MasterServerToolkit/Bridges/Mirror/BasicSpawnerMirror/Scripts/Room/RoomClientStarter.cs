#if MIRROR
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    [AddComponentMenu("Master Server Toolkit/Mirror/RoomClientStarter")]
    public class RoomClientStarter : BaseClientBehaviour
    {
        protected override void OnDestroy()
        {
            base.OnDestroy();
            Connection?.RemoveConnectionListener(OnConnectedToMasterServerEventHandler);
        }

        protected override void OnInitialize()
        {
            // This will start client connection to room server if it came from menu with global parameter AUTOSTART_ROOM_CLIENT
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