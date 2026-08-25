using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;

#if VKPLAY_STEAMWORKS
using Steamworks;
#endif

namespace MasterServerToolkit.GameService
{
    public class VkPlayLeaderboardsModule : BaseLeaderboardsModule
    {
#if VKPLAY_STEAMWORKS
        private const int DefaultEntriesCount = 20;

        private readonly Dictionary<string, SteamLeaderboard_t> leaderboardHandles = new();
        private readonly List<PendingLeaderboardOperation> pendingOperations = new();
#endif

        public override void OnInit(IService service)
        {
            base.OnInit(service);

#if VKPLAY_STEAMWORKS
            IsSupported = service is VkPlayService vkPlayService && vkPlayService.IsSteamApiInitialized;

            if (!IsSupported)
                Logger.Warn("VK Play leaderboards are not available because Steam API emulation is not initialized.");
#else
            IsSupported = false;
#endif

            IsReady = true;
        }

        public override void SetScore(
            string name,
            long score,
            MstJson extra,
            SuccessCallback callback = null)
        {
            if (!CanUseLeaderboards())
            {
                callback?.Invoke(false, "leaderboards_not_available");
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                callback?.Invoke(false, "leaderboard_name_required");
                return;
            }

            if (score < int.MinValue || score > int.MaxValue)
            {
                callback?.Invoke(false, "leaderboard_score_out_of_range");
                return;
            }

#if VKPLAY_STEAMWORKS
            FindOrCreateLeaderboard(
                name,
                handle => UploadScore(name, handle, (int)score, callback),
                () =>
                {
                    Logger.Warn($"VK Play leaderboard was not found. Name={name}");
                    callback?.Invoke(false, "leaderboard_not_found");
                });
#else
            callback?.Invoke(false, "leaderboards_not_supported");
#endif
        }

        public override void GetInfo(string name, LeaderboardInfoHandler callback)
        {
            if (!CanUseLeaderboards())
            {
                callback?.Invoke(null);
                return;
            }

            Description = CreateLeaderboardInfo(name);
            callback?.Invoke(Description);
        }

        public override void GetEntries(string name, MstJson options, LeaderboardEntriesHandler callback)
        {
            if (!CanUseLeaderboards() || string.IsNullOrWhiteSpace(name))
            {
                callback?.Invoke(CreateEmptyEntries(name));
                return;
            }

            base.GetEntries(name, options, callback);

#if VKPLAY_STEAMWORKS
            int count = ReadIntOption(options, "quantityTop", DefaultEntriesCount);
            int start = Math.Max(1, ReadIntOption(options, "start", 1));
            int end = start + Math.Max(1, count) - 1;

            FindOrCreateLeaderboard(
                name,
                handle => DownloadEntries(name, handle, start, end),
                () => NotifyOnGetLeaderboardEntries(CreateEmptyEntries(name)));
#endif
        }

        public override void GetPlayerInfo(string name, LeaderboardPlayerInfoHandler callback)
        {
            if (!CanUseLeaderboards() || string.IsNullOrWhiteSpace(name))
            {
                callback?.Invoke(null);
                return;
            }

            base.GetPlayerInfo(name, callback);

#if VKPLAY_STEAMWORKS
            FindOrCreateLeaderboard(
                name,
                handle => DownloadPlayerEntry(handle),
                () => NotifyOnGetLeaderboardPlayerInfo(null));
#endif
        }

#if VKPLAY_STEAMWORKS
        private void FindOrCreateLeaderboard(string name, Action<SteamLeaderboard_t> onFound, Action onFailed)
        {
            if (leaderboardHandles.TryGetValue(name, out SteamLeaderboard_t cachedHandle))
            {
                onFound?.Invoke(cachedHandle);
                return;
            }

            var operation = CreatePendingOperation();

            operation.FindResult = CallResult<LeaderboardFindResult_t>.Create((result, ioFailure) =>
            {
                CompleteOperation(operation);

                if (ioFailure || result.m_bLeaderboardFound == 0)
                {
                    onFailed?.Invoke();
                    return;
                }

                leaderboardHandles[name] = result.m_hSteamLeaderboard;
                onFound?.Invoke(result.m_hSteamLeaderboard);
            });

            try
            {
                SteamAPICall_t call = SteamUserStats.FindOrCreateLeaderboard(
                    name,
                    ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                    ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeMilliSeconds);

                if (!TrySetCall(operation.FindResult, call))
                {
                    CompleteOperation(operation);
                    onFailed?.Invoke();
                }
            }
            catch (Exception e)
            {
                CompleteOperation(operation);
                Logger.Warn($"VK Play leaderboard lookup failed. Name={name}, Error={GetExceptionMessage(e)}");
                onFailed?.Invoke();
            }
        }

