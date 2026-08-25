using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Abstraction over the persistence layer for user/player profiles.
    /// Implementations encapsulate loading and saving of <see cref="ObservableServerProfile"/> instances.
    ///
    /// <para>
    /// <b>Error handling contract:</b> do not swallow unexpected persistence errors.
    /// Propagate exceptions to the caller; callers may translate them to domain or transport errors.
    /// </para>
    ///
    /// <para>
    /// <b>Thread-safety:</b> implementations should be safe to call from concurrent
    /// asynchronous operations or must document their constraints explicitly (e.g., single-threaded access).
    /// </para>
    ///
    /// <para>
    /// <b>Transactions:</b> multi-entity writes (e.g., <see cref="UpdateProfilesAsync"/>) should be executed
    /// atomically within a database transaction where supported.
    /// </para>
    ///
    /// <para>
    /// <b>Merge semantics:</b> profile schema, serialization format, and merge behavior are implementation-defined.
    /// Typical strategies include full replace, upsert with per-field merge, or patch semantics.
    /// </para>
    /// </summary>
    public interface IProfilesDatabaseAccessor : IDatabaseAccessor
    {
        /// <summary>
        /// Restores the persisted state of the specified <paramref name="profile"/> from the database.
        /// If no persisted entry exists, the method should leave the in-memory object unchanged.
        /// </summary>
        /// <param name="profile">
        /// The in-memory <see cref="ObservableServerProfile"/> instance to fill/update.
        /// Implementations should mutate this instance (by side-effect) to reflect the stored state.
        /// </param>
        /// <param name="cancellationToken">Token used to cancel waiting for the persistence operation.</param>
        /// <returns>A task that completes when the load/merge operation has finished.</returns>
        /// <remarks>
        /// <para>
        /// <b>Not-found behavior:</b> absence of a stored profile is <i>not</i> an error; the method should simply do nothing.
        /// </para>
        /// <para>
        /// <b>Merge behavior:</b> define clearly whether restore performs a full replace or a field-wise merge
        /// (e.g., only overwriting fields that are present in storage). If the profile supports default values,
        /// restoration should not wipe caller-supplied defaults unless explicitly intended.
        /// </para>
        /// <para>
        /// <b>Concurrency:</b> when profiles have a version/concurrency token, implementations may set it on the
        /// provided instance to support optimistic concurrency on subsequent updates.
        /// </para>
        /// </remarks>
        Task RestoreProfileAsync(ObservableServerProfile profile, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores the persisted state for multiple <paramref name="profiles"/> from the database.
        /// If some profiles do not exist in storage, implementations should leave those in-memory instances unchanged.
        /// </summary>
        /// <param name="profiles">
        /// A sequence of <see cref="ObservableServerProfile"/> instances to restore.
        /// Implementations should mutate the provided objects by side-effect.
        /// </param>
        /// <param name="cancellationToken">Token used to cancel waiting for the persistence operation.</param>
        /// <returns>A task that completes when the restore operation has finished for all provided profiles.</returns>
        /// <remarks>
        /// <para>
        /// <b>Performance:</b> prefer batched reads over N independent round-trips where supported by the backing store.
        /// </para>
        /// <para>
        /// <b>Not-found behavior:</b> absence of a stored profile is not an error and should not fail the whole batch.
        /// </para>
        /// </remarks>
        Task RestoreProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches profile properties in the persistence layer and returns a paged result.
        /// </summary>
        /// <param name="filter">Search, sorting and paging parameters.</param>
        /// <param name="cancellationToken">Token used to cancel waiting for the persistence operation.</param>
        /// <returns>A paged collection of profile property entries.</returns>
        Task<DatabaseEntriesInfo<IProfilePropertyData>> Search(Dictionary<string, object> filter,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists the updated state of a single <paramref name="profile"/> to the database.
        /// </summary>
        /// <param name="profile">
        /// The profile instance to persist. Implementations should decide whether this is a full-replace,
        /// an upsert, or a partial update (patch). Any required identifiers (e.g., user/account id) must be present.
        /// </param>
        /// <param name="cancellationToken">Token used to cancel waiting for the persistence operation.</param>
        /// <returns>A task that completes when the profile has been persisted.</returns>
        /// <remarks>
        /// <para>
        /// <b>Atomicity:</b> the update should be atomic for this single profile. If the profile is split across
        /// multiple tables/collections, wrap the operation in a transaction where supported.
        /// </para>
        /// <para>
        /// <b>Optimistic concurrency:</b> if the profile model exposes a version/etag, implementations should
        /// enforce it to prevent lost updates, throwing a domain-specific concurrency exception on conflict.
        /// </para>
        /// <para>
        /// <b>Validation:</b> implementations may validate schema or required fields and should throw on violations.
        /// </para>
        /// </remarks>
        Task UpdateProfileAsync(ObservableServerProfile profile, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists the updated state of multiple profiles in bulk.
        /// </summary>
        /// <param name="profiles">
        /// A sequence of <see cref="ObservableServerProfile"/> instances to persist.
        /// </param>
        /// <param name="cancellationToken">Token used to cancel waiting for the persistence operation.</param>
        /// <returns>A task that completes when all profiles have been persisted.</returns>
        /// <remarks>
        /// <para>
        /// <b>Atomicity:</b> where feasible, persist all changes within a single transaction so the batch is
        /// fully applied or fully rolled back. If atomic batch updates are not supported by the underlying store,
        /// document partial-failure semantics (e.g., which items succeeded).
        /// </para>
        /// <para>
        /// <b>Performance:</b> prefer batched/bulk operations over N individual round-trips.
        /// Consider chunking the input to reasonable sizes to avoid exceeding database limits.
        /// </para>
        /// <para>
        /// <b>Concurrency:</b> apply the same version/etag checks per profile as in <see cref="UpdateProfileAsync"/>.
        /// </para>
        /// <para>
        /// <b>Enumeration:</b> callers may pass a streaming <see cref="IEnumerable{T}"/>; implementations should
        /// snapshot the input where necessary to avoid multiple enumeration.
        /// </para>
        /// </remarks>
        Task UpdateProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
            CancellationToken cancellationToken = default);
    }
}
