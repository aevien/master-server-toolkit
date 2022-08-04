using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public enum ObservablePropertyCodes { DisplayName, Avatar, Bronze, Silver, Gold, Items }

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

            server.RegisterMessageHandler((ushort)MstOpCodes.UpdateDisplayNameRequest, UpdateDisplayNameRequestHandler);

            //Update profile resources each 5 sec
            InvokeRepeating(nameof(IncreaseResources), 1f, 1f);
        }

        public override JObject JsonInfo()
        {
            var json = base.JsonInfo();
            json["name"] = "ProfilesModule";
            return json;
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
                new ObservableString((ushort)ObservablePropertyCodes.DisplayName, SimpleNameGenerator.Generate(Gender.Male)),
                new ObservableString((ushort)ObservablePropertyCodes.Avatar, avatarUrl),
                new ObservableFloat((ushort)ObservablePropertyCodes.Bronze, bronze),
                new ObservableFloat((ushort)ObservablePropertyCodes.Silver, silver),
                new ObservableFloat((ushort)ObservablePropertyCodes.Gold, gold),
                new ObservableDictStringInt((ushort)ObservablePropertyCodes.Items, new Dictionary<string, int>())
            };
        }

        private void IncreaseResources()
        {
            foreach (var profile in Profiles)
            {
                var bronzeProperty = profile.Get<ObservableFloat>((ushort)ObservablePropertyCodes.Bronze);
                var silverProperty = profile.Get<ObservableFloat>((ushort)ObservablePropertyCodes.Silver);
                var goldProperty = profile.Get<ObservableFloat>((ushort)ObservablePropertyCodes.Gold);

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
                if (profilesList.TryGetValue(userExtension.UserId, out ObservableServerProfile profile))
                {
                    profile.Get<ObservableString>((ushort)ObservablePropertyCodes.DisplayName).Value = newProfileData["displayName"];
                    profile.Get<ObservableString>((ushort)ObservablePropertyCodes.Avatar).Value = newProfileData["avatarUrl"];

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
