#if FISHNET

namespace MasterServerToolkit.Bridges.FishNetworking.Character
{
    public class PlayerCharacterProfile : PlayerCharacterBehaviour
    {
        //converted from mirror example. code was already commented out.

        //protected RoomPlayer roomPlayer;

        //public override void OnStartServer()
        //{
        //    base.OnStartServer();

        //    // Get room server player by mirror peer id
        //    roomPlayer = RoomServer.Instance.GetRoomPlayerByMirrorPeer(netIdentity.connectionToClient);

        //    if(roomPlayer == null)
        //    {
        //        Debug.LogError($"Player {netIdentity.connectionToClient.connectionId} could not get its room playerinfo");
        //        netIdentity.connectionToClient.Disconnect();
        //        return;
        //    }
        //}

        //protected virtual void OnDestroy() { }
    }
}
#endif