using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public class PlayWeb3Service : BaseGameService
    {
        public PlayWeb3Service()
        {
            Id = GameServiceId.PlayWeb3;
        }

        public override IEnumerator Authenticate(UnityAction<bool> callback)
        {
            var json = MstWebBrowser.GetQueryStringData();

#if UNITY_EDITOR
            string pw3Auth = Mst.Args.AsString(GameServiceArgNames.PW3_AUTH_KEY);

            if (!string.IsNullOrEmpty(pw3Auth))
            {
                json.SetField("pw3_auth", pw3Auth);
            }
#endif

            bool done = false;
            bool result = false;

            Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetUserWalletByKey,
                json["pw3_auth"].StringValue, (status, message) =>
                {
                    if (status != Networking.ResponseStatus.Success)
                    {
                        Debug.LogError(message.AsString());
                        result = false;
                    }
                    else
                    {
                        PlayerId = message.AsString();
                        result = true;
                    }

                    done = true;
                });

            while (!done)
            {
                yield return null;
            }

            callback?.Invoke(result);
        }

        public override IEnumerator GetProducts(UnityAction<bool> callback)
        {
            bool done = false;
            bool result = false;
            IntPairPacket data = new IntPairPacket();
            data.A = 0;
            data.B = 100;

            Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetArtifacts, data, (status, message) =>
                {
                    if (status != Networking.ResponseStatus.Success)
                    {
                        Debug.LogError(message.AsString());
                        result = false;
                    }
                    else
                    {
                        Products = new Json.MstJson(message.AsString());
                        result = true;
                    }

                    done = true;
                });

            while (!done)
            {
                yield return null;
            }

            callback?.Invoke(result);
        }

        public override IEnumerator GetProductPurchases(UnityAction<bool> callback)
        {
            bool done = false;
            bool result = false;
            GetArtifactPurchases data = new GetArtifactPurchases();
            data.skip = 0;
            data.limit = 100;
            data.wallet = PlayerId;

            Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetArtifactPurchases, data, (status, message) =>
            {
                if (status != Networking.ResponseStatus.Success)
                {
                    Debug.LogError(message.AsString());
                    result = false;
                }
                else
                {
                    Purchases = new Json.MstJson(message.AsString());
                    result = true;
                }

                done = true;
            });

            while (!done)
            {
                yield return null;
            }

            callback?.Invoke(result);
        }
    }
}