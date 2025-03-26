using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.Demos.BasicProfile
{
    public class ProfilesModule : MasterServer.ProfilesModule
    {
        [SerializeField]
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up profiles values for new users"
        };

        public override void Initialize(IServer server)
        {
            base.Initialize(server);

            server.RegisterMessageHandler(MstOpCodes.UpdateDisplayNameRequest, UpdateDisplayNameRequestHandler);
            server.RegisterMessageHandler(MessageOpCodes.BuyDemoItem, BuyDemoItemMessageHandler);
            server.RegisterMessageHandler(MessageOpCodes.SellDemoItem, SellDemoItemMessageHandler);

            //Update profile resources each 5 sec
            InvokeRepeating(nameof(IncreaseResources), 1f, 1f);
        }
        private void IncreaseResources()
        {
            foreach (var profile in Profiles)
            {
                if (profile.TryGet(ProfilePropertyOpCodes.gold, out ObservableInt goldProperty))
                    goldProperty.Add(1);

                if (profile.TryGet(ProfilePropertyOpCodes.silver, out ObservableInt silverProperty))
                    silverProperty.Add(10);

                if (profile.TryGet(ProfilePropertyOpCodes.bronze, out ObservableInt bronzeProperty))
                    bronzeProperty.Add(100);
            }
        }

        private async Task UpdateDisplayNameRequestHandler(IIncomingMessage message)
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

            await Task.CompletedTask;
        }

        private async Task BuyDemoItemMessageHandler(IIncomingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null || userExtension.Account == null)
            {
                message.Respond("Invalid session", ResponseStatus.Unauthorized);
                return;
            }

            var data = message.AsPacket<BuySellItemPacket>();

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
                        items.Add(data.Id, 1);
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

            await Task.CompletedTask;
        }

        private async Task SellDemoItemMessageHandler(IIncomingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null || userExtension.Account == null)
            {
                message.Respond("Invalid session", ResponseStatus.Unauthorized);
                return;
            }

            var data = message.AsPacket<BuySellItemPacket>();

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

            await Task.CompletedTask;
        }
    }
}
