using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class YandexGamesLeaderboardsModule : BaseLeaderboardsModule
    {
        private const long JavaScriptMaxSafeInteger = 9007199254740991L;

        [DllImport("__Internal")]
        private static extern void Gb_Yg_SetLeaderboardScore(string options);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetLeaderboardDescription(string name);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetLeaderboardEntries(string name, string options);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetLeaderboardPlayerEntry(string name);

        private Coroutine leaderboardEntriesCoroutine;
        private Coroutine leaderboardPlayerEntryCoroutine;
        private readonly Dictionary<string, SuccessCallback> scoreCallbacks = new();

        public override void OnBeforeInit(IService service)
        {
            IsSupported = true;
            base.OnBeforeInit(service);
        }

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public override void SetScore(
            string name,
            long score,
            MstJson extra,
            SuccessCallback callback = null)
        {
            if (!IsSupported || !IsReady || Service?.Player == null || Service.Player.IsGuest)
            {
                callback?.Invoke(false, "leaderboard_authentication_required");
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                callback?.Invoke(false, "leaderboard_name_required");
                return;
            }

            if (score < 0 || score > JavaScriptMaxSafeInteger)
            {
                callback?.Invoke(false, "leaderboard_score_out_of_range");
                return;
            }

            string requestId = Mst.Helper.CreateGuidString();

            if (callback != null)
                scoreCallbacks[requestId] = callback;

            var options = MstJson.CreateObject();
            options.AddField("requestId", requestId);
            options.AddField("leaderboardName", name);
            options.AddField("score", score);
            options.AddField("extraData", extra?.ToString() ?? "{}");

            try
            {
                Gb_Yg_SetLeaderboardScore(options.ToString());
            }
            catch (Exception exception)
            {
                scoreCallbacks.Remove(requestId);
                callback?.Invoke(false, exception.Message);
            }
        }

        public override void GetInfo(string name, LeaderboardInfoHandler callback)
        {
            if (Description == null)
            {
                base.GetInfo(name, callback);
                Gb_Yg_GetLeaderboardDescription(name);
            }
            else
            {
                callback?.Invoke(Description);
            }
        }

        public override void GetPlayerInfo(string name, LeaderboardPlayerInfoHandler callback)
        {
            if (leaderboardPlayerEntryCoroutine == null)
            {
                base.GetPlayerInfo(name, callback);
                leaderboardPlayerEntryCoroutine = StartCoroutine(coroutine());
            }
            else
            {
                callback?.Invoke(PlayerEntry);
            }

            IEnumerator coroutine()
            {
                Gb_Yg_GetLeaderboardPlayerEntry(name);
                yield return new WaitForSecondsRealtime(Service.Options.GetField(nameof(YandexGameSdkSettings.leaderboardEntriesInterval)).FloatValue);
                leaderboardPlayerEntryCoroutine = null;
            }
        }

        public override void GetEntries(string name, MstJson options, LeaderboardEntriesHandler callback)
        {
            if (leaderboardEntriesCoroutine == null)
            {
                base.GetEntries(name, options, callback);
                leaderboardEntriesCoroutine = StartCoroutine(coroutine());
            }
            else
            {
                callback?.Invoke(Entries);
            }

            IEnumerator coroutine()
            {
                if (options == null || options.Count == 0)
                {
                    options = MstJson.CreateObject();
                    options.AddField("includeUser", true);
                    options.AddField("quantityAround", 5);
                    options.AddField("quantityTop", 20);
                }

                Gb_Yg_GetLeaderboardEntries(name, options.ToString());
                yield return new WaitForSecondsRealtime(Service.Options.GetField(nameof(YandexGameSdkSettings.leaderboardEntriesInterval)).FloatValue);
                leaderboardEntriesCoroutine = null;
            }
        }

        #region WEB_CALLBACK

        protected void Yg_OnSetLeaderboardScore(string json)
        {
            if (!MstJson.IsJson(json))
            {
                Logger.Error("Yandex leaderboard score update returned invalid JSON");
                return;
            }

            var data = new MstJson(json);
            string requestId = data.HasField("requestId")
                ? data["requestId"].StringValue
                : string.Empty;

            if (string.IsNullOrWhiteSpace(requestId) ||
                !scoreCallbacks.TryGetValue(requestId, out SuccessCallback callback))
            {
                return;
            }

            scoreCallbacks.Remove(requestId);
            bool success = data.HasField("success") && data["success"].BoolValue;
            string error = data.HasField("error") ? data["error"].StringValue : string.Empty;
            callback.Invoke(success, error);
        }

        protected void Yg_OnGetLeaderboardDescription(string json)
        {
            var data = new MstJson(json);

            if (!data.HasField("error"))
            {
                var info = new LeaderboardInfo
                {
                    Id = data["name"].StringValue,
                    IsDefault = data.HasField(YandexGamesKeys.Default) && data[YandexGamesKeys.Default].BoolValue,
                    Invert = data["description"]["invert_sort_order"].BoolValue,
                    DecimalOffset = data["description"]["score_format"]["options"]["decimal_offset"].IntValue
                };

                if (Enum.TryParse(data["description"]["score_format"]["type"].StringValue, out LeaderboardType type))
                {
                    info.Type = type;
                }

                Description = info;
                NotifyOnGetLeaderboardInfo(Description);
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
                    IsDefault = data["leaderboard"].HasField(YandexGamesKeys.Default) && data["leaderboard"][YandexGamesKeys.Default].BoolValue,
                    Invert = data["leaderboard"]["description"]["invert_sort_order"].BoolValue,
                    DecimalOffset = data["leaderboard"]["description"]["score_format"]["options"]["decimal_offset"].IntValue,
                    UserRank = data["userRank"].IntValue
                };

                ApplyRanges(data, info);

                if (Enum.TryParse(data["leaderboard"]["description"]["score_format"]["type"].StringValue, out LeaderboardType type))
                {
                    info.Type = type;
                }

                var leaderboardPlayerInfos = new List<LeaderboardPlayerInfo>();

                foreach (var entry in data["entries"])
                {
                    var newEntry = new LeaderboardPlayerInfo
                    {
                        Score = entry["score"].LongValue,
                        Extra = entry["extraData"],
                        Rank = entry["rank"].IntValue,
                        FormatedScore = entry["formattedScore"].StringValue,
                        PlayerAvatar = entry["player"]["avatar"].StringValue,
                        PlayerLang = entry["player"]["lang"].StringValue,
                        PlayerName = entry["player"]["publicName"].StringValue,
                        PlayerId = entry["player"]["uniqueID"].StringValue,
                        IsPlayerAvatarAllowed = IsScopePermissionAllowed(entry["player"], "avatar"),
                        IsPlayerNameAllowed = IsScopePermissionAllowed(entry["player"], "public_name")
                    };

                    leaderboardPlayerInfos.Add(newEntry);
                }

                info.Entries = leaderboardPlayerInfos;
                Entries = info;
                NotifyOnGetLeaderboardEntries(Entries);
            }
            else
            {
                NotifyOnGetLeaderboardEntries(Entries);
            }
        }

        protected void Yg_OnGetLeaderboardPlayerEntry(string json)
        {
            var data = new MstJson(json);

            if (!data.HasField("error"))
            {
                var info = new LeaderboardPlayerInfo
                {
                    Score = data["score"].LongValue,
                    Extra = data["extraData"],
                    Rank = data["rank"].IntValue,
                    FormatedScore = data["formattedScore"].StringValue,
                    PlayerAvatar = data["player"]["avatar"].StringValue,
                    PlayerLang = data["player"]["lang"].StringValue,
                    PlayerName = data["player"]["publicName"].StringValue,
                    PlayerId = data["player"]["uniqueID"].StringValue,
                    IsPlayerAvatarAllowed = IsScopePermissionAllowed(data["player"], "avatar"),
                    IsPlayerNameAllowed = IsScopePermissionAllowed(data["player"], "public_name")
                };

                PlayerEntry = info;
                NotifyOnGetLeaderboardPlayerInfo(PlayerEntry);
            }
            else
            {
                NotifyOnGetLeaderboardPlayerInfo(null);
            }
        }

        private static bool IsScopePermissionAllowed(MstJson player, string permission)
        {
            return player != null &&
                   player.HasField("scopePermissions") &&
                   player["scopePermissions"].HasField(permission) &&
                   player["scopePermissions"][permission].StringValue == "allow";
        }

        private static void ApplyRanges(MstJson data, LeaderboardEntries info)
        {
            MstJson ranges = data["ranges"];

            if (ranges == null)
                return;

            if (!ranges.IsArray)
            {
                info.Start = ranges["start"].IntValue;
                info.Size = ranges["size"].IntValue;
                return;
            }

            int totalSize = 0;

            if (ranges.Count > 0 && ranges[0] != null)
                info.Start = ranges[0]["start"].IntValue;

            for (int i = 0; i < ranges.Count; i++)
            {
                if (ranges[i] != null && ranges[i].HasField("size"))
                    totalSize += ranges[i]["size"].IntValue;
            }

            info.Size = totalSize > 0
                ? totalSize
                : (data.HasField("entries") ? data["entries"].Count : 0);
        }

        #endregion
    }
}
