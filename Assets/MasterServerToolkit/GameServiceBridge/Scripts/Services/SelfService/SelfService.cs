using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class SelfService : BaseGameService
    {
        protected override void Awake()
        {
            base.Awake();

            Id = GameServiceId.Self;
            Player = new PlayerInfo();
        }

        public override void Authenticate(SuccessCallback callback)
        {
            base.Authenticate(callback);
            NotifyOnAuthenticated(true, string.Empty);
        }

        public override void GetPurchases(PurchaseHandler callback)
        {
            base.GetPurchases(callback);
            NotifyOnGetPurchases(null);
        }

        public override void GetProducts(ProductsHandler callback)
        {
            base.GetProducts(callback);
            NotifyOnGetProducts(Enumerable.Empty<ProductInfo>());
        }

        public override void Init(MstJson options)
        {
            base.Init(options);
            GetOrCretePlayerData();
        }

        private void GetOrCretePlayerData()
        {
            var playerInfo = MstJson.EmptyObject;

            if (PlayerPrefs.HasKey("playerData"))
            {
                playerInfo = new MstJson(PlayerPrefs.GetString("playerData"));
            }
            else
            {
                playerInfo.AddField("id", Guid.NewGuid().ToString());
                playerInfo.AddField("name", SimpleNameGenerator.Generate(Gender.Male));
                playerInfo.AddField("is_guest", true);
                playerInfo.AddField("avatar", "");
                playerInfo.AddField("extra", MstJson.EmptyObject);
                PlayerPrefs.SetString("playerData", playerInfo.ToString());
                PlayerPrefs.Save();
            }

            Player.Id = playerInfo["id"].StringValue;
            Player.Name = playerInfo["name"].StringValue;
            Player.IsGuest = playerInfo["is_guest"].BoolValue;
            Player.Avatar = playerInfo["avatar"].StringValue;
            Player.Extra = playerInfo.HasField("extra") ? playerInfo["extra"] : MstJson.EmptyObject;

            NotifyOnPlayerInfo();
            NotifyOnPlayerData();
            NotifyOnReady();
        }
    }
}