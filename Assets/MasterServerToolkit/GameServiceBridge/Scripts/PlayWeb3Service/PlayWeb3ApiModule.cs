using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class PlayWeb3ApiModule : BaseServerModule
    {
        [Header("Settings"), SerializeField]
        private string apiUrl = "https://playweb3.io/api/v1";
        [SerializeField]
        private string apiKey = "I8r6QVCEDTDUTvecOF5421YRhGG7ThvrG2G64+Mh1IHhtDFDYxMLVE/z5dpHeHIwXwzM8C6gCbun2fua1hsrAkFTw4jlBOAiJy7kocQMW5dM1vTzG8j0mL3Iki8WlsqE";

        protected override void Awake()
        {
            base.Awake();
            apiKey = Mst.Args.AsString(GameServiceArgNames.PW3_API_KEY, apiKey);
        }

        public override void Initialize(IServer server)
        {
            server.RegisterMessageHandler(GameServiceOpCodes.PlayWeb3GetUserWalletByKey, GetUserWalletByKeyMessageHandler);
            server.RegisterMessageHandler(GameServiceOpCodes.PlayWeb3GetArtifacts, GetArtifactsMessageHandler);
            server.RegisterMessageHandler(GameServiceOpCodes.PlayWeb3GetArtifactPurchases, GetArtifactPurchasesMessageHandler);
            server.RegisterMessageHandler(GameServiceOpCodes.PlayWeb3RegisterArtifactPurchasedWebhook, PlayWeb3RegisterArtifactPurchasedWebhookMessageHandler);
        }

        private Dictionary<string, string> CreateHeaders()
        {
            return new Dictionary<string, string> {
                { "X-API-Key", apiKey }
            };
        }

        private string CreateGetWalletUrl(string authKey)
        {
            return $"{apiUrl}/users/get_wallet_by_key?auth_key={authKey}";
        }

        private string CreateGetArtifactsUrl(int skip, int limit)
        {
            return $"{apiUrl}/game/artifacts?skip={skip}&limit={limit}";
        }

        private string CreateGetArtifactPurchasesUrl(int skip, int limit, string wallet)
        {
            return $"{apiUrl}/game/artifacts/purchases?skip={skip}&limit={limit}&user_wallet={wallet}";
        }

        #region MESSAGE HANDLERS

        private async Task GetUserWalletByKeyMessageHandler(IIncomingMessage message)
        {
            string authKey = message.AsString();
            var headers = CreateHeaders();
            string url = CreateGetWalletUrl(authKey);

            MstJson json = await NetWebRequests.GetAsync(url, headers);

            if (json.HasField("data") && json["data"].HasField("wallet"))
            {
                message.Respond(json["data"]["wallet"].StringValue, ResponseStatus.Success);
            }
            else if (json.HasField("error"))
            {
                logger.Error(json["error"].StringValue);
                message.Respond(json["error"].StringValue, ResponseStatus.Failed);
            }
            else
            {
                message.Respond(ResponseStatus.NotHandled);
            }
        }

        private async Task GetArtifactsMessageHandler(IIncomingMessage message)
        {
            IntPairPacket data = message.AsPacket<IntPairPacket>();
            var headers = CreateHeaders();
            string url = CreateGetArtifactsUrl(data.A, data.B);

            MstJson json = await NetWebRequests.GetAsync(url, headers);

            if (json.HasField("data") && json["data"].IsArray)
            {
                var products = new List<ProductInfo>();

                foreach (var item in json["data"])
                {
                    var product = new ProductInfo()
                    {
                        Id = item["id"].IntValue.ToString(),
                        Title = item["title"].StringValue,
                        Description = item["description"].StringValue,
                        ImageUrl = MstJson.EmptyObject,
                        PriceValue = item["price_value"].IntValue,
                        PriceCurrencyCode = item["price_currency"].StringValue,
                        Platform = GameServiceId.PlayWeb3
                    };

                    product.PriceFormat = $"{product.PriceValue} {product.PriceCurrencyCode}";
                    product.ExtraProperties.Add("game_id", item["game_id"].IntValue);
                    product.ExtraProperties.Add("kind", item["kind"].StringValue);

                    products.Add(product);
                }

                message.Respond(products.ToBytes(), ResponseStatus.Success);
            }
            else if (json.HasField("error"))
            {
                logger.Error(json["error"].StringValue);
                message.Respond(ResponseStatus.Failed);
            }
            else
            {
                message.Respond(ResponseStatus.NotFound.ToString(), ResponseStatus.NotFound);
            }
        }

        private async Task PlayWeb3RegisterArtifactPurchasedWebhookMessageHandler(IIncomingMessage message)
        {
            string productId = message.AsString();


        }

        private async Task GetArtifactPurchasesMessageHandler(IIncomingMessage message)
        {
            GetArtifactPurchases data = message.AsPacket<GetArtifactPurchases>();
            var headers = CreateHeaders();
            string url = CreateGetArtifactPurchasesUrl(data.skip, data.limit, data.wallet);

            MstJson json = await NetWebRequests.GetAsync(url, headers);

            if (json.HasField("data") && json.IsArray)
            {
                message.Respond(json.ToString(), ResponseStatus.Success);
            }
            else if (json.HasField("error"))
            {
                logger.Error(json["error"].StringValue);
                message.Respond(json["error"].StringValue, ResponseStatus.Failed);
            }
            else
            {
                message.Respond(ResponseStatus.NotFound.ToString(), ResponseStatus.NotFound);
            }
        }

        #endregion
    }
}