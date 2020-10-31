using MasterServerToolkit.MasterServer;
using CommandTerminal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace MasterServerToolkit.Client.Utilities
{
    public class SmtpTerminalCommands
    {
        private void Start()
        {
            Terminal.Shell.AddCommand("smtp.send", ClientAuthSignInAsGuest, 1, 1, "Sends E-Mail message to given address");
        }

        [RegisterCommand(Name = "master.smtp.send", Help = "Sends E-Mail message to given address. 1 Email, 2 Message", MinArgCount = 2)]
        private static async void ClientAuthSignInAsGuest(CommandArg[] args)
        {
            string[] argsArray = args.Select(i => i.String).ToArray();

            var mailer = Object.FindObjectOfType<Mailer>();
            var message = Mst.Helper.JoinCommandArgs(argsArray, 1);
            bool sentResult = await mailer.SendMailAsync(args[0].String, "Test Message", message);
        }
    }
}