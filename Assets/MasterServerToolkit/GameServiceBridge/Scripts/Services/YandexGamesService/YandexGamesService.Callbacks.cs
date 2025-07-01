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
            var data = new MstJson(json);
            Logs.Debug(data);

            if (!data.HasField("error"))
            {
                var info = new LeaderboardInfo
                {
                    Id = data["name"].StringValue,
                    IsDefault = data["dåfault"].BoolValue,
                    Invert = data["description"]["invert_sort_order"].BoolValue,
                    DecimalOffset = data["description"]["score_format"]["options"]["decimal_offset"].IntValue
                };

                if (Enum.TryParse(data["description"]["score_format"]["type"].StringValue, out LeaderboardType type))
                {
                    info.Type = type;
                }

                LeaderboardDescription = info;
                NotifyOnGetLeaderboardInfo(LeaderboardDescription);
            }
            else
            {
                NotifyOnGetLeaderboardInfo(null);
            }
        }

        protected void Yg_OnGetLeaderboardEntries(string json)
        {
            var data = new MstJson(json);

            if (!data.HasField("error"))
            {
                var info = new LeaderboardEntries()
                {
                    Id = data["leaderboard"]["name"].StringValue,
                    IsDefault = data["leaderboard"]["dåfault"].BoolValue,
                    Invert = data["leaderboard"]["description"]["invert_sort_order"].BoolValue,
                    DecimalOffset = data["leaderboard"]["description"]["score_format"]["options"]["decimal_offset"].IntValue,
                    UserRank = data["userRank"].IntValue,
                    Start = data["ranges"]["start"].IntValue,
                    Size = data["ranges"]["size"].IntValue
                };

                if (Enum.TryParse(data["leaderboard"]["description"]["score_format"]["type"].StringValue, out LeaderboardType type))
                {
                    info.Type = type;
                }

                foreach (var entry in data["entries"])
                {
                    var newEntry = new LeaderboardPlayerInfo
                    {
                        Score = entry["score"].IntValue,
                        Extra = entry["extraData"],
                        Rank = entry["rank"].IntValue,
                        FormatedScore = entry["formattedScore"].StringValue,
                        PlayerAvatar = entry["player"]["avatar"].StringValue,
                        PlayerLang = entry["player"]["lang"].StringValue,
                        PlayerName = entry["player"]["publicName"].StringValue,
                        PlayerId = entry["player"]["uniqueID"].StringValue,
                        IsPlayerAvatarAllowed = entry["player"]["scopePermissions"].HasField("avatar") && entry["player"]["scopePermissions"]["avatar"].StringValue == "allow",
                        IsPlayerNameAllowed = entry["player"]["scopePermissions"].HasField("public_name") && entry["player"]["scopePermissions"]["public_name"].StringValue == "allow"
                    };

                    info.Entries.Add(newEntry);
                }

                LeaderboardEntries = info;
                NotifyOnGetLeaderboardEntries(LeaderboardEntries);
            }
            else
            {
                NotifyOnGetLeaderboardEntries(LeaderboardEntries);
            }
        }

        protected void Yg_OnGetLeaderboardPlayerEntry(string json)
        {
            var data = new MstJson(json);
            Logs.Debug(data);

            if (!data.HasField("error"))
            {
                var info = new LeaderboardPlayerInfo
                {
                    Score = data["score"].IntValue,
                    Extra = data["extraData"],
                    Rank = data["rank"].IntValue,
                    FormatedScore = data["formattedScore"].StringValue,
                    PlayerAvatar = data["player"]["avatar"].StringValue,
                    PlayerLang = data["player"]["lang"].StringValue,
                    PlayerName = data["player"]["publicName"].StringValue,
                    PlayerId = data["player"]["uniqueID"].StringValue,
                    IsPlayerAvatarAllowed = data["player"]["scopePermissions"].HasField("avatar") && data["player"]["scopePermissions"]["avatar"].StringValue == "allow",
                    IsPlayerNameAllowed = data["player"]["scopePermissions"].HasField("public_name") && data["player"]["scopePermissions"]["public_name"].StringValue == "allow"
                };

                LeaderboardPlayerEntry = info;
                NotifyOnGetLeaderboardPlayerInfo(LeaderboardPlayerEntry);
            }
            else
            {
                NotifyOnGetLeaderboardPlayerInfo(null);
            }
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