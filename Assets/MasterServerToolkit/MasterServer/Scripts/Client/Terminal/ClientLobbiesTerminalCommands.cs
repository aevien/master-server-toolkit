using CommandTerminal;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Client.Utilities
{
    public class ClientLobbiesTerminalCommands
    {
        [RegisterCommand(Name = "cl.lobbies.start", Help = "Start new lobby")]
        private static void LobbyCreateNew(CommandArg[] args)
        {
            MstProperties options = new MstProperties();

            Mst.Client.Lobbies.CreateLobby("", options, (lobbyId, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Logs.Error(error);
                    return;
                }

                Logs.Info("It is ok!");
            });
        }
    }
}
