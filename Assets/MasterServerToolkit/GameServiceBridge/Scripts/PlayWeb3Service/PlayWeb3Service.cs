using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class PlayWeb3Service : BaseGameService
    {
        protected override void Awake()
        {
            base.Awake();

            Id = GameServiceId.PlayWeb3;
            Player = new PlayerInfo();
            IsInAppPurchaseSupported = true;
        }

        public override void Init()
        {
            IEnumerator Coroutine()
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

                    Authenticate((isSuccess, error) =>
                    {
                        NotifyOnReady();
                    });
                }
                else
                {
                    NotifyOnReady();
                }
            }

            StartCoroutine(Coroutine());
        }

        public override void Authenticate(SuccessCallback callback)
        {
            IEnumerator Coroutine(SuccessCallback callback)
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

                Logs.Debug(json);

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

            StartCoroutine(Coroutine(callback));
        }

        #region PURCHASES

        public override void GetProducts(SuccessCallback callback)
        {
            IEnumerator Coroutine(SuccessCallback callback)
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

            StartCoroutine(Coroutine(callback));
        }

        public override void Purchase(string productId, SuccessCallback callback)
        {
            IEnumerator Coroutine(string productId, SuccessCallback callback)
            {
                //Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetArtifacts,  

                yield return new WaitForEndOfFrame();
            }

            StartCoroutine(Coroutine(productId, callback));
        }

        public override void GetProductPurchases(SuccessCallback callback)
        {
            IEnumerator Coroutine(SuccessCallback callback)
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

            StartCoroutine(Coroutine(callback));
        }

        #endregion
    }
}