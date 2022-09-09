using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;

namespace MasterServerToolkit.Examples.BasicRoomsAndLobbies
{
    public class RoomHudView : UIView
    {
        public void Disconnect()
        {
            Mst.Events.Invoke(MstEventKeys.leaveRoom);
        }

        public void ShowPlayersList()
        {
            if (Mst.Client.Rooms.HasAccess)
                Mst.Events.Invoke(MstEventKeys.showPlayersListView, Mst.Client.Rooms.ReceivedAccess.RoomId);
        }
    }
}