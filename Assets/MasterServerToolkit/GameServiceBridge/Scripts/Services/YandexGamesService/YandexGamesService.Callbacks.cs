using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.GameService
{
    public partial class YandexGamesService : BaseGameService
    {
        #region WEBGL CALLBACKS

        protected void Yg_OnAuthPlayer(string json)
        {
            var data = new MstJson(json);

            if (data["success"].BoolValue)
            {
                NotifyOnAuthenticated(true, string.Empty);

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                Gb_Yg_GetPlayer();
#endif
            }
            else
            {
                NotifyOnAuthenticated(false, data["error"].StringValue);
            }
        }

        protected void Yg_OnGetPlayer(string json)
        {
            var data = new MstJson(json);
            Player.Id = data["id"].StringValue;
            Player.Name = data["name"].StringValue;
            Player.IsGuest = data["is_guest"].BoolValue;
            Player.Avatar = data["avatar"].StringValue;
            Player.Extra = data.HasField("extra") ? data["extra"] : MstJson.EmptyObject;

            NotifyOnPlayerInfo();

            if (!IsReady)
            {
                if (options.HasField(GameServiceOptionKeys.YG_AUTOSEND_API_READY)
                    && options[GameServiceOptionKeys.YG_AUTOSEND_API_READY].BoolValue)
                {
                    GameLoaded();
                }

                MstTimer.WaitForSeconds(0.1f, () =>
                {
                    NotifyOnReady();
                });
            }
        }

        protected void Yg_OnPlayerGetData(string json)
        {
            Data = new MstJson(json);
            NotifyOnPlayerData();
        }

        protected void Yg_OnPlayerSetData(string json)
        {
            var data = new MstJson(json);
            NotifyOnSetPlayerData(data["success"].BoolValue, data["error"].StringValue);
        }

        protected void Yg_OnGameApiPause(int result)
        {
            NotifyOnPause(result > 0);
        }

        protected void Yg_OnFullScreenVideoStatus(string status)
        {
            NotifyOnFullScreenVideoStatus(Enum.Parse<FullScreenVideoStatus>(status));
        }

        protected void Yg_OnRewardedVideoStatus(string status)
        {
            NotifyOnRewardedVideoStatus(Enum.Parse<RewardedVideoStatus>(status));
        }

        protected void Yg_OnGetLeaderboardDescription(string json)
        {
            var leaderboardJson = new MstJson(json);
        }

        protected void Yg_OnGetLeaderboardEntries(string json)
        {
            var data = new MstJson(json);
            Logs.Debug(data);
        }

        protected void Yg_OnPurchaseResult(string json)
        {
            var data = new PurchasesInfo()
            {
                serviceId = GameServiceId.YandexGames,
                data = new MstJson(json)
            };

            NotifyOnPurchase(data);
        }

        protected void Yg_OnGetProducts(string json)
        {
            var data = new MstJson(json);

            List<ProductInfo> products = new List<ProductInfo>();

            foreach (var productJson in data)
            {
                var productInfo = new ProductInfo()
                {
                    Id = productJson["id"].StringValue,
                    Title = productJson["title"].StringValue,
                    Description = productJson["description"].StringValue,
                    ImageUrl = productJson["imageUrl"].StringValue,
                    Price = productJson["price"].StringValue,
                    PriceValue = productJson["priceValue"].IntValue,
                    PriceCurrencyCode = productJson["priceCurrencyCode"].StringValue,
                    PriceCurrencyImage = productJson["priceCurrencyImage"],
                    Extra = productJson.HasField("extra") ? productJson["extra"] : MstJson.NullObject
                };

                products.Add(productInfo);
            }

            Products = products;
            NotifyOnGetProducts(Products);
        }

        protected void Yg_OnGetPurchases(string json)
        {
            var data = new PurchasesInfo()
            {
                serviceId = GameServiceId.YandexGames,
                data = new MstJson(json)
            };

            NotifyOnGetPurchases(data);
        }

        #endregion
    }
}