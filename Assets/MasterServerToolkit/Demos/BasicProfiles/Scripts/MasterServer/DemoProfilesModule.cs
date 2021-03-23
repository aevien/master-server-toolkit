using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer.Examples.BasicProfile
{
    public enum ObservablePropertiyCodes { DisplayName, Avatar, Bronze, Silver, Gold }

    public class DemoProfilesModule : ProfilesModule
    {
        [Header("Start Values"), SerializeField]
        private float bronze = 100;
        [SerializeField]
        private float silver = 50;
        [SerializeField]
        private float gold = 50;
        [SerializeField]
        private string avatarUrl = "https://i.imgur.com/JQ9pRoD.png";

        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up profiles values for new users"
        };

        public override void Initialize(IServer server)
        {
            base.Initialize(server);

            // Set the new factory in ProfilesModule
            ProfileFactory = CreateProfileInServer;

            server.RegisterMessageHandler((short)MstMessageCodes.UpdateDisplayNameRequest, UpdateDisplayNameRequestHandler);

            //Update profile resources each 5 sec
            InvokeRepeating(nameof(IncreaseResources), 1f, 1f);
        }

        /// <summary>
        /// This method is just for creation of profile on server side as default for users that are logged in for the first time
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="clientPeer"></param>
        /// <returns></returns>
        private ObservableServerProfile CreateProfileInServer(string userId, IPeer clientPeer)
        {
            return new ObservableServerProfile(userId, clientPeer)
            {
                new ObservableString((short)ObservablePropertiyCodes.DisplayName, SimpleNameGenerator.Generate(Gender.Male)),
                new ObservableString((short)ObservablePropertiyCodes.Avatar, avatarUrl),
                new ObservableFloat((short)ObservablePropertiyCodes.Bronze, bronze),
                new ObservableFloat((short)ObservablePropertiyCodes.Silver, silver),
                new ObservableFloat((short)ObservablePropertiyCodes.Gold, gold)
            };
        }

        private void IncreaseResources()
        {
            foreach (var profile in Profiles)
            {
                var bronzeProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.Bronze);
                var silverProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.Silver);
                var goldProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.Gold);

                bronzeProperty.Add(1f);
                silverProperty.Add(0.1f);
                goldProperty.Add(0.01f);
            }
        }

        private void UpdateDisplayNameRequestHandler(IIncomingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null || userExtension.Account == null)
            {
                message.Respond("Invalid session", ResponseStatus.Unauthorized);
                return;
            }

            var newProfileData = new Dictionary<string, string>().FromBytes(message.AsBytes());

            try
            {
                if (profilesList.TryGetValue(userExtension.Username, out ObservableServerProfile profile))
                {
                    profile.GetProperty<ObservableString>((short)ObservablePropertiyCodes.DisplayName).Set(newProfileData["displayName"]);
                    profile.GetProperty<ObservableString>((short)ObservablePropertiyCodes.Avatar).Set(newProfileData["avatarUrl"]);

                    message.Respond(ResponseStatus.Success);
                }
                else
                {
                    message.Respond("Invalid session", ResponseStatus.Unauthorized);
                }
            }
            catch (Exception e)
            {
                message.Respond($"Internal Server Error: {e}", ResponseStatus.Error);
            }
        }
    }
}
