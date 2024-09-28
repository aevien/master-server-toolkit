using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class PlayWeb3Service : BaseGameService
    {
        public PlayWeb3Service()
        {
            Id = GameServiceId.PlayWeb3;
            Player = new PlayerInfo();
        }

        public override IEnumerator Init()
        {
            var json = MstWebBrowser.GetQueryStringData();

#if UNITY_EDITOR
            string pw3Auth = Mst.Args.AsString(GameServiceArgNames.PW3_AUTH_KEY);

            if (!string.IsNullOrEmpty(pw3Auth))
            {
                json.SetField("pw3_auth", pw3Auth);
            }
#endif
            if (json.HasField("pw3_auth"))
            {
                while (!Mst.Client.Connection.IsConnected)
                {
                    yield return new WaitForEndOfFrame();
                }

                yield return GameBridge.Instance.StartCoroutine(Authenticate((isSuccess, error) =>
                {
                    NotifyOnReady();
                }));
            }
            else
            {
                NotifyOnReady();
            }
        }

        public override IEnumerator Authenticate(SuccessCallback callback)
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

            Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetUserWalletByKey,
                json["pw3_auth"].StringValue, (status, message) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        callback?.Invoke(false, message.AsString());
                    }
                    else
                    {
                        Player.Id = message.AsString();
                        Player.IsGuest = false;
                        callback?.Invoke(true, string.Empty);
                    }

                    done = true;
                });

            while (!done)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        public override IEnumerator GetProducts(SuccessCallback callback)
        {
            bool done = false;
            IntPairPacket data = new IntPairPacket();
            data.A = 0;
            data.B = 100;

            Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetArtifacts, data, (status, message) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        callback?.Invoke(false, message.AsString());
                    }
                    else
                    {
                        Products = message.AsPacketsList<ProductInfo>();
                        callback?.Invoke(true, string.Empty);
                    }

                    done = true;
                });

            while (!done)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        public override IEnumerator GetProductPurchases(SuccessCallback callback)
        {
            bool done = false;
            GetArtifactPurchases data = new GetArtifactPurchases();
            data.skip = 0;
            data.limit = 100;
            data.wallet = Player.Id;

            Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetArtifactPurchases, data, (status, message) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, message.AsString());
                }
                else
                {
                    Purchases = new Json.MstJson(message.AsString());
                    callback?.Invoke(true, string.Empty);
                }

                done = true;
            });

            while (!done)
            {
                yield return new WaitForEndOfFrame();
            }
        }
    }
}