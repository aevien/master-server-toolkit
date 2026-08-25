using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using MasterServerToolkit.GameService;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Abstraction over the persistence layer for user accounts.
    /// Implementations encapsulate all data-access operations related to accounts,
    /// tokens, confirmation codes, and arbitrary extra properties.
    ///
    /// <para>
    /// <b>Error handling contract:</b> methods that retrieve a single entity
    /// should return <c>null</c> when the entity is not found; all unexpected
    /// persistence errors should be surfaced as exceptions (do not swallow).
    /// </para>
    ///
    /// <para>
    /// <b>Thread-safety:</b> implementors should be safe to use from concurrent
    /// asynchronous calls or document their constraints explicitly.
    /// </para>
    ///
    /// <para>
    /// <b>Transactions:</b> multi-step write operations that must be atomic
    /// (e.g., creating an account together with its extra properties) should be
    /// executed within a database transaction.
    /// </para>
    /// </summary>
    public interface IAccountsDatabaseAccessor : IDatabaseAccessor
    {
        /// <summary>
        /// Creates a new empty account data instance.
        /// This is a factory method that returns a concrete implementation of <see cref="IAccountInfoData"/>.
        /// </summary>
        /// <returns>
        /// A new, uninitialized <see cref="IAccountInfoData"/> object that the caller can populate and pass to create/update methods.
        /// </returns>
        IAccountInfoData CreateAccountInstance();

        /// <summary>
        /// Creates a new empty account block data instance for the current persistence provider.
        /// </summary>
        IAccountBlockData CreateAccountBlockInstance();

        /// <summary>
        /// Creates a new empty external service binding data instance.
        /// The binding is server-owned and must be used for authoritative account lookup by external platform identity.
        /// </summary>
        IAccountServiceBindingData CreateServiceBindingInstance();

        /// <summary>
        /// Retrieves the account binding for a concrete external service identity.
        /// </summary>
        /// <param name="serviceId">External game service identifier.</param>
        /// <param name="playerId">Player identifier returned by the external service.</param>
        /// <returns>The matching binding, or <c>null</c> when this service identity is not linked.</returns>
        Task<IAccountServiceBindingData> GetServiceBindingAsync(GameServiceId serviceId, string playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all external service bindings linked to one MST account.
        /// </summary>
        /// <param name="accountId">MST account identifier.</param>
        Task<IReadOnlyList<IAccountServiceBindingData>> GetServiceBindingsByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts or updates one server-owned external service binding.
        /// Implementations must make both <c>accountId + serviceId</c> and
        /// <c>serviceId + playerId</c> unique.
        /// </summary>
        Task UpsertServiceBindingAsync(IAccountServiceBindingData binding, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically replaces the external service binding owned by the same account and service.
        /// </summary>
        /// <param name="binding">The authoritative binding that must remain after the operation completes.</param>
        /// <param name="cancellationToken">
        /// Cancels the operation before its transaction is committed. Once commit begins, implementations
        /// must finish it and report the committed result instead of surfacing a late cancellation.
        /// </param>
        /// <remarks>
        /// The persistent binding identity is the stable
        /// <c>binding.AccountId + binding.ServiceId</c> slot. Rebinding a platform player must
        /// replace that slot with one atomic write and must not leave the old player id addressable.
        /// </remarks>
        Task ReplaceServiceBindingAsync(IAccountServiceBindingData binding, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes one external service binding from an account.
        /// </summary>
        Task DeleteServiceBindingAsync(string accountId, GameServiceId serviceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the active block record for the account, or <c>null</c> when the account is not blocked.
        /// </summary>
        Task<IAccountBlockData> GetActiveAccountBlockAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves historical account block records, newest first.
        /// </summary>
        Task<DatabaseEntriesInfo<IAccountBlockData>> GetAccountBlockHistoryAsync(string accountId, int size, int page, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a new account block record. Implementations should close an existing active block first.
        /// </summary>
        Task<string> InsertAccountBlockAsync(IAccountBlockData block, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the active account block as removed without deleting the historical record.
        /// </summary>
        Task<bool> UnblockAccountAsync(string accountId, string removeReason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a paged list of accounts with an optional filter.
        /// </summary>
        /// <param name="filter">
        /// Optional filter bag. Keys and value types are implementation-defined.
        /// Typical keys may include:
        /// <list type="bullet">
        ///   <item><description><c>"q"</c> (string): free-text search over Id/Username/Email.</description></item>
        ///   <item><description><c>"isAdmin"</c> (bool): filter by admin flag.</description></item>
        ///   <item><description><c>"isGuest"</c> (bool): filter by guest flag.</description></item>
        ///   <item><description><c>"isEmailConfirmed"</c> (bool): filter by email confirmation flag.</description></item>
        ///   <item><description><c>"isBlocked"</c> (bool): filter by active account block.</description></item>
        /// </list>
        /// Implementations should document supported keys and apply identical filters to
        /// both the total count and the entries query to keep pagination consistent.
        /// </param>
        /// <returns>
        /// A <see cref="DatabaseEntriesInfo{T}"/> containing the total number of matching records
        /// and the current page of <see cref="IAccountInfoData"/> entries.
        /// </returns>
        Task<DatabaseEntriesInfo<IAccountInfoData>> Search(Dictionary<string, object> filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a user account by its unique identifier.
        /// </summary>
        /// <param name="accountId">The unique account identifier.</param>
        /// <returns>
        /// The matching account, or <c>null</c> if no account exists with the given identifier.
        /// </returns>
        Task<IAccountInfoData> GetAccountByIdAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a user account by its unique username.
        /// </summary>
        /// <param name="username">The unique username to look up.</param>
        /// <returns>
        /// The matching account, or <c>null</c> if none exists.
        /// </returns>
        Task<IAccountInfoData> GetAccountByUsernameAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a user account by its email address.
        /// </summary>
        /// <param name="email">Email address to look up.</param>
        /// <returns>
        /// The matching account, or <c>null</c> if none exists.
        /// </returns>
        Task<IAccountInfoData> GetAccountByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a user account by an extra (custom) property.
        /// </summary>
        /// <param name="propertyKey">The extra property key (case sensitivity is implementation-defined).</param>
        /// <param name="propertyValue">The extra property value to match.</param>
        /// <returns>
        /// The first matching account, or <c>null</c> if no account has the specified extra property pair.
        /// </returns>
        /// <remarks>
        /// Implementations should define whether multiple accounts can share the same key/value pair
        /// and which record is returned if multiple matches exist (e.g., first by creation date).
        /// </remarks>
        Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a user account by its authentication token.
        /// </summary>
        /// <param name="token">Opaque session or API token associated with the account.</param>
        /// <returns>
        /// The matching account, or <c>null</c> if the token is unknown or expired (if expiration is enforced elsewhere).
        /// </returns>
        /// <remarks>
        /// Token format and lifecycle (issuance, rotation, revocation, expiration) are outside the scope of this interface.
        /// Implementations typically index this field for fast lookups.
        /// </remarks>
        Task<IAccountInfoData> GetAccountByTokenAsync(string token, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the last successful login timestamp for the account.
        /// </summary>
        /// <param name="accountId">The unique account identifier.</param>
        /// <returns>The UTC timestamp persisted as the account's last login time.</returns>
        Task<DateTime> UpdateLastLoginAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists a password reset code associated with an email address.
        /// </summary>
        /// <param name="email">Email address of the account requesting a password reset.</param>
        /// <param name="code">The reset code to store (format and length are application-defined).</param>
        /// <param name="expiresAt">UTC time after which the code must be rejected and removed.</param>
        /// <param name="attempts">Maximum number of invalid validation attempts.</param>
        /// <returns>A task that completes when the code has been persisted.</returns>
        /// <remarks>
        /// Implementations may upsert by email (replace previous code), and may also persist metadata such as creation and expiration times.
        /// </remarks>
        Task SavePasswordResetCodeAsync(
            string email,
            string code,
            DateTime expiresAt,
            int attempts,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies a password reset code and consumes it if valid.
        /// </summary>
        /// <param name="email">Email address to match against the stored reset code.</param>
        /// <param name="code">The reset code provided by the user.</param>
        /// <returns>
        /// A precise validation result. A successful code is consumed. An expired code or a code
        /// whose last attempt is spent is also removed.
        /// </returns>
        /// <remarks>
        /// Implementations should ensure that successful validation makes the code unusable thereafter (one-time use).
        /// </remarks>
        Task<VerificationCodeResult> CheckPasswordResetCodeAsync(string email, string code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists an email confirmation code for post-registration verification.
        /// </summary>
        /// <param name="email">Email address of the account to confirm.</param>
        /// <param name="code">The confirmation code to store.</param>
        /// <param name="expiresAt">UTC time after which the code must be rejected and removed.</param>
        /// <param name="attempts">Maximum number of invalid validation attempts.</param>
        /// <returns>A task that completes when the code has been persisted.</returns>
        /// <remarks>
        /// As with reset codes, implementations may upsert per email and should consider storing expiration timestamps.
        /// </remarks>
        Task SaveEmailConfirmationCodeAsync(
            string email,
            string code,
            DateTime expiresAt,
            int attempts,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies an email confirmation code and consumes it if valid.
        /// </summary>
        /// <param name="email">Email address to match against the stored confirmation code.</param>
        /// <param name="code">The confirmation code provided by the user.</param>
        /// <returns>
        /// A precise validation result. A successful code is consumed. An expired code or a code
        /// whose last attempt is spent is also removed.
        /// </returns>
        Task<VerificationCodeResult> CheckEmailConfirmationCodeAsync(string email, string code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the current authentication-token revision for an account. Missing revision data is revision zero.
        /// Normal token rotation does not change this value.
        /// </summary>
        Task<int> GetAuthTokenRevisionAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically increments and returns the authentication-token revision for an account.
        /// Every token issued with an older revision becomes invalid.
        /// </summary>
        Task<int> IncrementAuthTokenRevisionAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates all mutable fields of an existing account record.
        /// </summary>
        /// <param name="account">
        /// The account object containing the updated state. Must include a valid primary key value (e.g., <c>Id</c>).
        /// </param>
        /// <returns>A task that completes when the update has been persisted.</returns>
        /// <remarks>
        /// Implementations should update only allowed columns and may perform an upsert of associated extra properties atomically.
        /// </remarks>
        Task UpdateAccountAsync(IAccountInfoData account, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new account record.
        /// </summary>
        /// <param name="account">
        /// The account object to insert. Must include all required fields (e.g., unique <c>Username</c>, <c>Email</c>, password hash).
        /// </param>
        /// <returns>
        /// The persistent identifier of the newly created account (typically the same as <c>account.Id</c> after insertion).
        /// </returns>
        /// <remarks>
        /// If extra properties are part of the model, implementations should insert them within the same transaction.
        /// </remarks>
        Task<string> InsertAccountAsync(IAccountInfoData account, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts or updates an authentication token associated with the specified account.
        /// </summary>
        /// <param name="account">The account for which to store the token. Must identify an existing record.</param>
        /// <param name="token">The token value to persist (opaque to the data layer).</param>
        /// <returns>A task that completes when the token has been stored.</returns>
        /// <remarks>
        /// Typical implementations perform an update on the account's token column.
        /// Validation of token format and lifecycle is the responsibility of the calling layer.
        /// </remarks>
        Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token, CancellationToken cancellationToken = default);
    }
}