        private void UploadScore(
            string name,
            SteamLeaderboard_t handle,
            int score,
            SuccessCallback callback)
        {
            var operation = CreatePendingOperation();

            operation.UploadResult = CallResult<LeaderboardScoreUploaded_t>.Create((result, ioFailure) =>
            {
                CompleteOperation(operation);

                if (ioFailure || result.m_bSuccess == 0)
                {
                    Logger.Warn($"VK Play leaderboard score upload failed. Name={name}, Score={score}");
                    callback?.Invoke(false, "leaderboard_score_upload_failed");
                    return;
                }

                Logger.Info(
                    $"VK Play leaderboard score uploaded. Name={name}, Score={result.m_nScore}, " +
                    $"Rank={result.m_nGlobalRankNew}, ScoreChanged={result.m_bScoreChanged != 0}");
                callback?.Invoke(true, string.Empty);
            });

            try
            {
                SteamAPICall_t call = SteamUserStats.UploadLeaderboardScore(
                    handle,
                    ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate,
                    score,
                    Array.Empty<int>(),
                    0);

                if (!TrySetCall(operation.UploadResult, call))
                {
                    CompleteOperation(operation);
                    Logger.Warn($"VK Play leaderboard score upload was not started. Name={name}, Score={score}");
                    callback?.Invoke(false, "leaderboard_score_upload_not_started");
                }
            }
            catch (Exception e)
            {
                CompleteOperation(operation);
                Logger.Warn($"VK Play leaderboard score upload failed. Name={name}, Score={score}, Error={GetExceptionMessage(e)}");
                callback?.Invoke(false, GetExceptionMessage(e));
            }
        }

        private void DownloadEntries(string name, SteamLeaderboard_t handle, int start, int end)
        {
            var operation = CreatePendingOperation();

            operation.DownloadResult = CallResult<LeaderboardScoresDownloaded_t>.Create((result, ioFailure) =>
            {
                CompleteOperation(operation);

                if (ioFailure)
                {
                    Logger.Warn($"VK Play leaderboard entries download failed. Name={name}");
                    NotifyOnGetLeaderboardEntries(CreateEmptyEntries(name));
                    return;
                }

                Entries = CreateEntries(name, result, start);
                NotifyOnGetLeaderboardEntries(Entries);
            });

            try
            {
                SteamAPICall_t call = SteamUserStats.DownloadLeaderboardEntries(
                    handle,
                    ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
                    start,
                    end);

                if (!TrySetCall(operation.DownloadResult, call))
                {
                    CompleteOperation(operation);
                    NotifyOnGetLeaderboardEntries(CreateEmptyEntries(name));
                }
            }
            catch (Exception e)
            {
                CompleteOperation(operation);
                Logger.Warn($"VK Play leaderboard entries download failed. Name={name}, Error={GetExceptionMessage(e)}");
                NotifyOnGetLeaderboardEntries(CreateEmptyEntries(name));
            }
        }

        private void DownloadPlayerEntry(SteamLeaderboard_t handle)
        {
            var operation = CreatePendingOperation();

            operation.DownloadResult = CallResult<LeaderboardScoresDownloaded_t>.Create((result, ioFailure) =>
            {
                CompleteOperation(operation);

                if (ioFailure || result.m_cEntryCount <= 0)
                {
                    NotifyOnGetLeaderboardPlayerInfo(null);
                    return;
                }

                int[] details = Array.Empty<int>();

                if (!SteamUserStats.GetDownloadedLeaderboardEntry(result.m_hSteamLeaderboardEntries, 0, out LeaderboardEntry_t entry, details, 0))
                {
                    NotifyOnGetLeaderboardPlayerInfo(null);
                    return;
                }

                PlayerEntry = CreatePlayerInfo(entry);
                NotifyOnGetLeaderboardPlayerInfo(PlayerEntry);
            });

            try
            {
                var users = new[] { SteamUser.GetSteamID() };
                SteamAPICall_t call = SteamUserStats.DownloadLeaderboardEntriesForUsers(handle, users, users.Length);

                if (!TrySetCall(operation.DownloadResult, call))
                {
                    CompleteOperation(operation);
                    NotifyOnGetLeaderboardPlayerInfo(null);
                }
            }
            catch (Exception e)
            {
                CompleteOperation(operation);
                Logger.Warn($"VK Play leaderboard player entry download failed. Error={GetExceptionMessage(e)}");
                NotifyOnGetLeaderboardPlayerInfo(null);
            }
        }

