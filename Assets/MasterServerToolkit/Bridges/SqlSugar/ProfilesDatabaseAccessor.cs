using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    /// <summary>
    /// SqlSugar-based implementation of <see cref="IProfilesDatabaseAccessor"/>.
    /// Responsibilities:
    ///  - Restore in-memory <see cref="ObservableServerProfile"/> state from DB
    ///  - Persist single or multiple profiles atomically (batched upsert of properties)
    ///
    /// Error handling:
    ///  - Do NOT swallow unexpected persistence errors; let exceptions bubble up.
    ///  - "Not found" on restore is NOT an error; method just leaves the profile unchanged.
    ///
    /// Performance:
    ///  - Bulk upserts are chunked to avoid parameter/packet limits.
    ///  - One transaction per UpdateProfilesAsync call.
    /// </summary>
    public class ProfilesDatabaseAccessor : IProfilesDatabaseAccessor
    {
        private readonly ConnectionConfig configuration;

        public MstProperties CustomProperties { get; private set; }
        public Logging.Logger Logger { get; set; }

        // Increase if your profiles contain very large numbers of properties
        private const int BulkChunkSize = 500;

        public ProfilesDatabaseAccessor(ConnectionConfig configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            using SqlSugarClient db = new(configuration);

            // Ensure required tables exist (consider moving to startup/migrations in production)
            var tableTypes = new[]
            {
                typeof(ProfilePropertyData)
            };

            foreach (var tableType in tableTypes)
            {
                var tableName = db.EntityMaintenance.GetTableName(tableType);

                if (!db.DbMaintenance.IsAnyTable(tableName, false))
                    db.CodeFirst.InitTables(tableType);

                if (!db.DbMaintenance.IsAnyTable(tableName, false))
                    throw new InvalidOperationException($"Required database table '{tableName}' was not created");
            }
        }

        public void Dispose() { }

        /// <summary>
        /// Loads persisted properties for the given profile and mutates the provided instance.
        /// Absence of data is not an error; the method leaves the profile as-is.
        /// </summary>
        public async Task RestoreProfileAsync(ObservableServerProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RestoreProfilesAsync(new[] { profile }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task RestoreProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (profiles is null)
                throw new ArgumentNullException(nameof(profiles));

            var profilesList = profiles as IList<ObservableServerProfile> ?? profiles.ToList();

            if (profilesList.Count == 0)
                return;

            var validProfiles = profilesList
                .Where(p => p != null)
                .ToList();

            foreach (var profile in validProfiles)
            {
                if (string.IsNullOrWhiteSpace(profile.UserId))
                    throw new ArgumentException("Every profile must have a valid UserId", nameof(profiles));
            }

            using SqlSugarClient db = new(configuration);

            var userIds = validProfiles
                .Select(p => p.UserId)
                .Distinct()
                .ToList();

            var entries = await db.Queryable<ProfilePropertyData>()
                                  .Where(p => userIds.Contains(p.AccountId))
                                  .ToListAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var entriesByAccountId = entries
                .GroupBy(e => e.AccountId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var profile in validProfiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!entriesByAccountId.TryGetValue(profile.UserId, out var profileEntries))
                    continue;

                foreach (var entry in profileEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var hashedKey = entry.PropertyKey.ToUint16Hash();

                    if (profile.TryGet(hashedKey, out IObservableProperty property))
                    {
                        property.FromJson(entry.PropertyValue);
                    }
                }
            }
        }

        public async Task<DatabaseEntriesInfo<IProfilePropertyData>> Search(Dictionary<string, object> filter,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int size = int.Parse(filter[MstParamKeys.QS_PARAM_SIZE].ToString());
            int page = int.Parse(filter[MstParamKeys.QS_PARAM_PAGE].ToString());
            int skip = page * size;

            string accountId = filter.TryGetValue(MstParamKeys.QS_PARAM_ACCOUNT_ID, out var aid)
                ? aid?.ToString() ?? string.Empty
                : string.Empty;
            string[] accountIds = accountId
                .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            string propertyId = filter.TryGetValue(MstParamKeys.QS_PARAM_ID, out var pid)
                ? pid?.ToString() ?? string.Empty
                : string.Empty;
            string[] propertyIds = propertyId
                .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            string searchValue = filter.TryGetValue(MstParamKeys.QS_PARAM_VALUE, out var sv)
                ? sv?.ToString() ?? string.Empty
                : string.Empty;
            string conditionValue = filter.TryGetValue(MstParamKeys.QS_PARAM_CONDITION, out var sc)
                ? sc?.ToString() ?? string.Empty
                : string.Empty;
            string sortField = filter.TryGetValue(MstParamKeys.QS_PARAM_SORT_FIELD, out var sf)
                ? sf?.ToString() ?? string.Empty
                : string.Empty;
            string sortDir = filter.TryGetValue(MstParamKeys.QS_PARAM_SORT_DIR, out var sd)
                ? sd?.ToString() ?? string.Empty
                : string.Empty;

            MstSearchCondition condition = MstSearchCondition.Contains;
            MstSortDirection sortDirection = MstSortDirection.Asc;

            if (!string.IsNullOrWhiteSpace(conditionValue) &&
                Enum.TryParse(conditionValue, true, out MstSearchCondition parsedCondition))
            {
                condition = parsedCondition;
            }

            if (!string.IsNullOrWhiteSpace(sortDir) &&
                Enum.TryParse(sortDir, true, out MstSortDirection parsedSortDirection))
            {
                sortDirection = parsedSortDirection;
            }

            using SqlSugarClient db = new(configuration);

            int totalAll = await db.Queryable<ProfilePropertyData>().CountAsync();
            cancellationToken.ThrowIfCancellationRequested();

            ISugarQueryable<ProfilePropertyData> query = db.Queryable<ProfilePropertyData>();

            if (accountIds.Length > 0)
            {
                query = query.Where(p => accountIds.Contains(p.AccountId));
            }

            if (propertyIds.Length > 0)
            {
                query = query.Where(p => propertyIds.Contains(p.PropertyKey));
            }

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                switch (condition)
                {
                    case MstSearchCondition.Equals:
                        query = query.Where(p => p.PropertyValue == searchValue);
                        break;

                    case MstSearchCondition.Greater:
                    case MstSearchCondition.Less:
                    case MstSearchCondition.GreaterOrEquals:
                    case MstSearchCondition.LessOrEquals:
                        if (!decimal.TryParse(searchValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal numericValue) &&
                            !decimal.TryParse(searchValue, NumberStyles.Any, CultureInfo.CurrentCulture, out numericValue))
                        {
                            throw new ArgumentException($"Search value '{searchValue}' is not a valid number for condition '{condition}'.", nameof(filter));
                        }

                        string sqlOperator = condition switch
                        {
                            MstSearchCondition.Greater => ">",
                            MstSearchCondition.Less => "<",
                            MstSearchCondition.GreaterOrEquals => ">=",
                            MstSearchCondition.LessOrEquals => "<=",
                            _ => "="
                        };

                        query = query.Where($"CAST(property_value AS DECIMAL(38,10)) {sqlOperator} @numericValue", new { numericValue });
                        break;

                    case MstSearchCondition.Contains:
                    default:
                        query = query.Where(p => p.PropertyValue.Contains(searchValue));
                        break;
                }
            }

            int filteredCount = await query.CountAsync();
            cancellationToken.ThrowIfCancellationRequested();

            query = sortField switch
            {
                MstParamKeys.QS_PARAM_ID => sortDirection == MstSortDirection.Desc
                    ? query.OrderBy(p => p.PropertyKey, OrderByType.Desc)
                    : query.OrderBy(p => p.PropertyKey, OrderByType.Asc),

                MstParamKeys.QS_PARAM_VALUE => sortDirection == MstSortDirection.Desc
                    ? query.OrderBy(p => p.PropertyValue, OrderByType.Desc)
                    : query.OrderBy(p => p.PropertyValue, OrderByType.Asc),

                _ => sortDirection == MstSortDirection.Desc
                    ? query.OrderBy(p => p.AccountId, OrderByType.Desc)
                    : query.OrderBy(p => p.AccountId, OrderByType.Asc)
            };

            List<ProfilePropertyData> pageItems = await query
                .Skip(skip)
                .Take(size)
                .ToListAsync();
            cancellationToken.ThrowIfCancellationRequested();

            return new DatabaseEntriesInfo<IProfilePropertyData>
            {
                entries = pageItems.Cast<IProfilePropertyData>().ToList(),
                total = totalAll,
                filtered = filteredCount
            };
        }

        /// <summary>
        /// Persists a single profile by delegating to the bulk method.
        /// </summary>
        public Task UpdateProfileAsync(ObservableServerProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (profile is null)
                throw new ArgumentNullException(nameof(profile));

            return UpdateProfilesAsync(new[] { profile }, cancellationToken);
        }

        /// <summary>
        /// Persists multiple profiles atomically. Uses batched upsert of properties.
        /// </summary>
        public async Task UpdateProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (profiles is null)
                throw new ArgumentNullException(nameof(profiles));

            using SqlSugarClient db = new(configuration);

            // Snapshot the input to avoid multiple enumeration and allow quick checks
            var profilesList = profiles as IList<ObservableServerProfile> ?? profiles.ToList();
            cancellationToken.ThrowIfCancellationRequested();

            if (profilesList.Count == 0)
                return;

            int total = 0;

            foreach (var p in profilesList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (p != null)
                    total += p.Count;
            }

            var entries = new List<ProfilePropertyData>(total);

            foreach (var profile in profilesList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (profile is null)
                    continue;

                if (string.IsNullOrWhiteSpace(profile.UserId))
                    throw new ArgumentException("Every profile must have a valid UserId", nameof(profiles));

                foreach (var property in profile)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var keyName = StringExtensions.FromHash(property.Key);

                    entries.Add(new ProfilePropertyData
                    {
                        AccountId = profile.UserId,
                        PropertyKey = keyName,
                        PropertyValue = property.ToJson().ToString()
                    });
                }
            }

            if (entries.Count == 0)
                return;

            // Atomic bulk upsert in chunks
            var tran = await db.Ado.UseTranAsync(async () =>
            {
                foreach (var chunk in Chunk(entries, BulkChunkSize))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await db.Storageable(chunk)
                            .WhereColumns(new[] { "account_id", "property_key" })
                            .ExecuteCommandAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            });

            if (!tran.IsSuccess)
                throw tran.ErrorException;
        }

        // Helper: chunk large lists to avoid exceeding DB parameter limits
        private static IEnumerable<List<T>> Chunk<T>(IList<T> source, int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            for (int i = 0; i < source.Count; i += size)
            {
                var count = Math.Min(size, source.Count - i);
                var slice = new List<T>(count);

                for (int j = 0; j < count; j++)
                    slice.Add(source[i + j]);

                yield return slice;
            }
        }
    }
}
