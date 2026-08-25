using MasterServerToolkit.Bridges;
using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;

namespace MasterServerToolkit.Demos.BasicWorlds
{
    public class WorldView : UIView
    {
        protected override void Awake()
        {
            base.Awake();

            foreach (var button in GetComponentsInChildren<HideInSceneBehaviour>(true))
                button.gameObject.SetActive(true);
        }

        public void GoToZone(string zoneId)
        {
            logger.Info($"Going to zone {zoneId}");

            Mst.Connection.SendMessage(MstOpCodes.GetZoneRoomInfo, zoneId, (status, response) =>
            {
                if (status != Networking.ResponseStatus.Success)
                {
                    logger.Error(Mst.Errors.Parse(status, response));
                    return;
                }

                var game = response.AsPacket<GameInfoPacket>();
                logger.Info(game);

                Mst.Events.Invoke(MstEventKeys.goToZone, true);

                Mst.Client.Rooms.GetAccess(game.Id, (accessData, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        Mst.Events.Invoke(MstEventKeys.goToZone, false);
                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(error));
                        logger.Error(error);
                        return;
                    }
                });
            });
        }
    }
}
