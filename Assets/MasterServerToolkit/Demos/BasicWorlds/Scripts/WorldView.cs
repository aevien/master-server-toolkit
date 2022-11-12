using MasterServerToolkit.Bridges;
using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;

namespace MasterServerToolkit.Examples.BasicWorlds
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
            logger.Info($"Going to zone {zoneId}".ToGreen());

            Mst.Connection.SendMessage(MstOpCodes.GetZoneRoomInfo, zoneId, (status, response) =>
            {
                if (status != Networking.ResponseStatus.Success)
                {
                    logger.Error(response.AsString(status.ToString()));
                    return;
                }

                var game = response.AsPacket(new GameInfoPacket());
                logger.Info(game);

                Mst.Events.Invoke(MstEventKeys.goToZone, true);

                Mst.Client.Rooms.GetAccess(game.Id, (accessData, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        Mst.Events.Invoke(MstEventKeys.goToZone, false);
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error));
                        logger.Error(error);
                        return;
                    }
                });
            });
        }
    }
}