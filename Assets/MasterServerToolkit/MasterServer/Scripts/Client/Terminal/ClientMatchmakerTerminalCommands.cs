using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using CommandTerminal;
using System;
using System.Collections.Generic;
using UnityEngine;
using MsfLogger = MasterServerToolkit.Logging.Logger;

namespace MasterServerToolkit.Client.Utilities
{
    public class ClientMatchmakerTerminalCommands
    {
        [RegisterCommand(Name = "cl.match.games", Help = "Get list of game from matchmaking module")]
        private void GetMatchGamesListCmd(CommandArg[] args)
        {
            Mst.Client.Matchmaker.FindGames((games) =>
            {
                if (games.Count > 0)
                {
                    foreach(var game in games)
                    {
                        Logs.Info(game);
                    }
                }
                else
                {
                    Logs.Info("No games found");
                }
            });
        }

        [RegisterCommand(Name = "cl.spawner.start", Help = "Send request to start room. 1 Room Name, 2 Max Connections", MinArgCount = 1)]
        private static void SendRequestSpawn(CommandArg[] args)
        {
            var options = new MstProperties();
            options.Add(MstDictKeys.ROOM_NAME, args[0].String.Replace('+', ' '));

            if (args.Length > 1)
            {
                options.Add(MstDictKeys.ROOM_MAX_PLAYERS, args[1].String);
            }

            var customOptions = new MstProperties();
            customOptions.Add("-myName", "\"John Adams\"");
            customOptions.Add("-myAge", 45);
            customOptions.Add("-msfStartClientConnection", string.Empty);

            Mst.Client.Spawners.RequestSpawn(options, customOptions, string.Empty, (controller, error) =>
            {
                if (controller == null) return;

                MstTimer.WaitWhile(() =>
                {
                    return controller.Status != SpawnStatus.Finalized;
                }, (isSuccess) =>
                {

                    if (!isSuccess)
                    {
                        Mst.Client.Spawners.AbortSpawn(controller.SpawnTaskId);
                        Logs.Error("You have failed to spawn new room");
                    }

                    Logs.Info("You have successfully spawned new room");
                }, 60f);
            });
        }

        [RegisterCommand(Name = "cl.spawner.abort", Help = "Send request to start room. 1 Process Id", MinArgCount = 1)]
        private static void SendAbortSpawn(CommandArg[] args)
        {
            Mst.Client.Spawners.AbortSpawn(args[0].Int);
        }
    }
}
