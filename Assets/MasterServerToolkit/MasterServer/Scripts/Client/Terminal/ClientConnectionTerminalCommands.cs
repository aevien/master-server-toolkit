using MasterServerToolkit.MasterServer;
using CommandTerminal;
using UnityEngine;

namespace MasterServerToolkit.Client.Utilities
{
    public class ClientConnectionTerminalCommands
    {
        [RegisterCommand(Name = "cl.ping", Help = "Send ping to master server")]
        private static void SendPingCmd(CommandArg[] args)
        {
            Mst.Connection.SendMessage((short)MstMessageCodes.Ping, (status, response) =>
            {
                Debug.Log($"Message: {response.AsString()}, Status: {response.Status.ToString()}");
            });
        }

        [RegisterCommand(Name = "cl.connect", Help = "Connects the client to master. 1 Server IP address, 2 Server port", MinArgCount = 1)]
        private static void ClientConnect(CommandArg[] args)
        {
            Mst.Connection.UseSsl = MstApplicationConfig.Instance.UseSecure || Mst.Args.UseSecure;
            Mst.Connection.Connect(args[0].String, Mathf.Clamp(args[1].Int, 0, ushort.MaxValue));
        }

        [RegisterCommand(Name = "cl.disconnect", Help = "Disconnects the client from master")]
        private static void ClientDisconnect(CommandArg[] args)
        {
            Mst.Connection.Disconnect();
        }
    }
}