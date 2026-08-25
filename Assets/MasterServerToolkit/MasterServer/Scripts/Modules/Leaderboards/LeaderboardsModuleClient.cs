using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Reports the result of a leaderboard request.
    /// </summary>
    /// <typeparam name="T">Successful response packet type.</typeparam>
    /// <param name="result">Successful response, or <c>null</c> when the request failed.</param>
    /// <param name="error">Localized failure description, or an empty string on success.</param>
    public delegate void LeaderboardResultCallback<T>(T result, string error);

    /// <summary>
    /// Client API for reading leaderboards and submitting scores where client submission is enabled.
    /// </summary>
    public class LeaderboardsModuleClient : MstBaseClient
    {
        private const int REQUEST_CACHE_LIFETIME_SECONDS = 60;

        private readonly object cacheSync = new object();
        private readonly Dictionary<LeaderboardRequestCacheKey,
            CachedLeaderboardResponse<LeaderboardEntriesPacket>> entriesCache = new();
        private readonly Dictionary<LeaderboardRequestCacheKey,
            CachedLeaderboardResponse<LeaderboardEntry>> playerEntryCache = new();

        /// <summary>
        /// Creates a leaderboard client bound to the supplied connection.
        /// </summary>
        /// <param name="connection">Connection used for leaderboard requests.</param>
        public LeaderboardsModuleClient(IClientSocket connection) : base(connection)
        {
            RegisterErrors(
                MstErrorCodes.LEADERBOARD_ACCOUNT_NOT_FOUND,
                MstErrorCodes.LEADERBOARD_CLIENT_SUBMISSION_FORBIDDEN,
                MstErrorCodes.LEADERBOARD_DATABASE_UNAVAILABLE,
                MstErrorCodes.LEADERBOARD_ENTRY_NOT_FOUND,
                MstErrorCodes.LEADERBOARD_GUEST_SUBMISSION_FORBIDDEN,
                MstErrorCodes.LEADERBOARD_KEY_REQUIRED,
                MstErrorCodes.LEADERBOARD_NOT_ACTIVE,
                MstErrorCodes.LEADERBOARD_NOT_FOUND,
                MstErrorCodes.LEADERBOARD_REQUEST_INVALID,
                MstErrorCodes.LEADERBOARD_RESPONSE_INVALID,
                MstErrorCodes.LEADERBOARD_SCORE_OUT_OF_RANGE,
                MstErrorCodes.LEADERBOARD_SUBMISSION_FAILED,
                MstErrorCodes.USER_IS_NOT_LOGGED_IN);
        }

        /// <summary>
        /// Gets every configured leaderboard season through the current connection.
        /// </summary>
        /// <param name="callback">Request result callback.</param>
        public void GetLeaderboards(
            LeaderboardResultCallback<LeaderboardDefinitionsPacket> callback)
        {
            GetLeaderboards(callback, Connection);
        }

        /// <summary>
        /// Gets every configured leaderboard season through the specified connection.
        /// </summary>
        /// <param name="callback">Request result callback.</param>
        /// <param name="connection">Connection used for the request.</param>
        public void GetLeaderboards(
            LeaderboardResultCallback<LeaderboardDefinitionsPacket> callback,
            IClientSocket connection)
        {
            SendRequest(MstOpCodes.ClientGetLeaderboards, null, callback, connection);
        }

        /// <summary>
        /// Gets one sorted leaderboard page through the current connection.
        /// </summary>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="offset">Zero-based number of entries to skip.</param>
        /// <param name="limit">Requested page size; values less than one use the master default.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the current season.</param>
        public void GetEntries(
            string key,
            LeaderboardResultCallback<LeaderboardEntriesPacket> callback,
            int offset = 0,
            int limit = 0,
            string seasonId = "")
        {
            GetEntries(key, callback, offset, limit, seasonId, Connection);
        }

        /// <summary>
        /// Gets one sorted leaderboard page through the specified connection.
        /// </summary>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="offset">Zero-based number of entries to skip.</param>
        /// <param name="limit">Requested page size.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the current season.</param>
        /// <param name="connection">Connection used for the request.</param>
        public void GetEntries(
            string key,
            LeaderboardResultCallback<LeaderboardEntriesPacket> callback,
            int offset,
            int limit,
            string seasonId,
            IClientSocket connection)
        {
            var request = new LeaderboardEntriesRequestPacket
            {
                Key = key,
                SeasonId = seasonId,
                Offset = offset,
                Limit = limit
            };
            LeaderboardRequestCacheKey cacheKey = CreateCacheKey(
                MstOpCodes.ClientGetLeaderboardEntries,
                key,
                seasonId,
                offset,
                limit,
                connection);

            SendCachedRequest(MstOpCodes.ClientGetLeaderboardEntries,
                request, callback, connection, cacheKey, entriesCache);
        }

        /// <summary>
        /// Gets a page centered around the authenticated account.
        /// </summary>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="limit">Requested page size; values less than one use the master default.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the current season.</param>
        public void GetEntriesAroundMe(
            string key,
            LeaderboardResultCallback<LeaderboardEntriesPacket> callback,
            int limit = 0,
            string seasonId = "")
        {
            GetEntriesAroundMe(key, callback, limit, seasonId, Connection);
        }

        /// <summary>
        /// Gets a page centered around the authenticated account through the specified connection.
        /// </summary>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="limit">Requested page size.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the current season.</param>
        /// <param name="connection">Connection used for the request.</param>
        public void GetEntriesAroundMe(
            string key,
            LeaderboardResultCallback<LeaderboardEntriesPacket> callback,
            int limit,
            string seasonId,
            IClientSocket connection)
        {
            var request = new LeaderboardAroundPlayerRequestPacket
            {
                Key = key,
                SeasonId = seasonId,
                Limit = limit
            };
            LeaderboardRequestCacheKey cacheKey = CreateCacheKey(
                MstOpCodes.ClientGetLeaderboardAroundMe,
                key,
                seasonId,
                0,
                limit,
                connection);

            SendCachedRequest(MstOpCodes.ClientGetLeaderboardAroundMe,
                request, callback, connection, cacheKey, entriesCache);
        }

        /// <summary>
        /// Gets the authenticated account entry and current rank.
        /// </summary>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the current season.</param>
        public void GetMyEntry(
            string key,
            LeaderboardResultCallback<LeaderboardEntry> callback,
            string seasonId = "")
        {
            GetMyEntry(key, callback, seasonId, Connection);
        }

        /// <summary>
        /// Gets the authenticated account entry through the specified connection.
        /// </summary>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the current season.</param>
        /// <param name="connection">Connection used for the request.</param>
        public void GetMyEntry(
            string key,
            LeaderboardResultCallback<LeaderboardEntry> callback,
            string seasonId,
            IClientSocket connection)
        {
            var request = new LeaderboardEntryRequestPacket
            {
                Key = key,
                SeasonId = seasonId
            };
            LeaderboardRequestCacheKey cacheKey = CreateCacheKey(
                MstOpCodes.ClientGetLeaderboardEntry,
                key,
                seasonId,
                0,
                0,
                connection);

            SendCachedRequest(MstOpCodes.ClientGetLeaderboardEntry,
                request, callback, connection, cacheKey, playerEntryCache);
        }

        /// <summary>
        /// Submits the authenticated account score when client submissions are enabled.
        /// </summary>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="score">Signed 64-bit score in the definition's fixed-point units.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the active season.</param>
        public void SubmitScore(
            string key,
            long score,
            LeaderboardResultCallback<LeaderboardSubmitResultPacket> callback,
            string seasonId = "")
        {
            SubmitScore(key, score, callback, seasonId, Connection);
        }

        /// <summary>
        /// Submits the authenticated account score through the specified connection.
        /// </summary>
        /// <param name="key">Stable leaderboard key.</param>
        /// <param name="score">Signed 64-bit score in the definition's fixed-point units.</param>
        /// <param name="callback">Request result callback.</param>
        /// <param name="seasonId">Season identifier, or empty to let the master resolve the active season.</param>
        /// <param name="connection">Connection used for the request.</param>
        public void SubmitScore(
            string key,
            long score,
            LeaderboardResultCallback<LeaderboardSubmitResultPacket> callback,
            string seasonId,
            IClientSocket connection)
        {
            SendRequest<LeaderboardSubmitResultPacket>(
                MstOpCodes.ClientSubmitLeaderboardScore,
                new LeaderboardSubmitScorePacket
                {
                    Key = key,
                    SeasonId = seasonId,
                    Score = score
                }, (result, error) =>
                {
                    if (result != null)
                        InvalidateLeaderboard(key, connection);

                    callback?.Invoke(result, error);
                }, connection);
        }

        protected override void OnConnectionStatusChanged(
            ConnectionStatus status)
        {
            ClearCache();
        }

        private void ClearCache()
        {
            lock (cacheSync)
            {
                entriesCache.Clear();
                playerEntryCache.Clear();
            }
        }

        private void SendCachedRequest<TResponse>(
            ushort opCode,
            SerializablePacket request,
            LeaderboardResultCallback<TResponse> callback,
            IClientSocket connection,
            LeaderboardRequestCacheKey cacheKey,
            Dictionary<LeaderboardRequestCacheKey,
                CachedLeaderboardResponse<TResponse>> cache)
            where TResponse : SerializablePacket, new()
        {
            CachedLeaderboardResponse<TResponse> cacheEntry;
            TResponse cachedResult = null;
            string cachedError = string.Empty;
            bool useCachedResponse = false;
            DateTime now = DateTime.UtcNow;

            lock (cacheSync)
            {
                RemoveExpiredEntries(cache, now);

                if (cache.TryGetValue(cacheKey, out cacheEntry))
                {
                    if (cacheEntry.IsPending)
                    {
                        if (callback != null)
                            cacheEntry.Callbacks.Add(callback);

                        return;
                    }

                    cachedResult = cacheEntry.Result;
                    cachedError = cacheEntry.Error;
                    useCachedResponse = true;
                }
                else
                {
                    cacheEntry = new CachedLeaderboardResponse<TResponse>
                    {
                        IsPending = true
                    };

                    if (callback != null)
                        cacheEntry.Callbacks.Add(callback);

                    cache.Add(cacheKey, cacheEntry);
                }
            }

            if (useCachedResponse)
            {
                callback?.Invoke(cachedResult, cachedError);
                return;
            }

            SendRequest<TResponse>(opCode, request,
                (result, error) => CompleteCachedRequest(
                    cacheEntry, result, error), connection);
        }

        private void CompleteCachedRequest<TResponse>(
            CachedLeaderboardResponse<TResponse> cacheEntry,
            TResponse result,
            string error)
            where TResponse : SerializablePacket
        {
            List<LeaderboardResultCallback<TResponse>> callbacks;

            lock (cacheSync)
            {
                cacheEntry.Result = result;
                cacheEntry.Error = error ?? string.Empty;
                cacheEntry.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(
                    REQUEST_CACHE_LIFETIME_SECONDS);
                cacheEntry.IsPending = false;
                callbacks = new List<LeaderboardResultCallback<TResponse>>(
                    cacheEntry.Callbacks);
                cacheEntry.Callbacks.Clear();
            }

            foreach (LeaderboardResultCallback<TResponse> callback in callbacks)
            {
                try
                {
                    callback?.Invoke(result, cacheEntry.Error);
                }
                catch (Exception exception)
                {
                    Logs.Error(
                        $"Leaderboard response callback failed: {exception}");
                }
            }
        }

        private static void RemoveExpiredEntries<TResponse>(
            Dictionary<LeaderboardRequestCacheKey,
                CachedLeaderboardResponse<TResponse>> cache,
            DateTime now)
            where TResponse : SerializablePacket
        {
            List<LeaderboardRequestCacheKey> expiredKeys = null;

            foreach (KeyValuePair<LeaderboardRequestCacheKey,
                         CachedLeaderboardResponse<TResponse>> entry in cache)
            {
                if (entry.Value.IsPending || entry.Value.ExpiresAtUtc > now)
                    continue;

                expiredKeys ??= new List<LeaderboardRequestCacheKey>();
                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
                return;

            foreach (LeaderboardRequestCacheKey key in expiredKeys)
                cache.Remove(key);
        }

        private void InvalidateLeaderboard(string leaderboardKey,
            IClientSocket connection)
        {
            string connectionId = connection?.Id ?? string.Empty;

            lock (cacheSync)
            {
                RemoveLeaderboardEntries(entriesCache, leaderboardKey,
                    connectionId);
                RemoveLeaderboardEntries(playerEntryCache, leaderboardKey,
                    connectionId);
            }
        }

        private static void RemoveLeaderboardEntries<TResponse>(
            Dictionary<LeaderboardRequestCacheKey,
                CachedLeaderboardResponse<TResponse>> cache,
            string leaderboardKey,
            string connectionId)
            where TResponse : SerializablePacket
        {
            List<LeaderboardRequestCacheKey> keys = null;

            foreach (LeaderboardRequestCacheKey key in cache.Keys)
            {
                if (!string.Equals(key.LeaderboardKey, leaderboardKey,
                        StringComparison.Ordinal) ||
                    !string.Equals(key.ConnectionId, connectionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                keys ??= new List<LeaderboardRequestCacheKey>();
                keys.Add(key);
            }

            if (keys == null)
                return;

            foreach (LeaderboardRequestCacheKey key in keys)
                cache.Remove(key);
        }

        private static LeaderboardRequestCacheKey CreateCacheKey(
            ushort opCode,
            string leaderboardKey,
            string seasonId,
            int offset,
            int limit,
            IClientSocket connection)
        {
            string accountId = ReferenceEquals(connection, Mst.Connection)
                ? Mst.Client?.Auth?.Account?.Id ?? string.Empty
                : string.Empty;

            return new LeaderboardRequestCacheKey(opCode,
                leaderboardKey, seasonId, offset, limit,
                connection?.Id, accountId);
        }

        private static void SendRequest<TResponse>(
            ushort opCode,
            SerializablePacket request,
            LeaderboardResultCallback<TResponse> callback,
            IClientSocket connection)
            where TResponse : SerializablePacket, new()
        {
            int completionState = 0;

            void Complete(TResponse result, string error)
            {
                if (Interlocked.Exchange(ref completionState, 1) != 0)
                    return;

                callback?.Invoke(result, error);
            }

            if (connection == null || !connection.IsConnected)
            {
                Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            try
            {
                ResponseCallback responseCallback = (status, response) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        Complete(null, Mst.Errors.Parse(status, response));
                        return;
                    }

                    if (response == null || !response.HasData)
                    {
                        Complete(null, Mst.Errors.Parse(
                            ResponseStatus.DependencyError,
                            CreateErrorProperties(MstErrorCodes.LEADERBOARD_RESPONSE_INVALID)));
                        return;
                    }

                    TResponse result;

                    try
                    {
                        result = response.AsPacket<TResponse>();
                    }
                    catch (Exception exception)
                    {
                        Logs.Error($"Failed to decode leaderboard response: {exception}");
                        Complete(null, Mst.Errors.Parse(
                            ResponseStatus.DependencyError,
                            CreateErrorProperties(MstErrorCodes.LEADERBOARD_RESPONSE_INVALID)));
                        return;
                    }

                    Complete(result, string.Empty);
                };

                if (request == null)
                    connection.SendMessage(opCode, responseCallback);
                else
                    connection.SendMessage(opCode, request, responseCallback);
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to send leaderboard request: {exception}");
                Complete(null, Mst.Errors.Parse(
                    connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected));
            }
        }

        private static MstProperties CreateErrorProperties(string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return properties;
        }

        private sealed class CachedLeaderboardResponse<TResponse>
            where TResponse : SerializablePacket
        {
            public bool IsPending { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
            public TResponse Result { get; set; }
            public string Error { get; set; } = string.Empty;
            public List<LeaderboardResultCallback<TResponse>> Callbacks { get; } =
                new List<LeaderboardResultCallback<TResponse>>();
        }

        private readonly struct LeaderboardRequestCacheKey :
            IEquatable<LeaderboardRequestCacheKey>
        {
            public ushort OpCode { get; }
            public string LeaderboardKey { get; }
            public string SeasonId { get; }
            public int Offset { get; }
            public int Limit { get; }
            public string ConnectionId { get; }
            public string AccountId { get; }

            public LeaderboardRequestCacheKey(ushort opCode,
                string leaderboardKey, string seasonId, int offset,
                int limit, string connectionId, string accountId)
            {
                OpCode = opCode;
                LeaderboardKey = leaderboardKey ?? string.Empty;
                SeasonId = seasonId ?? string.Empty;
                Offset = offset;
                Limit = limit;
                ConnectionId = connectionId ?? string.Empty;
                AccountId = accountId ?? string.Empty;
            }

            public bool Equals(LeaderboardRequestCacheKey other)
            {
                return OpCode == other.OpCode &&
                    Offset == other.Offset &&
                    Limit == other.Limit &&
                    string.Equals(LeaderboardKey, other.LeaderboardKey,
                        StringComparison.Ordinal) &&
                    string.Equals(SeasonId, other.SeasonId,
                        StringComparison.Ordinal) &&
                    string.Equals(ConnectionId, other.ConnectionId,
                        StringComparison.Ordinal) &&
                    string.Equals(AccountId, other.AccountId,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LeaderboardRequestCacheKey other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = OpCode.GetHashCode();
                    hashCode = (hashCode * 397) ^ Offset;
                    hashCode = (hashCode * 397) ^ Limit;
                    hashCode = (hashCode * 397) ^
                        StringComparer.Ordinal.GetHashCode(LeaderboardKey);
                    hashCode = (hashCode * 397) ^
                        StringComparer.Ordinal.GetHashCode(SeasonId);
                    hashCode = (hashCode * 397) ^
                        StringComparer.Ordinal.GetHashCode(ConnectionId);
                    hashCode = (hashCode * 397) ^
                        StringComparer.Ordinal.GetHashCode(AccountId);
                    return hashCode;
                }
            }
        }
    }
}
