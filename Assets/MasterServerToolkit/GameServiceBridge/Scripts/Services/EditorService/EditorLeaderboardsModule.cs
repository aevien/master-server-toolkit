using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.GameService
{
    public class EditorLeaderboardsModule : BaseLeaderboardsModule
    {
        private const string JsonPlaceholderUsersUrl = "https://jsonplaceholder.typicode.com/users";

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            CreateEditorEntries();
            IsReady = true;
            IsSupported = true;
        }

        public override void SetScore(
            string name,
            long score,
            MstJson extra,
            SuccessCallback callback = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                callback?.Invoke(false, "leaderboard_name_required");
                return;
            }

            callback?.Invoke(true, string.Empty);
        }

        private void CreateEditorEntries()
        {
            CreateEntriesFromUsers(null);
        }

        private void CreateEntriesFromUsers(MstJson usersData)
        {
            int playerRank = 5;
            var leaderboardEntries = new LeaderboardEntries
            {
                Id = "editorLeaderboard",
                Title = "Editor Leaderboard",
                IsDefault = true,
                Type = LeaderboardType.Numeric,
                UserRank = playerRank,
                Start = 0,
                Size = 10
            };

            List<LeaderboardPlayerInfo> entries = new();
            int externalEntriesCount = leaderboardEntries.Size - 1;

            for (int i = 0; i < externalEntriesCount; i++)
            {
                var userData = TryGetUser(usersData, i);
                string playerName = GetUserName(userData);

                if (string.IsNullOrWhiteSpace(playerName))
                    continue;

                long currentScore = GenerateScore(userData, i, playerName);

                var entry = new LeaderboardPlayerInfo
                {
                    Score = currentScore,
                    FormatedScore = currentScore.ToString(),
                    Rank = 0,
                    PlayerId = userData != null && userData.HasField("id") ? userData["id"].StringValue : Mst.Helper.CreateGuidString(),
                    PlayerAvatar = $"https://i.pravatar.cc/300?img={i + 1}",
                    PlayerLang = GetUserLanguage(userData),
                    PlayerName = playerName,
                    IsPlayerAvatarAllowed = true,
                    IsPlayerNameAllowed = true
                };

                entries.Add(entry);
            }

            int playerScore = 0;
            entries.Add(new LeaderboardPlayerInfo
            {
                Score = playerScore,
                FormatedScore = playerScore.ToString(),
                Rank = playerRank,
                PlayerId = Service.Player.Id,
                PlayerAvatar = Service.Player.Avatar,
                PlayerLang = "en",
                PlayerName = Service.Player.Name,
                IsPlayerAvatarAllowed = true,
                IsPlayerNameAllowed = true
            });

            entries.Sort((left, right) => right.Score.CompareTo(left.Score));

            if (entries.Count >= playerRank)
            {
                int targetIndex = playerRank - 1;
                long scoreAbove = targetIndex > 0 ? entries[targetIndex - 1].Score : entries[targetIndex].Score + 100;
                long scoreBelow = targetIndex < entries.Count - 1 ? entries[targetIndex].Score : scoreAbove - 100;

                if (scoreAbove <= scoreBelow)
                    scoreBelow = scoreAbove - 100;

                var playerEntry = entries.Find(i => i.PlayerId == Service.Player.Id);

                if (playerEntry != null)
                {
                    playerEntry.Score = scoreAbove - Math.Max(1L,
                        (scoreAbove - scoreBelow) / 2L);
                    playerEntry.FormatedScore = playerEntry.Score.ToString();

                    entries.Remove(playerEntry);
                    entries.Insert(targetIndex, playerEntry);
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Rank = entries[i].PlayerId == Service.Player.Id ? playerRank : i + 1;
                entries[i].FormatedScore = entries[i].Score.ToString();
            }

            leaderboardEntries.Entries = entries;
            Entries = leaderboardEntries;
        }

        public override void GetEntries(string name, MstJson options, LeaderboardEntriesHandler callback)
        {
            base.GetEntries(name, options, callback);
            StartCoroutine(GetEntriesFromApi());
        }

        private IEnumerator GetEntriesFromApi()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(JsonPlaceholderUsersUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        CreateEntriesFromUsers(new MstJson(request.downloadHandler.text));
                        NotifyOnGetLeaderboardEntries(Entries);
                        yield break;
                    }
                    catch (Exception exception)
                    {
                        Logger.Error($"Failed to parse leaderboard users from jsonplaceholder. Error: {exception.Message}");
                    }
                }
                else
                {
                    Logger.Warn($"Failed to load leaderboard users from jsonplaceholder. Error: {request.error}");
                }
            }

            NotifyOnGetLeaderboardEntries(Entries);
        }

        private static MstJson TryGetUser(MstJson usersData, int index)
        {
            if (usersData == null || usersData.Count <= index)
                return null;

            return usersData[index];
        }

        private static string GetUserName(MstJson userData)
        {
            if (userData != null)
            {
                if (userData.HasField("username") && !string.IsNullOrWhiteSpace(userData["username"].StringValue))
                    return userData["username"].StringValue;

                if (userData.HasField("name") && !string.IsNullOrWhiteSpace(userData["name"].StringValue))
                    return userData["name"].StringValue;
            }

            return SimpleNameGenerator.Generate(Gender.Male);
        }

        private static string GetUserLanguage(MstJson userData)
        {
            if (userData != null && userData.HasField("address") && userData["address"].HasField("city"))
            {
                string city = userData["address"]["city"].StringValue.ToLowerInvariant();

                if (city.Contains("south") || city.Contains("aliyaview"))
                    return "tr";
            }

            return "en";
        }

        private static int GenerateScore(MstJson userData, int index, string playerName)
        {
            int idScore = 0;
            int companyScore = 0;
            int cityScore = 0;

            if (userData != null)
            {
                if (userData.HasField("id"))
                    idScore = userData["id"].IntValue * 137;

                if (userData.HasField("company") && userData["company"].HasField("name"))
                    companyScore = userData["company"]["name"].StringValue.Length * 29;

                if (userData.HasField("address") && userData["address"].HasField("city"))
                    cityScore = userData["address"]["city"].StringValue.Length * 17;
            }

            int nameScore = playerName.Length * 53;
            return 1500 + idScore + companyScore + cityScore + (index * 211) + nameScore;
        }
    }
}
