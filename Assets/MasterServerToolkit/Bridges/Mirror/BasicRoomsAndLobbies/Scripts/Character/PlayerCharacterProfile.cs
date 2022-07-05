#if MIRROR

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    public class PlayerCharacterProfile : PlayerCharacterBehaviour
    {
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