using MasterServerToolkit.Bridges;
using MasterServerToolkit.Bridges.MirrorNetworking.Character;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using UnityEngine;

namespace MasterServerToolkit.Demos.BasicRoomsAndLobbies
{
    public class RoomHudView : UIView
    {
        private void Update()
        {
            if (PlayerCharacter.Local != null)
            {
                Show();

                if (Input.GetKeyDown(KeyCode.Escape))
                    ShowPlayersList();
            }
            else
            {
                Hide();
            }
        }

        public void ShowPlayersList()
        {
            if (Mst.Client.Rooms.HasAccess)
                ViewsManager.Show<PlayersListView>(Mst.Client.Rooms.ReceivedAccess.Id);
        }
    }
}