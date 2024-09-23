using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class PlayWeb3Module : BaseServerModule
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

            string result = await Task.Run(() =>
            {
                return NetWebRequests.Get(url, headers);
            });

            var json = new MstJson(result);

            if (json.HasField("wallet"))
            {
                message.Respond(json["wallet"].StringValue, ResponseStatus.Success);
            }
            else if (json.HasField("detail"))
            {
                logger.Error(json["detail"].StringValue);
                message.Respond(ResponseStatus.Failed);
            }
            else
            {
                message.Respond(ResponseStatus.NotFound);
            }
        }

        private async Task GetArtifactsMessageHandler(IIncomingMessage message)
        {
            IntPairPacket data = message.AsPacket<IntPairPacket>();
            var headers = CreateHeaders();
            string url = CreateGetArtifactsUrl(data.A, data.B);

            string result = await Task.Run(() =>
            {
                return NetWebRequests.Get(url, headers);
            });

            var json = new MstJson(result);

            if (json.IsArray)
            {
                message.Respond(json.ToString(), ResponseStatus.Success);
            }
            else if (json.HasField("detail"))
            {
                logger.Error(json["detail"].StringValue);
                message.Respond(ResponseStatus.Failed);
            }
            else
            {
                message.Respond(ResponseStatus.NotFound);
            }
        }

        private async Task GetArtifactPurchasesMessageHandler(IIncomingMessage message)
        {
            GetArtifactPurchases data = message.AsPacket<GetArtifactPurchases>();
            var headers = CreateHeaders();
            string url = CreateGetArtifactPurchasesUrl(data.skip, data.limit, data.wallet);

            string result = await Task.Run(() =>
            {
                return NetWebRequests.Get(url, headers);
            });

            var json = new MstJson(result);

            if (json.IsArray)
            {
                message.Respond(json.ToString(), ResponseStatus.Success);
            }
            else if (json.HasField("detail"))
            {
                logger.Error(json["detail"].StringValue);
                message.Respond(ResponseStatus.Failed);
            }
            else
            {
                message.Respond(ResponseStatus.NotFound);
            }
        }

        #endregion
    }
}