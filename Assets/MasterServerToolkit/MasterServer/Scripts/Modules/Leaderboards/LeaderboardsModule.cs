using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Authoritative cross-platform leaderboard module hosted by the master server.
    /// </summary>
    public class LeaderboardsModule : BaseServerModule
    {
        [Header("Settings")]
        [SerializeField, Tooltip("Factory that creates and registers the leaderboard database accessor. The module cannot read or submit scores when the accessor is unavailable.")]
        protected DatabaseAccessorFactory databaseAccessorFactory;

        [SerializeField, Tooltip("Leaderboard and season definitions available to clients and trusted room servers. Entries with invalid or duplicate Key and Season Id combinations are ignored during initialization.")]
        protected List<LeaderboardDefinition> leaderboards = new List<LeaderboardDefinition>();

        [SerializeField, Min(1), Tooltip("Number of entries returned when a request supplies 0 or a negative page size. Must not exceed Maximum Page Size.")]
        protected int defaultPageSize = 20;

        [SerializeField, Min(1), Tooltip("Largest number of entries returned by one leaderboard request. Larger client values are reduced to this limit to bound database and network work.")]
        protected int maximumPageSize = 100;

        protected AuthModule authModule;
        protected ILeaderboardsDatabaseAccessor databaseAccessor;

        private readonly Dictionary<string, LeaderboardDefinition> definitionsByIdentity =
            new Dictionary<string, LeaderboardDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<LeaderboardDefinition>> definitionsByKey =
            new Dictionary<string, List<LeaderboardDefinition>>(StringComparer.Ordinal);

        /// <summary>
        /// Database accessor used by this module.
        /// </summary>
        public ILeaderboardsDatabaseAccessor DatabaseAccessor
        {
            get => databaseAccessor;
            set => databaseAccessor = value;
        }

        /// <summary>
        /// Current UTC time. Overridable for deterministic tests.
        /// </summary>
        protected virtual DateTime UtcNow => DateTime.UtcNow;

        protected override void Awake()
        {
            base.Awake();
            AddDependency<AuthModule>();
        }

        public override void Initialize(IServer server)
        {
            authModule = server.GetModule<AuthModule>();
            CacheDefinitions();

            if (databaseAccessorFactory != null)
                databaseAccessorFactory.CreateAccessors();

            if (databaseAccessor == null)
                databaseAccessor = Mst.Server.DbAccessors.GetAccessor<ILeaderboardsDatabaseAccessor>();

            if (databaseAccessor == null)
                logger.Error($"{nameof(ILeaderboardsDatabaseAccessor)} is not registered. Leaderboards are unavailable.");

            server.RegisterMessageHandler(MstOpCodes.ClientGetLeaderboards, GetLeaderboardsRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientGetLeaderboardEntries, GetEntriesRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientGetLeaderboardAroundMe, GetEntriesAroundMeRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientGetLeaderboardEntry, GetMyEntryRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientSubmitLeaderboardScore, ClientSubmitScoreRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerSubmitLeaderboardScore, ServerSubmitScoreRequestHandler);
        }

        /// <summary>
        /// Rebuilds the runtime definition cache from inspector data.
        /// </summary>
        protected void CacheDefinitions()
        {
            definitionsByIdentity.Clear();
            definitionsByKey.Clear();
            maximumPageSize = Math.Max(1, maximumPageSize);
            defaultPageSize = Math.Min(Math.Max(1, defaultPageSize), maximumPageSize);

            if (leaderboards == null)
                return;

            foreach (LeaderboardDefinition definition in leaderboards)
            {
                if (definition == null)
                    continue;

                if (!definition.TryValidate(out string error))
                {
                    logger.Error(error);
                    continue;
                }

                string identity = CreateDefinitionIdentity(definition.Key, definition.SeasonId);

                if (definitionsByIdentity.ContainsKey(identity))
                {
                    logger.Error($"Duplicate leaderboard definition ignored: key={definition.Key}, season={definition.SeasonId}");
                    continue;
                }

                if (!definitionsByKey.TryGetValue(definition.Key, out List<LeaderboardDefinition> seasons))
                {
                    seasons = new List<LeaderboardDefinition>();
                    definitionsByKey.Add(definition.Key, seasons);
                }

                if (seasons.Any(existing => PeriodsOverlap(existing, definition)))
                {
                    logger.Error($"Overlapping leaderboard season ignored: key={definition.Key}, season={definition.SeasonId}");
                    continue;
                }

                definitionsByIdentity.Add(identity, definition);
                seasons.Add(definition);
            }
        }

        protected virtual Task GetLeaderboardsRequestHandler(IIncomingMessage message)
        {
            var response = new LeaderboardDefinitionsPacket();
            DateTime utcNow = UtcNow;

            foreach (LeaderboardDefinition definition in definitionsByIdentity.Values
                         .OrderBy(item => item.Key, StringComparer.Ordinal)
                         .ThenBy(item => item.SeasonId, StringComparer.Ordinal))
            {
                response.Items.Add(LeaderboardDefinitionPacket.FromDefinition(definition, utcNow));
            }

            RespondIfExpected(message, response);
            return Task.CompletedTask;
        }

        protected virtual async Task GetEntriesRequestHandler(
            IIncomingMessage message, CancellationToken cancellationToken)
        {
            try
            {
                LeaderboardEntriesRequestPacket request =
                    message.AsPacket<LeaderboardEntriesRequestPacket>();

                if (!TryResolveDefinition(request.Key, request.SeasonId,
                        out LeaderboardDefinition definition, out ResponseStatus status, out string errorCode))
                {
                    RespondDefinitionError(message, status, errorCode, request.Key, request.SeasonId);
                    return;
                }

                if (!TryGetDatabase(message, out ILeaderboardsDatabaseAccessor accessor))
                    return;

                int offset = Math.Max(0, request.Offset);
                int limit = NormalizePageSize(request.Limit);
                string accountId = GetAuthenticatedUser(message.Peer)?.UserId;

                LeaderboardEntriesPacket response = await CreateEntriesPacketAsync(
                    accessor, definition, offset, limit, accountId, cancellationToken)
                    .ConfigureAwait(false);

                RespondIfExpected(message, response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Error($"Leaderboard entries request failed: {exception}");
                RespondErrorIfExpected(message, ResponseStatus.Error,
                    MstErrorCodes.LEADERBOARD_REQUEST_INVALID);
            }
        }

        protected virtual async Task GetEntriesAroundMeRequestHandler(
            IIncomingMessage message, CancellationToken cancellationToken)
        {
            try
            {
                IUserPeerExtension user = GetAuthenticatedUser(message.Peer);

                if (user == null)
                {
                    RespondErrorIfExpected(message, ResponseStatus.Unauthorized,
                        MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                    return;
                }

                LeaderboardAroundPlayerRequestPacket request =
                    message.AsPacket<LeaderboardAroundPlayerRequestPacket>();

                if (!TryResolveDefinition(request.Key, request.SeasonId,
                        out LeaderboardDefinition definition, out ResponseStatus status, out string errorCode))
                {
                    RespondDefinitionError(message, status, errorCode, request.Key, request.SeasonId);
                    return;
                }

                if (!TryGetDatabase(message, out ILeaderboardsDatabaseAccessor accessor))
                    return;

                LeaderboardEntry ownEntry = await accessor.GetEntryAsync(
                    definition.Key, definition.SeasonId, user.UserId, cancellationToken)
                    .ConfigureAwait(false);

                if (ownEntry == null)
                {
                    RespondDefinitionError(message, ResponseStatus.NotFound,
                        MstErrorCodes.LEADERBOARD_ENTRY_NOT_FOUND, definition.Key, definition.SeasonId);
                    return;
                }

                long betterEntries = await accessor.CountBetterEntriesAsync(
                    definition.Key, definition.SeasonId, definition.SortOrder,
                    ownEntry.Score, ownEntry.AccountId, cancellationToken).ConfigureAwait(false);
                long rank = betterEntries + 1L;
                int limit = NormalizePageSize(request.Limit);
                long requestedOffset = Math.Max(0L, rank - 1L - limit / 2L);
                int offset = requestedOffset > int.MaxValue ? int.MaxValue : (int)requestedOffset;

                LeaderboardEntriesPacket response = await CreateEntriesPacketAsync(
                    accessor, definition, offset, limit, user.UserId, cancellationToken)
                    .ConfigureAwait(false);

                RespondIfExpected(message, response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Error($"Leaderboard around-player request failed: {exception}");
                RespondErrorIfExpected(message, ResponseStatus.Error,
                    MstErrorCodes.LEADERBOARD_REQUEST_INVALID);
            }
        }

        protected virtual async Task GetMyEntryRequestHandler(
            IIncomingMessage message, CancellationToken cancellationToken)
        {
            try
            {
                IUserPeerExtension user = GetAuthenticatedUser(message.Peer);

                if (user == null)
                {
                    RespondErrorIfExpected(message, ResponseStatus.Unauthorized,
                        MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                    return;
                }

                LeaderboardEntryRequestPacket request =
                    message.AsPacket<LeaderboardEntryRequestPacket>();

                if (!TryResolveDefinition(request.Key, request.SeasonId,
                        out LeaderboardDefinition definition, out ResponseStatus status, out string errorCode))
                {
                    RespondDefinitionError(message, status, errorCode, request.Key, request.SeasonId);
                    return;
                }

                if (!TryGetDatabase(message, out ILeaderboardsDatabaseAccessor accessor))
                    return;

                LeaderboardEntry entry = await accessor.GetEntryAsync(
                    definition.Key, definition.SeasonId, user.UserId, cancellationToken)
                    .ConfigureAwait(false);

                if (entry == null)
                {
                    RespondDefinitionError(message, ResponseStatus.NotFound,
                        MstErrorCodes.LEADERBOARD_ENTRY_NOT_FOUND, definition.Key, definition.SeasonId);
                    return;
                }

                entry.Rank = await accessor.CountBetterEntriesAsync(
                    definition.Key, definition.SeasonId, definition.SortOrder,
                    entry.Score, entry.AccountId, cancellationToken).ConfigureAwait(false) + 1L;

                RespondIfExpected(message, entry);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Error($"Leaderboard player entry request failed: {exception}");
                RespondErrorIfExpected(message, ResponseStatus.Error,
                    MstErrorCodes.LEADERBOARD_REQUEST_INVALID);
            }
        }

        protected virtual async Task ClientSubmitScoreRequestHandler(
            IIncomingMessage message, CancellationToken cancellationToken)
        {
            try
            {
                IUserPeerExtension user = GetAuthenticatedUser(message.Peer);

                if (user == null)
                {
                    RespondErrorIfExpected(message, ResponseStatus.Unauthorized,
                        MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                    return;
                }

                LeaderboardSubmitScorePacket request =
                    message.AsPacket<LeaderboardSubmitScorePacket>();

                if (!TryResolveDefinitionForSubmission(message, request.Key, request.SeasonId,
                        out LeaderboardDefinition definition))
                {
                    return;
                }

                if (definition.ServerOnly)
                {
                    RespondDefinitionError(message, ResponseStatus.Forbidden,
                        MstErrorCodes.LEADERBOARD_CLIENT_SUBMISSION_FORBIDDEN,
                        definition.Key, definition.SeasonId);
                    return;
                }

                if (!TryValidateAccountAndScore(message, definition, user.Account, request.Score))
                    return;

                await SubmitScoreAsync(message, definition, user.Account,
                    user.Username, null, request.Score, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Error($"Client leaderboard score submission failed: {exception}");
                RespondErrorIfExpected(message, ResponseStatus.Error,
                    MstErrorCodes.LEADERBOARD_SUBMISSION_FAILED);
            }
        }

        protected virtual async Task ServerSubmitScoreRequestHandler(
            IIncomingMessage message, CancellationToken cancellationToken)
        {
            try
            {
                if (!HasServerSubmissionPermission(message.Peer))
                {
                    RespondErrorIfExpected(message, ResponseStatus.Forbidden,
                        MstErrorCodes.PERMISSION_DENIED);
                    return;
                }

                LeaderboardSubmitScorePacket request =
                    message.AsPacket<LeaderboardSubmitScorePacket>();

                if (string.IsNullOrWhiteSpace(request.AccountId))
                {
                    RespondErrorIfExpected(message, ResponseStatus.Invalid,
                        MstErrorCodes.LEADERBOARD_ACCOUNT_REQUIRED);
                    return;
                }

                if (!TryResolveDefinitionForSubmission(message, request.Key, request.SeasonId,
                        out LeaderboardDefinition definition))
                {
                    return;
                }

                IAccountInfoData account = await GetAccountAsync(
                    request.AccountId, cancellationToken).ConfigureAwait(false);

                if (account == null)
                {
                    RespondDefinitionError(message, ResponseStatus.NotFound,
                        MstErrorCodes.LEADERBOARD_ACCOUNT_NOT_FOUND,
                        definition.Key, definition.SeasonId);
                    return;
                }

                if (!TryValidateAccountAndScore(message, definition, account, request.Score))
                    return;

                string playerName = string.IsNullOrWhiteSpace(request.PlayerName)
                    ? account.Username
                    : request.PlayerName;

                await SubmitScoreAsync(message, definition, account,
                    playerName, request.PlayerAvatar, request.Score, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Error($"Server leaderboard score submission failed: {exception}");
                RespondErrorIfExpected(message, ResponseStatus.Error,
                    MstErrorCodes.LEADERBOARD_SUBMISSION_FAILED);
            }
        }

        private async Task SubmitScoreAsync(
            IIncomingMessage message,
            LeaderboardDefinition definition,
            IAccountInfoData account,
            string playerName,
            string playerAvatar,
            long score,
            CancellationToken cancellationToken)
        {
            if (!TryGetDatabase(message, out ILeaderboardsDatabaseAccessor accessor))
                return;

            playerName = playerName?.Trim() ?? string.Empty;

            if (playerName.Length > LeaderboardEntry.MaxPlayerNameLength)
                playerName = playerName.Substring(0, LeaderboardEntry.MaxPlayerNameLength);

            if (playerAvatar != null)
                playerAvatar = LeaderboardEntry.NormalizePlayerAvatar(playerAvatar);

            DateTime submittedAtUtc = UtcNow;
            var submission = new LeaderboardScoreSubmission
            {
                LeaderboardKey = definition.Key,
                SeasonId = definition.SeasonId,
                AccountId = account.Id,
                PlayerName = playerName,
                PlayerAvatar = playerAvatar,
                Score = score,
                SortOrder = definition.SortOrder,
                KeepBest = definition.KeepBest,
                SubmittedAtUtc = submittedAtUtc
            };

            LeaderboardScoreUpdateResult result = await accessor.SubmitScoreAsync(
                submission, cancellationToken).ConfigureAwait(false);

            if (result?.Entry == null)
                throw new InvalidOperationException("Leaderboard accessor returned no persisted entry");

            result.Entry.Rank = await accessor.CountBetterEntriesAsync(
                definition.Key, definition.SeasonId, definition.SortOrder,
                result.Entry.Score, result.Entry.AccountId, cancellationToken).ConfigureAwait(false) + 1L;

            RespondIfExpected(message, new LeaderboardSubmitResultPacket
            {
                Entry = result.Entry,
                ScoreChanged = result.ScoreChanged
            });
        }

        private async Task<LeaderboardEntriesPacket> CreateEntriesPacketAsync(
            ILeaderboardsDatabaseAccessor accessor,
            LeaderboardDefinition definition,
            int offset,
            int limit,
            string currentAccountId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<LeaderboardEntry> entries = await accessor.GetEntriesAsync(
                definition.Key, definition.SeasonId, definition.SortOrder,
                offset, limit, cancellationToken).ConfigureAwait(false);
            long totalEntries = await accessor.CountEntriesAsync(
                definition.Key, definition.SeasonId, cancellationToken).ConfigureAwait(false);

            var responseEntries = new List<LeaderboardEntry>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                LeaderboardEntry entry = entries[i]?.Clone() ?? new LeaderboardEntry();
                entry.Rank = (long)offset + i + 1L;
                responseEntries.Add(entry);
            }

            long currentPlayerRank = 0L;

            if (!string.IsNullOrWhiteSpace(currentAccountId))
            {
                LeaderboardEntry ownEntry = await accessor.GetEntryAsync(
                    definition.Key, definition.SeasonId, currentAccountId, cancellationToken)
                    .ConfigureAwait(false);

                if (ownEntry != null)
                {
                    currentPlayerRank = await accessor.CountBetterEntriesAsync(
                        definition.Key, definition.SeasonId, definition.SortOrder,
                        ownEntry.Score, ownEntry.AccountId, cancellationToken).ConfigureAwait(false) + 1L;
                }
            }

            return new LeaderboardEntriesPacket
            {
                Definition = LeaderboardDefinitionPacket.FromDefinition(definition, UtcNow),
                Entries = responseEntries,
                TotalEntries = totalEntries,
                CurrentPlayerRank = currentPlayerRank,
                Offset = offset
            };
        }

        private bool TryResolveDefinitionForSubmission(
            IIncomingMessage message,
            string key,
            string seasonId,
            out LeaderboardDefinition definition)
        {
            if (!TryResolveDefinition(key, seasonId, out definition,
                    out ResponseStatus status, out string errorCode))
            {
                RespondDefinitionError(message, status, errorCode, key, seasonId);
                return false;
            }

            if (definition.GetAvailability(UtcNow) != LeaderboardAvailability.Active)
            {
                RespondDefinitionError(message, ResponseStatus.Conflict,
                    MstErrorCodes.LEADERBOARD_NOT_ACTIVE,
                    definition.Key, definition.SeasonId);
                return false;
            }

            return true;
        }

        private bool TryResolveDefinition(
            string key,
            string seasonId,
            out LeaderboardDefinition definition,
            out ResponseStatus status,
            out string errorCode)
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(key))
            {
                status = ResponseStatus.Invalid;
                errorCode = MstErrorCodes.LEADERBOARD_KEY_REQUIRED;
                return false;
            }

            key = key.Trim();

            if (!string.IsNullOrWhiteSpace(seasonId))
            {
                if (definitionsByIdentity.TryGetValue(
                        CreateDefinitionIdentity(key, seasonId.Trim()), out definition))
                {
                    status = ResponseStatus.Success;
                    errorCode = string.Empty;
                    return true;
                }

                status = ResponseStatus.NotFound;
                errorCode = MstErrorCodes.LEADERBOARD_NOT_FOUND;
                return false;
            }

            if (!definitionsByKey.TryGetValue(key, out List<LeaderboardDefinition> seasons) ||
                seasons.Count == 0)
            {
                status = ResponseStatus.NotFound;
                errorCode = MstErrorCodes.LEADERBOARD_NOT_FOUND;
                return false;
            }

            DateTime utcNow = UtcNow;
            definition = seasons.FirstOrDefault(item =>
                item.GetAvailability(utcNow) == LeaderboardAvailability.Active);

            if (definition == null)
            {
                definition = seasons
                    .Where(item => item.GetAvailability(utcNow) == LeaderboardAvailability.Ended)
                    .OrderByDescending(GetPeriodEnd)
                    .FirstOrDefault();
            }

            if (definition == null)
            {
                definition = seasons
                    .Where(item => item.GetAvailability(utcNow) == LeaderboardAvailability.Upcoming)
                    .OrderBy(GetPeriodStart)
                    .FirstOrDefault();
            }

            status = definition == null ? ResponseStatus.NotFound : ResponseStatus.Success;
            errorCode = definition == null ? MstErrorCodes.LEADERBOARD_NOT_FOUND : string.Empty;
            return definition != null;
        }

        private bool TryValidateAccountAndScore(
            IIncomingMessage message,
            LeaderboardDefinition definition,
            IAccountInfoData account,
            long score)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.Id))
            {
                RespondErrorIfExpected(message, ResponseStatus.NotFound,
                    MstErrorCodes.LEADERBOARD_ACCOUNT_NOT_FOUND);
                return false;
            }

            if (account.IsGuest && !definition.AllowGuests)
            {
                RespondDefinitionError(message, ResponseStatus.Forbidden,
                    MstErrorCodes.LEADERBOARD_GUEST_SUBMISSION_FORBIDDEN,
                    definition.Key, definition.SeasonId);
                return false;
            }

            if (score < definition.MinimumScore || score > definition.MaximumScore)
            {
                var properties = CreateDefinitionProperties(definition.Key, definition.SeasonId);
                properties.Set(MstErrorPropertyKeys.MIN,
                    definition.MinimumScore.ToString(CultureInfo.InvariantCulture));
                properties.Set(MstErrorPropertyKeys.MAX,
                    definition.MaximumScore.ToString(CultureInfo.InvariantCulture));
                properties.Set(MstErrorPropertyKeys.VALUE,
                    score.ToString(CultureInfo.InvariantCulture));
                RespondErrorIfExpected(message, ResponseStatus.Invalid,
                    MstErrorCodes.LEADERBOARD_SCORE_OUT_OF_RANGE, properties);
                return false;
            }

            return true;
        }

        private async Task<IAccountInfoData> GetAccountAsync(
            string accountId, CancellationToken cancellationToken)
        {
            if (authModule != null &&
                authModule.TryGetLoggedInUserById(accountId, out IUserPeerExtension user))
            {
                return user.Account;
            }

            if (authModule?.DatabaseAccessor == null)
                return null;

            return await authModule.DatabaseAccessor.GetAccountByIdAsync(
                accountId, cancellationToken).ConfigureAwait(false);
        }

        private bool HasServerSubmissionPermission(IPeer peer)
        {
            SecurityInfoPeerExtension security = peer?.GetExtension<SecurityInfoPeerExtension>();
            return security != null && security.HasPermission(MstPermissionKeys.RoomServer);
        }

        private static IUserPeerExtension GetAuthenticatedUser(IPeer peer)
        {
            return peer?.GetExtension<IUserPeerExtension>();
        }

        private bool TryGetDatabase(
            IIncomingMessage message, out ILeaderboardsDatabaseAccessor accessor)
        {
            accessor = databaseAccessor;

            if (accessor != null)
                return true;

            RespondErrorIfExpected(message, ResponseStatus.ServiceUnavailable,
                MstErrorCodes.LEADERBOARD_DATABASE_UNAVAILABLE);
            return false;
        }

        private int NormalizePageSize(int requestedSize)
        {
            if (requestedSize <= 0)
                return defaultPageSize;

            return Math.Min(requestedSize, maximumPageSize);
        }

        private static bool PeriodsOverlap(
            LeaderboardDefinition first, LeaderboardDefinition second)
        {
            first.TryGetPeriod(out DateTime? firstStart, out DateTime? firstEnd, out _);
            second.TryGetPeriod(out DateTime? secondStart, out DateTime? secondEnd, out _);

            DateTime firstStartValue = firstStart ?? DateTime.MinValue;
            DateTime firstEndValue = firstEnd ?? DateTime.MaxValue;
            DateTime secondStartValue = secondStart ?? DateTime.MinValue;
            DateTime secondEndValue = secondEnd ?? DateTime.MaxValue;

            return firstStartValue < secondEndValue && secondStartValue < firstEndValue;
        }

        private static DateTime GetPeriodStart(LeaderboardDefinition definition)
        {
            definition.TryGetPeriod(out DateTime? startsAtUtc, out _, out _);
            return startsAtUtc ?? DateTime.MinValue;
        }

        private static DateTime GetPeriodEnd(LeaderboardDefinition definition)
        {
            definition.TryGetPeriod(out _, out DateTime? endsAtUtc, out _);
            return endsAtUtc ?? DateTime.MaxValue;
        }

        private static string CreateDefinitionIdentity(string key, string seasonId)
        {
            return $"{key?.Trim()}\n{seasonId?.Trim()}";
        }

        private static MstProperties CreateDefinitionProperties(string key, string seasonId)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.LEADERBOARD_KEY, key ?? string.Empty);
            properties.Set(MstErrorPropertyKeys.SEASON_ID, seasonId ?? string.Empty);
            return properties;
        }

        private static void RespondDefinitionError(
            IIncomingMessage message,
            ResponseStatus status,
            string errorCode,
            string key,
            string seasonId)
        {
            RespondErrorIfExpected(message, status, errorCode,
                CreateDefinitionProperties(key, seasonId));
        }

        private static void RespondIfExpected(IIncomingMessage message, SerializablePacket packet)
        {
            if (message.IsExpectingResponse)
                message.Respond(packet, ResponseStatus.Success);
        }

        private static void RespondErrorIfExpected(
            IIncomingMessage message,
            ResponseStatus status,
            string errorCode,
            MstProperties properties = null)
        {
            if (message.IsExpectingResponse)
                message.RespondError(status, errorCode, properties);
        }
    }
}
