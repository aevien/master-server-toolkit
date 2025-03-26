using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System.Collections;
using System.Linq;
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
        }

        public override void Init(MstJson options)
        {
            base.Init(options);

            IEnumerator coroutine()
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

            StartCoroutine(coroutine());
        }

        public override void Authenticate(SuccessCallback callback)
        {
            base.Authenticate(callback);

            var json = Payload;

#if UNITY_EDITOR
            // For test purpose only
            string pw3Auth = Mst.Args.AsString(GameServiceArgNames.PW3_AUTH_KEY);

            if (!string.IsNullOrEmpty(pw3Auth))
            {
                json.SetField("pw3_auth", pw3Auth);
            }
#endif
            if (json.HasField("pw3_auth"))
            {
                Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetUserWalletByKey,
                json["pw3_auth"].StringValue, (status, message) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        Player.Id = message.AsString();
                        Player.IsGuest = true;
                        NotifyOnAuthenticated(false, message.AsString());
                        NotifyOnPlayerInfo();
                        NotifyOnPlayerData();
                    }
                    else
                    {
                        Player.Id = message.AsString();
                        Player.IsGuest = false;
                        NotifyOnAuthenticated(true, string.Empty);
                        NotifyOnPlayerInfo();
                        NotifyOnPlayerData();
                    }
                });
            }
            else
            {
                NotifyOnAuthenticated(false, "PlayWeb3 auth token not found");
            }
        }

        #region PURCHASES

        public override void GetProducts(ProductsHandler callback)
        {
            base.GetProducts(callback);

            if (IsInAppPurchaseSupported && Products.Count() == 0)
            {
                IntPairPacket data = new IntPairPacket();
                data.A = 0;
                data.B = 100;

                Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetArtifacts, data, (status, message) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        NotifyOnGetProducts(Products);
                    }
                    else
                    {
                        Products = message.AsPacketsList<ProductInfo>();
                        NotifyOnGetProducts(Products);
                    }
                });
            }
            else
            {
                NotifyOnGetProducts(Products);
            }
        }

        public override void Purchase(string productId, PurchaseHandler callback)
        {
            base.Purchase(productId, callback);
        }

        public override void GetPurchases(PurchaseHandler callback)
        {
            base.GetPurchases(callback);

            GetArtifactPurchases data = new GetArtifactPurchases();
            data.skip = 0;
            data.limit = 100;
            data.wallet = Player.Id;

            Mst.Client.Connection.SendMessage(GameServiceOpCodes.PlayWeb3GetArtifactPurchases, data, (status, message) =>
            {
                if (status != ResponseStatus.Success)
                {
                    NotifyOnGetPurchases(null);
                }
                else
                {
                    Purchases = new PurchasesInfo()
                    {
                        serviceId = GameServiceId.PlayWeb3,
                        data = new MstJson(message.AsString())
                    };

                    NotifyOnGetPurchases(Purchases);
                }
            });
        }

        #endregion
    }
}