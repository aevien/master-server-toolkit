using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using CommandTerminal;
using System.Collections.Generic;

namespace MasterServerToolkit.Client.Utilities
{
    public class ClientAuthTerminalCommands
    {
        [RegisterCommand(Name = "client.auth.signinasguest", Help = "Sign up as guest client. No credentials required", MinArgCount = 0, MaxArgCount = 0)]
        private static void ClientAuthSignInAsGuest(CommandArg[] args)
        {
            Mst.Client.Auth.SignInAsGuest((accountInfo, error) =>
            {
                if (string.IsNullOrEmpty(error))
                {
                    Logs.Info($"You have logged in as: {accountInfo.Username}");
                }
                else
                {
                    Logs.Error($"An error occurred while logging in: {error}");
                }
            });
        }

        [RegisterCommand(Name = "client.auth.signin", Help = "Sign in as registered user. 1 Username, 2 Password", MinArgCount = 2, MaxArgCount = 2)]
        private static void ClientAuthSignIn(CommandArg[] args)
        {
            Mst.Client.Auth.SignIn(args[0].String, args[1].String, (accountInfo, error) =>
            {
                if (string.IsNullOrEmpty(error))
                {
                    Logs.Info($"You have logged in as: {accountInfo.Username}");
                }
                else
                {
                    Logs.Error($"An error occurred while logging in: {error}");
                }
            });
        }

        [RegisterCommand(Name = "client.auth.signup", Help = "Sign up as registered user. 1 Username, 2 E-Mail, 3 Password", MinArgCount = 3, MaxArgCount = 3)]
        private static void ClientAuthSignUp(CommandArg[] args)
        {
            var credentials = new MstProperties();
            credentials.Set("username", args[0].String);
            credentials.Set("email", args[1].String);
            credentials.Set("password", args[2].String);

            Mst.Client.Auth.SignUp(credentials, (isSuccessful, error) =>
            {
                if (isSuccessful)
                {
                    Logs.Info($"You have successfuly signed up. Now you may sign in");
                }
                else
                {
                    Logs.Error($"An error occurred while signing up: {error}");
                }
            });
        }
    }
}