        private LeaderboardEntries CreateEntries(string name, LeaderboardScoresDownloaded_t result, int start)
        {
            var leaderboardEntries = new LeaderboardEntries
            {
                Id = name,
                Title = name,
                Type = LeaderboardType.Time,
                Start = start,
                Size = result.m_cEntryCount
            };

            var entries = new List<LeaderboardPlayerInfo>(result.m_cEntryCount);
            int[] details = Array.Empty<int>();
            CSteamID currentUserId = SteamUser.GetSteamID();

            for (int i = 0; i < result.m_cEntryCount; i++)
            {
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(result.m_hSteamLeaderboardEntries, i, out LeaderboardEntry_t entry, details, 0))
                    continue;

                var playerInfo = CreatePlayerInfo(entry);
                entries.Add(playerInfo);

                if (entry.m_steamIDUser == currentUserId)
                    leaderboardEntries.UserRank = entry.m_nGlobalRank;
            }

            leaderboardEntries.Entries = entries;
            Logger.Info($"VK Play leaderboard entries downloaded. Name={name}, Count={entries.Count}");
            return leaderboardEntries;
        }

        private LeaderboardPlayerInfo CreatePlayerInfo(LeaderboardEntry_t entry)
        {
            bool isCurrentPlayer = entry.m_steamIDUser == SteamUser.GetSteamID();
            string playerId = isCurrentPlayer && Service.Player != null ? Service.Player.Id : entry.m_steamIDUser.ToString();
            string playerName = isCurrentPlayer && Service.Player != null ? Service.Player.Name : ResolvePersonaName(entry.m_steamIDUser);
            string playerAvatar = isCurrentPlayer && Service.Player != null ? Service.Player.Avatar : string.Empty;

            return new LeaderboardPlayerInfo
            {
                Score = entry.m_nScore,
                FormatedScore = FormatTimeScore(entry.m_nScore),
                Extra = MstJson.CreateObject(),
                Rank = entry.m_nGlobalRank,
                PlayerId = playerId,
                PlayerAvatar = playerAvatar,
                PlayerLang = Service.Lang,
                PlayerName = playerName,
                IsPlayerAvatarAllowed = !string.IsNullOrWhiteSpace(playerAvatar),
                IsPlayerNameAllowed = !string.IsNullOrWhiteSpace(playerName)
            };
        }

        private string ResolvePersonaName(CSteamID steamId)
        {
            try
            {
                string personaName = SteamFriends.GetFriendPersonaName(steamId);

                if (!string.IsNullOrWhiteSpace(personaName) && personaName != "[unknown]")
                    return personaName;
            }
            catch (Exception e)
            {
                Logger.Warn($"VK Play leaderboard persona lookup failed. SteamId={steamId}, Error={GetExceptionMessage(e)}");
            }

            return steamId.ToString();
        }

        private PendingLeaderboardOperation CreatePendingOperation()
        {
            var operation = new PendingLeaderboardOperation();
            pendingOperations.Add(operation);
            return operation;
        }

        private void CompleteOperation(PendingLeaderboardOperation operation)
        {
            if (operation == null)
                return;

            operation.FindResult?.Dispose();
            operation.UploadResult?.Dispose();
            operation.DownloadResult?.Dispose();
            pendingOperations.Remove(operation);
        }

        private static bool TrySetCall<T>(CallResult<T> callResult, SteamAPICall_t call)
        {
            if (call == SteamAPICall_t.Invalid)
                return false;

            callResult.Set(call);
            return true;
        }

        private sealed class PendingLeaderboardOperation
        {
            public CallResult<LeaderboardFindResult_t> FindResult { get; set; }
            public CallResult<LeaderboardScoreUploaded_t> UploadResult { get; set; }
            public CallResult<LeaderboardScoresDownloaded_t> DownloadResult { get; set; }
        }
#endif

        private LeaderboardInfo CreateLeaderboardInfo(string name)
        {
            return new LeaderboardInfo
            {
                Id = name,
                Title = name,
                Type = LeaderboardType.Time
            };
        }

        private LeaderboardEntries CreateEmptyEntries(string name)
        {
            return new LeaderboardEntries
            {
                Id = name,
                Title = name,
                Type = LeaderboardType.Time,
                Start = 1,
                Size = 0,
                Entries = Array.Empty<LeaderboardPlayerInfo>()
            };
        }

        private static int ReadIntOption(MstJson options, string fieldName, int defaultValue)
        {
            if (options == null || !options.HasField(fieldName))
                return defaultValue;

            return options[fieldName].IntValue;
        }

        private static string FormatTimeScore(int score)
        {
            TimeSpan value = TimeSpan.FromMilliseconds(Math.Max(0, score));
            int totalHours = (int)value.TotalHours;

            if (totalHours > 0)
                return $"{totalHours}:{value.Minutes:00}:{value.Seconds:00}";

            return $"{value.Minutes}:{value.Seconds:00}";
        }

        private static string GetExceptionMessage(Exception e)
        {
            return e.InnerException?.Message ?? e.Message;
        }

        private bool CanUseLeaderboards()
        {
#if VKPLAY_STEAMWORKS
            return IsSupported && Service is VkPlayService vkPlayService && vkPlayService.IsSteamApiInitialized;
#else
            return false;
#endif
        }
    }
}
