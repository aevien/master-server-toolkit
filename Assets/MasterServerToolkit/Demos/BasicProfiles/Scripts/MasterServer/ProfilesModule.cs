using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class ProfilesModule : MasterServer.ProfilesModule
    {
        [Header("Start Values"), SerializeField]
        private int bronze = 100;
        [SerializeField]
        private int silver = 50;
        [SerializeField]
        private int gold = 50;
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

            server.RegisterMessageHandler(MstOpCodes.UpdateDisplayNameRequest, UpdateDisplayNameRequestHandler);
            server.RegisterMessageHandler(MessageOpCodes.BuyDemoItem, BuyDemoItemMessageHandler);
            server.RegisterMessageHandler(MessageOpCodes.SellDemoItem, SellDemoItemMessageHandler);

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
            var profile = new ObservableServerProfile(userId, clientPeer);

            ProfileProperties.Fill(profile);

            profile.Get<ObservableString>(ProfilePropertyOpCodes.displayName).Value = SimpleNameGenerator.Generate(Gender.Male);
            profile.Get<ObservableString>(ProfilePropertyOpCodes.avatarUrl).Value = avatarUrl;

            if (profile.TryGet(ProfilePropertyOpCodes.gold, out ObservableInt goldProperty))
                goldProperty.Add(gold);

            if (profile.TryGet(ProfilePropertyOpCodes.silver, out ObservableInt silverProperty))
                silverProperty.Add(silver);

            if (profile.TryGet(ProfilePropertyOpCodes.bronze, out ObservableInt bronzeProperty))
                bronzeProperty.Add(bronze);

            return profile;
        }

        private void IncreaseResources()
        {
            foreach (var profile in Profiles)
            {
                if (profile.TryGet(ProfilePropertyOpCodes.gold, out ObservableInt goldProperty))
                    goldProperty.Add(1);

                if (profile.TryGet(ProfilePropertyOpCodes.silver, out ObservableInt silverProperty))
                    silverProperty.Add(5);

                if (profile.TryGet(ProfilePropertyOpCodes.bronze, out ObservableInt bronzeProperty))
                    bronzeProperty.Add(10);
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
                    profile.Get<ObservableString>(ProfilePropertyOpCodes.displayName).Value = newProfileData["displayName"];
                    profile.Get<ObservableString>(ProfilePropertyOpCodes.avatarUrl).Value = newProfileData["avatarUrl"];

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

        private void BuyDemoItemMessageHandler(IIncomingMessage message)
        {
            try
            {
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null || userExtension.Account == null)
                {
                    message.Respond("Invalid session", ResponseStatus.Unauthorized);
                    return;
                }

                var data = message.AsPacket(new BuySellItemPacket());

                if (profilesList.TryGetValue(userExtension.UserId, out ObservableServerProfile profile)
                        && profile.TryGet(ProfilePropertyOpCodes.items, out ObservableDictStringInt items))
                {
                    if (profile.TryGet(data.Currency.ToUint16Hash(), out ObservableInt currencyProperty)
                        && currencyProperty.Subtract(data.Price, 0))
                    {
                        if (items.ContainsKey(data.Id))
                        {
                            items[data.Id]++;
                        }
                        else
                        {
                            items[data.Id] = 1;
                        }

                        message.Respond(ResponseStatus.Success);
                    }
                    else
                    {
                        message.Respond($"You don't have enough {data.Currency}", ResponseStatus.Failed);
                    }
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

        private void SellDemoItemMessageHandler(IIncomingMessage message)
        {
            try
            {
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null || userExtension.Account == null)
                {
                    message.Respond("Invalid session", ResponseStatus.Unauthorized);
                    return;
                }

                var data = message.AsPacket(new BuySellItemPacket());

                if (profilesList.TryGetValue(userExtension.UserId, out ObservableServerProfile profile)
                        && profile.TryGet(ProfilePropertyOpCodes.items, out ObservableDictStringInt items))
                {
                    if (profile.TryGet(data.Currency.ToUint16Hash(), out ObservableInt currencyProperty))
                    {
                        if (items.ContainsKey(data.Id))
                        {
                            items[data.Id]--;

                            if (items[data.Id] <= 0)
                                items.Remove(data.Id);

                            currencyProperty.Add(data.Price);
                        }

                        message.Respond(ResponseStatus.Success);
                    }
                    else
                    {
                        message.Respond($"Our store does not accept {data.Currency} as currency", ResponseStatus.Failed);
                    }
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
