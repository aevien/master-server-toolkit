using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public partial class SelfService : BaseGameService
    {
        public override void GetLeaderboardEntries(string name, MstJson options, LeaderboardEntriesHandler callback)
        {
            if (useFakeData)
            {
                if (LeaderboardEntries.Entries.Count == 0)
                {
                    int playerRank = 123;
                    var leaderboardEntries = new LeaderboardEntries
                    {
                        Id = "fakeLeaderboard",
                        Title = "Fake Leaderboard",
                        IsDefault = true,
                        Type = LeaderboardType.Numeric,
                        UserRank = playerRank,
                        Start = 0,
                        Size = 10
                    };

                    int totalScore = 10_000;

                    for (int i = 0; i < leaderboardEntries.Size; i++)
                    {
                        totalScore -= 1000;
                        int currentScore = totalScore + Random.Range(0, 1000);

                        var entry = new LeaderboardPlayerInfo
                        {
                            Score = currentScore,
                            FormatedScore = currentScore.ToString(),
                            Rank = i + 1,
                            PlayerId = i < 9 ? Mst.Helper.CreateGuidString() : Player.Id,
                            PlayerAvatar = i < 9 ? $"https://avatar.iran.liara.run/public/{i + 1}" : Player.Avatar,
                            PlayerLang = "en",
                            PlayerName = i < 9 ? SimpleNameGenerator.Generate(Gender.Male) : Player.Name,
                            IsPlayerAvatarAllowed = true,
                            IsPlayerNameAllowed = true
                        };

                        leaderboardEntries.Entries.Add(entry);
                    }

                    LeaderboardEntries = leaderboardEntries;
                }

                callback?.Invoke(LeaderboardEntries);
            }
            else
            {
                base.GetLeaderboardEntries(name, options, callback);
            }
        }
    }
}
