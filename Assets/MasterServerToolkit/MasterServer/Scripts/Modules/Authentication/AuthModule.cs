using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    // Delegates for handling user-related events
    public delegate Task UserLoggedInEventHandlerDelegate(IUserPeerExtension user, CancellationToken cancellationToken);
    public delegate void UserLoggedOutEventHandlerDelegate(IUserPeerExtension user);
    public delegate void UserRegisteredEventHandlerDelegate(IPeer peer, IAccountInfoData account);
    public delegate void UserEmailConfirmedEventHandlerDelegate(IAccountInfoData account);
    public delegate void UserAccountUpdatedEventHandlerDelegate(IAccountInfoData account);
    public delegate void UsernameChangedEventHandlerDelegate(string oldUsername, string newUsername);

    /// <summary>
    /// Authentication module, which handles logging in and registration of accounts
    /// </summary>
    public class AuthModule : BaseServerModule, IServerStartupValidator
    {
        private const string LegacyDefaultTokenSecret = "t0k9n-$ecr9t";
        private const string PlaceholderTokenSecret = "change-this-token-secret";
        private const int MinimumTokenSecretByteCount = 32;
        private const int MaximumActiveEmailOperations = 64;
        private const int MaximumConcurrentPasswordHashOperations = 2;
        private const int SessionTransitionLockCount = 1024;

        #region INSPECTOR

        [Header("Settings"), SerializeField, Tooltip("Minimum character count accepted for registered account usernames. Guest usernames are generated separately and do not use this limit.")]
        protected int usernameMinChars = 4;

        [SerializeField, Tooltip("Maximum character count accepted for registered account usernames. Guest usernames are generated separately and do not use this limit.")]
        protected int usernameMaxChars = 12;

        [SerializeField, Tooltip("Minimum character count accepted for account passwords during registration and password changes.")]
        protected int userPasswordMinChars = 8;

        [SerializeField, Tooltip("Maximum character count accepted for account passwords. Keep a finite limit to bound password-hashing input work.")]
        protected int userPasswordMaxChars = 1024;

        [SerializeField, Tooltip("Requires a registered account to confirm its email before normal sign-in succeeds. A configured Mailer is required to deliver confirmation messages.")]
        protected bool emailConfirmRequired = true;

        [Header("Guest Settings")]
        [SerializeField, Tooltip("Allows clients to create temporary guest sessions without registered credentials.")]
        protected bool allowGuestLogin = true;

        [SerializeField, Tooltip("Prefix added to generated guest usernames, for example 'player-'. It does not affect registered account usernames.")]
        protected string guestPrefix = "user#";

        [Header("E-Mail Settings"), SerializeField, Tooltip("Mailer used for confirmation, password-reset and email sign-in messages. Leave None only when every email-dependent flow is disabled or replaced by a custom implementation.")]
        protected Mailer mailer;

        [SerializeField, TextArea(3, 10), Tooltip("Regular expression used to validate account email addresses before database or mail operations. It must match the complete address; invalid expressions make email validation fail.")]
        protected string emailAddressValidationTemplate = @"^(?=.{3,254}$)(?!.*\.\.)[A-Za-z0-9](?:[A-Za-z0-9._%+\-]{0,62}[A-Za-z0-9])?@(?:[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$";

        [Header("Generic"), SerializeField, Tooltip("Minimum character count accepted for generated or submitted service codes such as confirmation and reset codes.")]
        protected int serviceCodeMinChars = 6;

        [Header("Email Sign-In Security")]
        [SerializeField, Min(1), Tooltip("Lifetime in minutes of an email sign-in code after it is issued. Minimum Inspector value is 1 minute.")]
        protected int emailSignInCodeLifetimeMinutes = 10;

        [SerializeField, Min(1), Tooltip("Maximum invalid confirmation attempts allowed before an email sign-in code is consumed. Minimum Inspector value is 1.")]
        protected int emailSignInMaxAttempts = 5;

        [SerializeField, Min(1), Tooltip("Cooldown in seconds between email operations for the same peer and address. Minimum Inspector value is 1 second.")]
        protected int emailOperationCooldownSeconds = 60;

        [Header("Persisted Verification Code Security")]
        [SerializeField, Min(1), Tooltip("Lifetime in minutes of password-reset and email-confirmation codes stored in the accounts database. Expired codes are removed when validation is attempted. Minimum Inspector value is 1 minute.")]
        protected int verificationCodeLifetimeMinutes = 10;

        [SerializeField, Min(1), Tooltip("Maximum invalid attempts allowed for a stored password-reset or email-confirmation code. The last failed attempt removes the code. Minimum Inspector value is 1.")]
        protected int verificationCodeMaxAttempts = 5;

        [Header("Security"), SerializeField, Tooltip("Server secret used to derive authentication-token protection keys. Production builds require at least 32 UTF-8 bytes. Changing it invalidates legacy tokens; prefer the matching command-line setting for deployment secrets.")]
        protected string tokenSecret = PlaceholderTokenSecret;
        [SerializeField, Tooltip("Lifetime of a newly issued authentication token in days. Must be greater than 0; expired tokens require a new sign-in.")]
        protected int tokenExpiresInDays = 7;
        [SerializeField, Tooltip("Issuer claim written to and required from authentication tokens. It must remain identical across restarts and deployments that accept the same tokens.")]
        protected string tokenIssuer = "http://mst.game.com";
        [SerializeField, Tooltip("Audience claim written to and required from authentication tokens. Set it to the logical service that consumes these user tokens and keep it stable across compatible deployments.")]
        protected string tokenAudience = "http://mst.game.com/users";

        /// <summary>
        /// Database accessor factory that helps to create integration with accounts db
        /// </summary>
        [Tooltip("Factory that creates the authoritative accounts database accessor. It is required for registration, sign-in, token resolution, blocks and account updates."), SerializeField]
        protected DatabaseAccessorFactory databaseAccessorFactory;

        #endregion

        /// <summary>
        /// List of access keys
        /// </summary>
        protected readonly HashSet<string> accessKeys = new();

        /// <summary>
        /// Database accessor for accounts
        /// </summary>
        protected IAccountsDatabaseAccessor databaseAccessor;

        /// <summary>
        /// Censor module for bad words checking
        /// </summary>
        protected CensorModule censorModule;

        /// <summary>
        /// Collection of users who are currently logged in by user id
        /// </summary>
        protected readonly ConcurrentDictionary<string, IUserPeerExtension> loggedInUsers = new();

        private readonly ConcurrentDictionary<string, EmailSignInChallenge> emailSignInChallenges = new();
        private readonly ConcurrentDictionary<int, byte> activePeerAuthOperations = new();
        private readonly SemaphoreSlim[] accountIdentityLocks = CreateSemaphorePool(64);
        private readonly SemaphoreSlim[] sessionTransitionLocks =
            CreateSemaphorePool(SessionTransitionLockCount);
        private readonly object[] peerSignInLocks = CreateSyncPool(64);
        private readonly HashSet<string> pendingEmailSignInChallenges =
            new(StringComparer.Ordinal);
        private readonly object emailChallengeSync = new();
        private readonly Dictionary<string, long> emailOperationRequestTicks =
            new(StringComparer.Ordinal);
        private readonly object emailOperationSync = new();
        private readonly SemaphoreSlim passwordHashOperations =
            new(MaximumConcurrentPasswordHashOperations, MaximumConcurrentPasswordHashOperations);
        private int activeEmailOperationCount;

        /// <summary>
        /// Collection of users who are currently logged in
        /// </summary>
        public IEnumerable<IUserPeerExtension> LoggedInUsers => loggedInUsers.Values;

        /// <summary>
        /// Database accessor for accounts
        /// </summary>
        public IAccountsDatabaseAccessor DatabaseAccessor
        {
            get => databaseAccessor;
            set => databaseAccessor = value;
        }

        /// <summary>
        /// Database accessor factory
        /// </summary>
        public DatabaseAccessorFactory DatabaseAccessorFactory
        {
            get => databaseAccessorFactory;
            set => databaseAccessorFactory = value;
        }

        /// <summary>
        /// Invoked, when user logs in
        /// </summary>
        public event UserLoggedInEventHandlerDelegate OnUserLoggedInEvent;

        /// <summary>
        /// Invoked, when user logs out
        /// </summary>
        public event UserLoggedOutEventHandlerDelegate OnUserLoggedOutEvent;

        /// <summary>
        /// Invoked, when user successfully registers an account
        /// </summary>
        public event UserRegisteredEventHandlerDelegate OnUserRegisteredEvent;

        /// <summary>
        /// Invoked, when user successfully confirms his e-mail
        /// </summary>
        public event UserEmailConfirmedEventHandlerDelegate OnUserEmailConfirmedEvent;

        /// <summary>
        /// Called when the script instance is being loaded
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // Optional dependency to CensorModule
            AddOptionalDependency<CensorModule>();
        }

        /// <summary>
        /// Called when the script is loaded or a value is changed in the inspector
        /// </summary>
        protected virtual void OnValidate()
        {
            if (usernameMaxChars <= usernameMinChars)
                usernameMaxChars = usernameMinChars + 1;

            if (userPasswordMaxChars < userPasswordMinChars)
                userPasswordMaxChars = userPasswordMinChars;

            emailSignInCodeLifetimeMinutes = Mathf.Max(1, emailSignInCodeLifetimeMinutes);
            emailSignInMaxAttempts = Mathf.Max(1, emailSignInMaxAttempts);
            emailOperationCooldownSeconds = Mathf.Max(1, emailOperationCooldownSeconds);
            verificationCodeLifetimeMinutes = Mathf.Max(1, verificationCodeLifetimeMinutes);
            verificationCodeMaxAttempts = Mathf.Max(1, verificationCodeMaxAttempts);

            if (tokenExpiresInDays < 0)
                tokenExpiresInDays = 1;
        }

        /// <summary>
        /// Initializes the module with the server
        /// </summary>
        /// <param name="server"></param>
        public override void Initialize(IServer server)
        {
            emailSignInChallenges.Clear();
            activePeerAuthOperations.Clear();

            lock (emailChallengeSync)
                pendingEmailSignInChallenges.Clear();

            lock (emailOperationSync)
            {
                emailOperationRequestTicks.Clear();
                activeEmailOperationCount = 0;
            }

            LoadTokenConfiguration();
            ValidateTokenConfiguration(Mst.Runtime.IsEditor);

            // Create database accessors if factory is provided
            if (databaseAccessorFactory != null)
                databaseAccessorFactory.CreateAccessors();

            // Get the database accessor for accounts
            databaseAccessor = Mst.Server.DbAccessors.GetAccessor<IAccountsDatabaseAccessor>();

            if (databaseAccessor == null)
            {
                logger.Fatal($"Account database implementation was not found in {GetType().Name}");
            }

            // Get the censor module
            censorModule = server.GetModule<CensorModule>();

            // Register message handlers for various operations
            server.RegisterMessageHandler(MstOpCodes.SignIn, SignInMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.SignUp, SignUpMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.SignOut, SignOutMessageHandler);

            server.RegisterMessageHandler(MstOpCodes.GetPasswordResetCode, GetPasswordResetCodeMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ChangePassword, ChangePasswordMessageHandler);

            server.RegisterMessageHandler(MstOpCodes.GetEmailConfirmationCode, GetEmailConfirmationCodeMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ConfirmEmail, ConfirmEmailMessageHandler);

            server.RegisterMessageHandler(MstOpCodes.GetAccountInfoByPeer, GetAccountInfoByPeerMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.GetAccountInfoByUsername, GetAccountInfoByUsernameMessageHandler);

            server.RegisterMessageHandler(MstOpCodes.SetProperties, SetPropertiesMessageHandler);
        }

        /// <inheritdoc />
        public void ValidateServerStartup()
        {
            LoadTokenConfiguration();
            ValidateTokenConfiguration(Mst.Runtime.IsEditor);
        }

        private void LoadTokenConfiguration()
        {
            tokenSecret = Mst.Args.AsString(Mst.Args.Names.TokenSecret, tokenSecret);
            tokenExpiresInDays = Mst.Args.AsInt(Mst.Args.Names.TokenExpiresInDays, tokenExpiresInDays);
            tokenIssuer = Mst.Args.AsString(Mst.Args.Names.TokenIssuer, tokenIssuer);
            tokenAudience = Mst.Args.AsString(Mst.Args.Names.TokenAudience, tokenAudience);
        }

        protected virtual void ValidateTokenConfiguration(bool isEditor)
        {
            if (isEditor)
                return;

            int secretByteCount = string.IsNullOrEmpty(tokenSecret)
                ? 0
                : Encoding.UTF8.GetByteCount(tokenSecret);
            bool usesKnownDefault =
                string.Equals(tokenSecret, LegacyDefaultTokenSecret, StringComparison.Ordinal) ||
                string.Equals(tokenSecret, PlaceholderTokenSecret, StringComparison.Ordinal);

            if (usesKnownDefault || secretByteCount < MinimumTokenSecretByteCount)
            {
                throw new InvalidOperationException(
                    $"{Mst.Args.Names.TokenSecret} must contain at least " +
                    $"{MinimumTokenSecretByteCount} UTF-8 bytes and must not use an MST default value");
            }

            if (tokenExpiresInDays <= 0)
            {
                throw new InvalidOperationException(
                    $"{Mst.Args.Names.TokenExpiresInDays} must be greater than zero");
            }

            if (string.IsNullOrWhiteSpace(tokenIssuer) ||
                string.IsNullOrWhiteSpace(tokenAudience))
            {
                throw new InvalidOperationException(
                    "Authentication token issuer and audience must be configured");
            }
        }

        /// <summary>
        /// Returns JSON information about the module
        /// </summary>
        /// <returns></returns>
        public override MstJson Details()
        {
            var info = base.Details();
            info.SetField("description", "This module provides secure player identity verification, account management, and access token issuance, allowing proper authorization before connecting to game services.");
            info["properties"].AddField("loggedIn", LoggedInUsers.Count());
            info["properties"].AddField("loggedInAsGuest", LoggedInUsers.Where(u => u.Account != null && u.Account.IsGuest).Count());
            info["properties"].AddField("inRooms", LoggedInUsers.Where(u => u.HasJoinedRoom()).Count());
            info["properties"].AddField("allowGuests", allowGuestLogin);
            info["properties"].AddField("guestNamePrefix", guestPrefix);
            info["properties"].AddField("emailConfirmRequired", emailConfirmRequired);
            info["properties"].AddField("minUsernameLength", usernameMinChars);
            info["properties"].AddField("minPasswordLength", userPasswordMinChars);

            return info;
        }

        /// <summary>
        /// Generates guest username
        /// </summary>
        /// <returns></returns>
        protected virtual string GenerateUsername()
        {
            string prefix = string.IsNullOrEmpty(guestPrefix) ? "user#" : guestPrefix;
            return $"{prefix}{Mst.Helper.CreateFriendlyId()}";
        }

        /// <summary>
        /// Notifies all subscribers when a user logs in and waits for their lifecycle work.
        /// </summary>
        /// <param name="user">Logged-in user.</param>
        /// <param name="cancellationToken">Cancellation token for the current server run.</param>
        public virtual async Task NotifyOnUserLoggedInEventAsync(IUserPeerExtension user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UserLoggedInEventHandlerDelegate handlers = OnUserLoggedInEvent;

            if (handlers == null)
                return;

            var subscriberTasks = new List<Task>();

            foreach (UserLoggedInEventHandlerDelegate handler in handlers.GetInvocationList())
            {
                try
                {
                    Task subscriberTask = handler(user, cancellationToken) ?? Task.CompletedTask;
                    subscriberTasks.Add(ObserveUserLoggedInSubscriberAsync(
                        handler,
                        subscriberTask,
                        user,
                        cancellationToken));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Server shutdown owns this cancellation.
                }
                catch (Exception ex)
                {
                    LogUserLoggedInSubscriberFailure(handler, user, ex);
                }
            }

            await Task.WhenAll(subscriberTasks);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task ObserveUserLoggedInSubscriberAsync(UserLoggedInEventHandlerDelegate handler,
            Task subscriberTask, IUserPeerExtension user, CancellationToken cancellationToken)
        {
            try
            {
                await subscriberTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Server shutdown owns this cancellation.
            }
            catch (Exception ex)
            {
                LogUserLoggedInSubscriberFailure(handler, user, ex);
            }
        }

        private void LogUserLoggedInSubscriberFailure(UserLoggedInEventHandlerDelegate handler,
            IUserPeerExtension user, Exception exception)
        {
            string subscriberName = handler.Method.DeclaringType == null
                ? handler.Method.Name
                : $"{handler.Method.DeclaringType.FullName}.{handler.Method.Name}";
            logger.Error($"Login lifecycle subscriber failed. subscriber={subscriberName}, userId={user?.UserId}, error={exception}");
        }

        /// <summary>
        /// Notify when user logs out
        /// </summary>
        /// <param name="user"></param>
        public virtual void NotifyOnUserLoggedOutEvent(IUserPeerExtension user)
        {
            UserLoggedOutEventHandlerDelegate handlers = OnUserLoggedOutEvent;

            if (handlers == null)
                return;

            foreach (UserLoggedOutEventHandlerDelegate handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(user);
                }
                catch (Exception exception)
                {
                    string subscriberName = handler.Method.DeclaringType == null
                        ? handler.Method.Name
                        : $"{handler.Method.DeclaringType.FullName}.{handler.Method.Name}";
                    logger.Error(
                        $"Logout lifecycle subscriber failed. " +
                        $"subscriber={subscriberName}, userId={user?.UserId}, error={exception}");
                }
            }
        }

        /// <summary>
        /// Get logged in user by username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public IUserPeerExtension GetLoggedInUserByUsername(string username)
        {
            return loggedInUsers.Values.Where(i => i.Username == username).FirstOrDefault();
        }

        /// <summary>
        /// Try to get logged in user by username
        /// </summary>
        /// <param name="username"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool TryGetLoggedInUserByUsername(string username, out IUserPeerExtension user)
        {
            user = GetLoggedInUserByUsername(username);
            return user != null;
        }

        /// <summary>
        /// Get logged in user by email
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public IUserPeerExtension GetLoggedInUserByEmail(string email)
        {
            return loggedInUsers.Values.Where(i => i.Account != null && i.Account.Email == email).FirstOrDefault();
        }

        /// <summary>
        /// Try to get logged in user by email
        /// </summary>
        /// <param name="email"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool TryGetLoggedInUserByEmail(string email, out IUserPeerExtension user)
        {
            user = GetLoggedInUserByEmail(email);
            return user != null;
        }

        /// <summary>
        /// Get logged in user by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IUserPeerExtension GetLoggedInUserById(string id)
        {
            loggedInUsers.TryGetValue(id, out IUserPeerExtension user);
            return user;
        }

        /// <summary>
        /// Try to get logged in user by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool TryGetLoggedInUserById(string id, out IUserPeerExtension user)
        {
            user = GetLoggedInUserById(id);
            return user != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public IUserPeerExtension GetLoggedInUserByExtraProperty(string key, string value)
        {
            foreach (var user in LoggedInUsers)
            {
                if (user.Account.ExtraProperties.ContainsKey(key) &&
                    user.Account.ExtraProperties[key] == value)
                {
                    return user;
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool TryGetLoggedInUserByExtraProperty(string key, string value, out IUserPeerExtension user)
        {
            user = GetLoggedInUserByExtraProperty(key, value);
            return user != null;
        }

        /// <summary>
        /// Get logged in users by their ids
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public IEnumerable<IUserPeerExtension> GetLoggedInUsersByIds(string[] ids)
        {
            List<IUserPeerExtension> list = new List<IUserPeerExtension>();

            foreach (string id in ids)
            {
                if (TryGetLoggedInUserById(id, out IUserPeerExtension user))
                {
                    list.Add(user);
                }
            }

            return list;
        }

        /// <summary>
        /// Check if given user is logged in by username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public bool IsUserLoggedInByUsername(string username)
        {
            var user = GetLoggedInUserByUsername(username);
            return user != null;
        }

        /// <summary>
        /// Check if given user is logged in by email
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public bool IsUserLoggedInByEmail(string email)
        {
            return TryGetLoggedInUserByEmail(NormalizeEmail(email), out _);
        }

        /// <summary>
        /// Check if given user is logged in by extra property
        /// </summary>
        /// <param name="property"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool IsUserLoggedInByExtraProperty(string property, string value)
        {
            foreach (var user in LoggedInUsers)
            {
                if (user.Account.ExtraProperties.ContainsKey(property) && user.Account.ExtraProperties[property] == value)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if given user is logged in by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsUserLoggedInById(string id)
        {
            return !string.IsNullOrEmpty(id) && loggedInUsers.ContainsKey(id);
        }

        /// <summary>
        /// Check if given peer has permission to get peer info
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public bool HasGetPeerInfoPermissions(IPeer peer)
        {
            var extension = peer.GetExtension<SecurityInfoPeerExtension>();
            return extension != null &&
                   (extension.HasPermission(MstPermissionKeys.RoomServer) ||
                    extension.HasAccountPermission(MstPermissionLevels.Admin));
        }

        /// <summary>
        /// Create instance of <see cref="UserPeerExtension"/>
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public virtual IUserPeerExtension CreateUserPeerExtension(IPeer peer)
        {
            return new UserPeerExtension(peer);
        }

        /// <summary>
        /// Gets or creates instance of <see cref="UserPeerExtension"/>
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public IUserPeerExtension GetOrCreateUserPeerExtension(IPeer peer)
        {
            var ext = peer.GetExtension<IUserPeerExtension>();
            return ext ?? peer.AddExtension(CreateUserPeerExtension(peer));
        }

        /// <summary>
        /// Removes user from signed users list by username
        /// </summary>
        /// <param name="username"></param>
        public void SignOut(string username)
        {
            if (TryGetLoggedInUserByUsername(username, out IUserPeerExtension user))
            {
                SignOut(user);
            }
        }

        /// <summary>
        /// Removes user from signed users list
        /// </summary>
        /// <param name="user"></param>
        public void SignOut(IUserPeerExtension user)
        {
            if (user == null || user.Account == null || user.Peer == null)
                return;

            bool removedCurrentSession;

            lock (GetPeerSignInLock(user.Peer.Id))
            {
                bool ownsPeerExtension = ReferenceEquals(
                    user.Peer.GetExtension<IUserPeerExtension>(),
                    user);

                if (ownsPeerExtension)
                    user.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;

                // Pair removal prevents a stale session from removing a newer mapping for the same user id.
                removedCurrentSession =
                    loggedInUsers.TryGetValue(user.UserId, out IUserPeerExtension currentUser) &&
                    ReferenceEquals(currentUser, user) &&
                    ((ICollection<KeyValuePair<string, IUserPeerExtension>>)loggedInUsers).Remove(
                        new KeyValuePair<string, IUserPeerExtension>(user.UserId, user));

                if (ownsPeerExtension)
                {
                    ResetPeerPermissionLevel(user.Peer);
                    user.Peer.ClearExtension<IUserPeerExtension>();
                }
            }

            if (removedCurrentSession)
                NotifyOnUserLoggedOutEvent(user);
        }

        /// <summary>
        /// Fired when any user disconnects from server
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnUserDisconnectedEventListener(IPeer peer)
        {
            var user = peer.GetExtension<IUserPeerExtension>();
            SignOut(user);
        }

        /// <summary>
        /// Check if username is valid
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        protected virtual bool IsUsernameValid(string username)
        {
            string lowerUserName = username?.ToLower();

            if (string.IsNullOrEmpty(lowerUserName))
            {
                return false;
            }

            if (lowerUserName.Contains(" "))
            {
                return false;
            }

            if ((username.Length < usernameMinChars || username.Length > usernameMaxChars))
            {
                return false;
            }

            if (censorModule != null && censorModule.ContainsBadWords(username))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if email is valid
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        protected virtual bool IsEmailValid(string email)
        {
            email = NormalizeEmail(email);
            return !string.IsNullOrEmpty(email) &&
                   email.Length <= 320 &&
                   Regex.IsMatch(email, emailAddressValidationTemplate);
        }

        /// <summary>
        /// Check if password is valid
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        protected virtual bool IsPasswordValid(string password)
        {
            return !string.IsNullOrEmpty(password?.Trim()) &&
                   password.Length >= userPasswordMinChars &&
                   password.Length <= userPasswordMaxChars;
        }

        protected virtual string NormalizeEmail(string email)
        {
            return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
        }

        protected virtual bool IsSameAccount(IAccountInfoData account, string accountId)
        {
            return account != null && !string.IsNullOrEmpty(accountId) && account.Id == accountId;
        }

        protected virtual async Task<bool> TryRespondAccountBlocked(IAccountInfoData account, IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (account == null)
                return false;

            var activeBlock = await databaseAccessor.GetActiveAccountBlockAsync(account.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (activeBlock == null || !activeBlock.IsActive())
                return false;

            logger.Warn($"Blocked account {account.Username} tried to sign in");
            DateTime blockedUntilUtc = activeBlock.BlockedUntil;

            if (blockedUntilUtc.Kind == DateTimeKind.Local)
                blockedUntilUtc = blockedUntilUtc.ToUniversalTime();
            else if (blockedUntilUtc.Kind == DateTimeKind.Unspecified)
                blockedUntilUtc = DateTime.SpecifyKind(blockedUntilUtc, DateTimeKind.Utc);

            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.REASON, activeBlock.BlockReason ?? string.Empty);
            properties.Set(
                MstErrorPropertyKeys.EXPIRES_AT,
                blockedUntilUtc.ToString("O", CultureInfo.InvariantCulture));
            message.RespondError(ResponseStatus.Banned, MstErrorCodes.ACCOUNT_BLOCKED, properties);
            return true;
        }

        protected virtual bool TryReadEncryptedCredentials(
            IIncomingMessage message,
            string purpose,
            out MstProperties userCredentials)
        {
            userCredentials = null;

            try
            {
                if (!Mst.Security.TryDecryptFromClient(
                        message.AsBytes(),
                        purpose,
                        message.OpCode,
                        message.Peer,
                        out byte[] decryptedBytesData))
                {
                    return false;
                }

                userCredentials = MstProperties.FromBytes(decryptedBytesData);
                return userCredentials != null;
            }
            catch (Exception e)
            {
                logger.Warn($"Invalid encrypted auth payload from peer {message.Peer.Id}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates user token
        /// </summary>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        protected virtual bool ValidateToken(MstProperties userCredentials)
        {
            return ValidateToken(userCredentials, out _, out _);
        }

        /// <summary>
        /// Validates a user token and returns the account identity protected by its signature.
        /// </summary>
        protected virtual bool ValidateToken(
            MstProperties userCredentials,
            out string accountId,
            out string username)
        {
            return ValidateToken(userCredentials, out accountId, out username, out _);
        }

        private bool ValidateToken(
            MstProperties userCredentials,
            out string accountId,
            out string username,
            out int revision)
        {
            accountId = null;
            username = null;
            revision = 0;

            try
            {
                if (userCredentials == null)
                    return false;

                string token = userCredentials.AsString(MstParamKeys.USER_AUTH_TOKEN);

                if (string.IsNullOrEmpty(token) ||
                    token.Length > MstNetworkLimits.MaxAuthenticationTokenCharacterCount)
                {
                    return false;
                }

                string decryptedToken;

                if (token.StartsWith("v2.", StringComparison.Ordinal))
                {
                    if (!Mst.Security.TryDecrypt(
                            token.Substring(3),
                            MstSecurityPurposes.AuthenticationToken,
                            out decryptedToken))
                    {
                        return false;
                    }
                }
                else if (!TryDecryptLegacyToken(token, out decryptedToken))
                {
                    return false;
                }

                return TryReadTokenPayload(
                    decryptedToken,
                    out accountId,
                    out username,
                    out revision);
            }
            catch (Exception e)
            {
                logger.Warn($"Invalid authentication token: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates and saves account token
        /// </summary>
        /// <param name="account"></param>
        protected virtual async Task<string> CreateAccountToken(IAccountInfoData account, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dt = DateTimeOffset.UtcNow.AddDays(tokenExpiresInDays);
            int revision = await databaseAccessor.GetAuthTokenRevisionAsync(
                account.Id,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var tokenJson = MstJson.CreateArray();
            tokenJson.Add(account.Id);
            tokenJson.Add(account.Username);
            tokenJson.Add(dt.ToUnixTimeSeconds());
            tokenJson.Add(tokenIssuer);
            tokenJson.Add(tokenAudience);
            tokenJson.Add(revision);

            string protectedToken = Mst.Security.Encrypt(
                tokenJson.ToString(),
                MstSecurityPurposes.AuthenticationToken);
            string finalToken = $"v2.{protectedToken}";
            account.Token = finalToken;
            await databaseAccessor.InsertOrUpdateTokenAsync(account, account.Token, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return finalToken;
        }

        /// <summary>
        /// Finalizes sign-in logic for a specific account.
        /// </summary>
        /// <param name="account"></param>
        /// <param name="message"></param>
        protected Task FinalizeSingIn(
            IAccountInfoData account,
            IIncomingMessage message,
            CancellationToken cancellationToken)
        {
            return FinalizeSingIn(account, message, false, cancellationToken);
        }

        /// <summary>
        /// Finalizes sign-in and optionally issues a new remember-me token after
        /// this peer has reserved exclusive ownership of the account session.
        /// </summary>
        protected async Task FinalizeSingIn(
            IAccountInfoData account,
            IIncomingMessage message,
            bool createToken,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (account == null || string.IsNullOrEmpty(account.Id))
            {
                logger.Error("Cannot finalize sign-in for an account without an identifier");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.AUTH_INTERNAL_ERROR);
                return;
            }

            SemaphoreSlim[] sessionLocks = await AcquireSessionTransitionLocksAsync(
                account.Id,
                null,
                cancellationToken);

            try
            {
                await FinalizeSingInCore(
                    account,
                    message,
                    createToken,
                    cancellationToken);
            }
            finally
            {
                ReleaseSessionTransitionLocks(sessionLocks);
            }
        }

        private bool TryDecryptLegacyToken(string token, out string decryptedToken)
        {
            decryptedToken = null;
            string[] parts = token.Split('.');

            if (parts.Length != 2)
                return false;

            string encryptedToken = parts[0];
            string signature = parts[1];

            if (string.IsNullOrEmpty(encryptedToken) ||
                string.IsNullOrEmpty(signature) ||
                signature.Length > 128)
            {
                return false;
            }

            byte[] computedSignature;

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(tokenSecret)))
                computedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(encryptedToken));

            byte[] providedSignature = Convert.FromBase64String(signature);

            if (providedSignature.Length != computedSignature.Length ||
                !MstSecurity.FixedTimeEquals(computedSignature, providedSignature))
            {
                return false;
            }

            decryptedToken = MstLegacyTokenCrypto.Decrypt(encryptedToken, tokenSecret);
            return true;
        }

        private bool TryReadTokenPayload(
            string decryptedToken,
            out string accountId,
            out string username,
            out int revision)
        {
            accountId = null;
            username = null;
            revision = 0;
            var tokenData = new MstJson(decryptedToken);

            if (!tokenData.IsArray || (tokenData.Count != 5 && tokenData.Count != 6))
                return false;

            if (!tokenData[0].IsString ||
                !tokenData[1].IsString ||
                !tokenData[2].IsNumber ||
                !tokenData[2].IsInteger ||
                !tokenData[3].IsString ||
                !tokenData[4].IsString ||
                (tokenData.Count == 6 &&
                 (!tokenData[5].IsNumber || !tokenData[5].IsInteger)))
            {
                return false;
            }

            string tokenAccountId = tokenData[0].StringValue;
            string tokenUsername = tokenData[1].StringValue;

            if (string.IsNullOrWhiteSpace(tokenAccountId) ||
                string.IsNullOrWhiteSpace(tokenUsername))
            {
                return false;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= tokenData[2].LongValue)
                return false;

            if (!string.Equals(tokenData[3].StringValue, tokenIssuer, StringComparison.Ordinal) ||
                !string.Equals(tokenData[4].StringValue, tokenAudience, StringComparison.Ordinal))
            {
                return false;
            }

            int tokenRevision = tokenData.Count == 6 ? tokenData[5].IntValue : 0;

            if (tokenRevision < 0)
                return false;

            accountId = tokenAccountId;
            username = tokenUsername;
            revision = tokenRevision;
            return true;
        }

        private async Task FinalizeSingInCore(
            IAccountInfoData account,
            IIncomingMessage message,
            bool createToken,
            CancellationToken cancellationToken)
        {
            if (loggedInUsers.ContainsKey(account.Id))
            {
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED);
                return;
            }

            lock (GetPeerSignInLock(message.Peer.Id))
            {
                if (message.Peer.GetExtension<IUserPeerExtension>() != null)
                {
                    message.RespondError(
                        ResponseStatus.DuplicateLogin,
                        MstErrorCodes.PEER_ALREADY_AUTHENTICATED);
                    return;
                }
            }

            await PrepareAccountSessionAsync(account, createToken, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            IUserPeerExtension userPeerExtension = CreateUserPeerExtension(message.Peer);
            userPeerExtension.Account = account;
            bool sessionAttached = false;

            try
            {
                lock (GetPeerSignInLock(message.Peer.Id))
                {
                    if (!message.Peer.IsConnected)
                    {
                        message.RespondError(
                            ResponseStatus.NotConnected,
                            MstErrorCodes.AUTH_SESSION_CONFLICT);
                        return;
                    }

                    if (message.Peer.GetExtension<IUserPeerExtension>() != null)
                    {
                        message.RespondError(
                            ResponseStatus.DuplicateLogin,
                            MstErrorCodes.PEER_ALREADY_AUTHENTICATED);
                        return;
                    }

                    try
                    {
                        message.Peer.AddExtension(userPeerExtension);
                        userPeerExtension.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                        userPeerExtension.Peer.OnConnectionCloseEvent += OnUserDisconnectedEventListener;

                        if (!message.Peer.IsConnected)
                        {
                            userPeerExtension.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                            message.Peer.ClearExtension<IUserPeerExtension>();
                            userPeerExtension.Account = null;
                            message.RespondError(
                                ResponseStatus.NotConnected,
                                MstErrorCodes.AUTH_SESSION_CONFLICT);
                            return;
                        }

                        if (!loggedInUsers.TryAdd(account.Id, userPeerExtension))
                        {
                            userPeerExtension.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                            message.Peer.ClearExtension<IUserPeerExtension>();
                            userPeerExtension.Account = null;
                            message.RespondError(
                                ResponseStatus.DuplicateLogin,
                                MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED);
                            return;
                        }

                        ApplyAuthenticatedPermissionLevel(userPeerExtension);
                        sessionAttached = true;
                    }
                    catch
                    {
                        RollBackPendingSignIn(account.Id, userPeerExtension);
                        throw;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                Task loginLifecycleTask =
                    NotifyOnUserLoggedInEventAsync(userPeerExtension, cancellationToken);

                try
                {
                    // Preserve the existing response timing while keeping post-login work owned by this handler.
                    cancellationToken.ThrowIfCancellationRequested();
                    message.Respond(userPeerExtension.CreateAccountInfoPacket(), ResponseStatus.Success);
                }
                finally
                {
                    await loginLifecycleTask;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                if (!sessionAttached)
                    RollBackPendingSignIn(account.Id, userPeerExtension);
            }
        }

        /// <summary>
        /// Persists the successful login timestamp and optionally issues the account token
        /// before the session becomes visible to peers and other modules.
        /// </summary>
        protected virtual async Task PrepareAccountSessionAsync(
            IAccountInfoData account,
            bool createToken,
            CancellationToken cancellationToken)
        {
            account.LastLoginAt =
                await databaseAccessor.UpdateLastLoginAsync(account.Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (createToken)
                await CreateAccountToken(account, cancellationToken);
        }

        /// <summary>
        /// Finalizes sign-in for an already validated identity and replaces the authenticated
        /// session currently owned by this peer. The caller controls whether the target account's
        /// existing session may also be replaced.
        /// </summary>
        /// <remarks>
        /// This method is intended for validated identity-transition flows such as linking an
        /// external platform account or replacing a guest session with a password-authenticated
        /// account. Ordinary repeated sign-in must continue using
        /// <see cref="FinalizeSingIn(IAccountInfoData, IIncomingMessage, CancellationToken)"/>
        /// so duplicate sessions remain rejected.
        /// </remarks>
        /// <param name="account">Validated account that must become the peer identity.</param>
        /// <param name="message">Authentication request that receives the final response.</param>
        /// <param name="cancellationToken">Cancellation token for the current server run.</param>
        /// <param name="createToken">Whether a persistent authentication token must be created.</param>
        /// <param name="allowTargetSessionTakeover">Whether an existing session for the target account may be replaced.</param>
        protected async Task FinalizeSingInWithSessionReplacement(
            IAccountInfoData account,
            IIncomingMessage message,
            CancellationToken cancellationToken,
            bool createToken = false,
            bool allowTargetSessionTakeover = true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (account == null || string.IsNullOrEmpty(account.Id))
            {
                logger.Error("Cannot replace sign-in session with an account without an identifier");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.AUTH_INTERNAL_ERROR);
                return;
            }

            IPeer peer = message.Peer;
            IUserPeerExtension currentPeerUser = peer.GetExtension<IUserPeerExtension>();

            if (currentPeerUser != null && currentPeerUser.Account == null)
            {
                lock (GetPeerSignInLock(peer.Id))
                {
                    if (!ReferenceEquals(peer.GetExtension<IUserPeerExtension>(), currentPeerUser))
                    {
                        message.RespondError(
                            ResponseStatus.DuplicateLogin,
                            MstErrorCodes.AUTH_SESSION_CONFLICT);
                        return;
                    }

                    peer.ClearExtension<IUserPeerExtension>();
                }

                currentPeerUser = null;
            }

            if (currentPeerUser == null)
            {
                SemaphoreSlim[] targetSessionLocks =
                    await AcquireSessionTransitionLocksAsync(
                        account.Id,
                        null,
                        cancellationToken);

                try
                {
                    if (!loggedInUsers.TryGetValue(
                            account.Id,
                            out IUserPeerExtension displacedTargetUser))
                    {
                        await FinalizeSingInCore(
                            account,
                            message,
                            createToken,
                            cancellationToken);
                    }
                    else if (allowTargetSessionTakeover)
                    {
                        await FinalizeTargetSessionTakeoverCore(
                            account,
                            message,
                            displacedTargetUser,
                            cancellationToken,
                            createToken);
                    }
                    else
                    {
                        message.RespondError(
                            ResponseStatus.DuplicateLogin,
                            MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED);
                    }
                }
                finally
                {
                    ReleaseSessionTransitionLocks(targetSessionLocks);
                }

                return;
            }

            SemaphoreSlim[] sessionLocks = await AcquireSessionTransitionLocksAsync(
                currentPeerUser.UserId,
                account.Id,
                cancellationToken);

            try
            {
                await FinalizeSessionReplacementCore(
                    account,
                    message,
                    currentPeerUser,
                    cancellationToken,
                    createToken,
                    allowTargetSessionTakeover);
            }
            finally
            {
                ReleaseSessionTransitionLocks(sessionLocks);
            }
        }

        private async Task FinalizeSessionReplacementCore(
            IAccountInfoData account,
            IIncomingMessage message,
            IUserPeerExtension currentPeerUser,
            CancellationToken cancellationToken,
            bool createToken,
            bool allowTargetSessionTakeover)
        {
            IPeer peer = message.Peer;
            bool isSameAccount = IsSameAccount(currentPeerUser.Account, account.Id);

            if (!isSameAccount && currentPeerUser.HasJoinedRoom())
            {
                logger.Warn(
                    $"Cannot replace authenticated identity while peer is joined to a room. " +
                    $"PeerId={peer.Id}, AccountId={currentPeerUser.UserId}, " +
                    $"RoomId={currentPeerUser.JoinedRoomID}");
                message.RespondError(
                    ResponseStatus.Conflict,
                    MstErrorCodes.ACCOUNT_SWITCH_REQUIRES_LEAVING_ROOM);
                return;
            }

            if (!isSameAccount &&
                !allowTargetSessionTakeover &&
                loggedInUsers.TryGetValue(account.Id, out IUserPeerExtension activeTargetUser) &&
                !ReferenceEquals(activeTargetUser, currentPeerUser))
            {
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED);
                return;
            }

            await PrepareAccountSessionAsync(account, createToken, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (isSameAccount)
            {
                lock (GetPeerSignInLock(peer.Id))
                {
                    if (!ReferenceEquals(peer.GetExtension<IUserPeerExtension>(), currentPeerUser) ||
                        !loggedInUsers.TryGetValue(account.Id, out IUserPeerExtension registeredUser) ||
                        !ReferenceEquals(registeredUser, currentPeerUser))
                    {
                        message.RespondError(
                            ResponseStatus.DuplicateLogin,
                            MstErrorCodes.AUTH_SESSION_CONFLICT);
                        return;
                    }

                    currentPeerUser.Account = account;
                    ApplyAuthenticatedPermissionLevel(currentPeerUser);
                }

                cancellationToken.ThrowIfCancellationRequested();
                message.Respond(currentPeerUser.CreateAccountInfoPacket(), ResponseStatus.Success);
                return;
            }

            var replacementUser = CreateUserPeerExtension(peer);
            replacementUser.Account = account;
            IUserPeerExtension displacedTargetUser = null;
            bool replacementAttached = false;
            ResponseStatus replacementFailureStatus = ResponseStatus.Success;
            string replacementFailureCode = null;
            loggedInUsers.TryGetValue(account.Id, out IUserPeerExtension targetSessionSnapshot);

            ExecuteWithPeerSignInLocks(peer, targetSessionSnapshot?.Peer, () =>
            {
                if (!peer.IsConnected)
                {
                    replacementFailureStatus = ResponseStatus.NotConnected;
                    replacementFailureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    return;
                }

                if (!ReferenceEquals(peer.GetExtension<IUserPeerExtension>(), currentPeerUser) ||
                    !loggedInUsers.TryGetValue(currentPeerUser.UserId, out IUserPeerExtension registeredCurrentUser) ||
                    !ReferenceEquals(registeredCurrentUser, currentPeerUser))
                {
                    replacementFailureStatus = ResponseStatus.DuplicateLogin;
                    replacementFailureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    return;
                }

                loggedInUsers.TryGetValue(account.Id, out IUserPeerExtension targetUser);

                if (targetUser != null &&
                    !ReferenceEquals(targetUser, targetSessionSnapshot))
                {
                    replacementFailureStatus = ResponseStatus.DuplicateLogin;
                    replacementFailureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    return;
                }

                if (targetUser != null && !allowTargetSessionTakeover)
                {
                    replacementFailureStatus = ResponseStatus.DuplicateLogin;
                    replacementFailureCode = MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED;
                    return;
                }

                if (targetUser != null &&
                    (ReferenceEquals(targetUser, currentPeerUser) ||
                     ReferenceEquals(targetUser.Peer, peer)))
                {
                    logger.Error(
                        $"Cannot replace inconsistent session mapping. " +
                        $"PeerId={peer.Id}, CurrentAccountId={currentPeerUser.UserId}, " +
                        $"TargetAccountId={account.Id}");
                    replacementFailureStatus = ResponseStatus.Error;
                    replacementFailureCode = MstErrorCodes.AUTH_INTERNAL_ERROR;
                    return;
                }

                if (targetUser != null && targetUser.HasJoinedRoom())
                {
                    replacementFailureStatus = ResponseStatus.Conflict;
                    replacementFailureCode = MstErrorCodes.TARGET_ACCOUNT_REQUIRES_LEAVING_ROOM;
                    return;
                }

                ResetPeerPermissionLevel(peer);

                bool targetSessionPublished = targetUser == null
                    ? loggedInUsers.TryAdd(account.Id, replacementUser)
                    : loggedInUsers.TryUpdate(account.Id, replacementUser, targetUser);

                if (!targetSessionPublished)
                {
                    ApplyAuthenticatedPermissionLevel(currentPeerUser);
                    replacementFailureStatus = ResponseStatus.DuplicateLogin;
                    replacementFailureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    return;
                }

                if (!((ICollection<KeyValuePair<string, IUserPeerExtension>>)loggedInUsers).Remove(
                    new KeyValuePair<string, IUserPeerExtension>(currentPeerUser.UserId, currentPeerUser)))
                {
                    RestoreReplacedSession(account.Id, replacementUser, targetUser);
                    ApplyAuthenticatedPermissionLevel(currentPeerUser);
                    replacementFailureStatus = ResponseStatus.DuplicateLogin;
                    replacementFailureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    return;
                }

                try
                {
                    currentPeerUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                    peer.AddExtension<IUserPeerExtension>(replacementUser);
                    replacementUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                    replacementUser.Peer.OnConnectionCloseEvent += OnUserDisconnectedEventListener;

                    if (!peer.IsConnected)
                        throw new InvalidOperationException("Peer disconnected during account session replacement");

                    ApplyAuthenticatedPermissionLevel(replacementUser);

                    if (targetUser != null &&
                        ReferenceEquals(
                            targetUser.Peer.GetExtension<IUserPeerExtension>(),
                            targetUser))
                    {
                        targetUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                        ResetPeerPermissionLevel(targetUser.Peer);
                        targetUser.Peer.ClearExtension<IUserPeerExtension>();
                    }

                    displacedTargetUser = targetUser;
                    replacementAttached = true;
                }
                catch (Exception exception)
                {
                    replacementUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;

                    if (!ReferenceEquals(
                            peer.GetExtension<IUserPeerExtension>(),
                            currentPeerUser))
                    {
                        peer.AddExtension<IUserPeerExtension>(currentPeerUser);
                    }

                    currentPeerUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                    currentPeerUser.Peer.OnConnectionCloseEvent += OnUserDisconnectedEventListener;
                    RestoreReplacedSession(account.Id, replacementUser, targetUser);
                    loggedInUsers.TryAdd(currentPeerUser.UserId, currentPeerUser);
                    ApplyAuthenticatedPermissionLevel(currentPeerUser);

                    if (targetUser != null && targetUser.Peer.IsConnected &&
                        !ReferenceEquals(
                            targetUser.Peer.GetExtension<IUserPeerExtension>(),
                            targetUser))
                    {
                        targetUser.Peer.AddExtension<IUserPeerExtension>(targetUser);
                        targetUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                        targetUser.Peer.OnConnectionCloseEvent += OnUserDisconnectedEventListener;
                        ApplyAuthenticatedPermissionLevel(targetUser);
                    }

                    if (!peer.IsConnected)
                    {
                        replacementFailureStatus = ResponseStatus.NotConnected;
                        replacementFailureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    }
                    else
                    {
                        logger.Error(
                            $"Failed to attach replacement account session. " +
                            $"PeerId={peer.Id}, AccountId={account.Id}, Error={exception}");
                        replacementFailureStatus = ResponseStatus.Error;
                        replacementFailureCode = MstErrorCodes.AUTH_INTERNAL_ERROR;
                    }
                }
            });

            if (!replacementAttached)
            {
                if (!peer.IsConnected)
                    SignOut(currentPeerUser);

                message.RespondError(replacementFailureStatus, replacementFailureCode);
                return;
            }

            await CompleteSessionReplacementAsync(
                account,
                message,
                replacementUser,
                currentPeerUser,
                displacedTargetUser,
                cancellationToken);
        }

        private async Task FinalizeTargetSessionTakeoverCore(
            IAccountInfoData account,
            IIncomingMessage message,
            IUserPeerExtension displacedTargetUser,
            CancellationToken cancellationToken,
            bool createToken)
        {
            IPeer peer = message.Peer;

            if (ReferenceEquals(displacedTargetUser.Peer, peer))
            {
                logger.Error(
                    $"Cannot replace inconsistent target session mapping. " +
                    $"PeerId={peer.Id}, AccountId={account.Id}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.AUTH_INTERNAL_ERROR);
                return;
            }

            if (displacedTargetUser.HasJoinedRoom())
            {
                logger.Warn(
                    $"Cannot take over an account session while it is joined to a room. " +
                    $"AccountId={account.Id}, PeerId={displacedTargetUser.Peer.Id}, " +
                    $"RoomId={displacedTargetUser.JoinedRoomID}");
                message.RespondError(
                    ResponseStatus.Conflict,
                    MstErrorCodes.TARGET_ACCOUNT_REQUIRES_LEAVING_ROOM);
                return;
            }

            await PrepareAccountSessionAsync(account, createToken, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var replacementUser = CreateUserPeerExtension(peer);
            replacementUser.Account = account;
            bool replacementAttached = false;
            ResponseStatus failureStatus = ResponseStatus.Success;
            string failureCode = null;
            IUserPeerExtension committedDisplacedUser = null;

            ExecuteWithPeerSignInLocks(peer, displacedTargetUser.Peer, () =>
            {
                if (!peer.IsConnected)
                {
                    failureStatus = ResponseStatus.NotConnected;
                    failureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    return;
                }

                if (peer.GetExtension<IUserPeerExtension>() != null)
                {
                    failureStatus = ResponseStatus.DuplicateLogin;
                    failureCode = MstErrorCodes.PEER_ALREADY_AUTHENTICATED;
                    return;
                }

                loggedInUsers.TryGetValue(
                    account.Id,
                    out IUserPeerExtension activeTargetUser);

                if (activeTargetUser != null &&
                    !ReferenceEquals(activeTargetUser, displacedTargetUser))
                {
                    failureStatus = ResponseStatus.DuplicateLogin;
                    failureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    return;
                }

                if (activeTargetUser != null && activeTargetUser.HasJoinedRoom())
                {
                    failureStatus = ResponseStatus.Conflict;
                    failureCode = MstErrorCodes.TARGET_ACCOUNT_REQUIRES_LEAVING_ROOM;
                    return;
                }

                ResetPeerPermissionLevel(peer);

                bool sessionPublished = activeTargetUser == null
                    ? loggedInUsers.TryAdd(account.Id, replacementUser)
                    : loggedInUsers.TryUpdate(account.Id, replacementUser, activeTargetUser);

                if (!sessionPublished)
                {
                    failureStatus = ResponseStatus.DuplicateLogin;
                    failureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    return;
                }

                try
                {
                    peer.AddExtension<IUserPeerExtension>(replacementUser);
                    replacementUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                    replacementUser.Peer.OnConnectionCloseEvent += OnUserDisconnectedEventListener;

                    if (!peer.IsConnected)
                        throw new InvalidOperationException("Peer disconnected during account session takeover");

                    ApplyAuthenticatedPermissionLevel(replacementUser);

                    if (activeTargetUser != null &&
                        ReferenceEquals(
                            activeTargetUser.Peer.GetExtension<IUserPeerExtension>(),
                            activeTargetUser))
                    {
                        activeTargetUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                        ResetPeerPermissionLevel(activeTargetUser.Peer);
                        activeTargetUser.Peer.ClearExtension<IUserPeerExtension>();
                    }

                    committedDisplacedUser = activeTargetUser;
                    replacementAttached = true;
                }
                catch (Exception exception)
                {
                    replacementUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;

                    if (activeTargetUser != null)
                    {
                        loggedInUsers.TryUpdate(
                            account.Id,
                            activeTargetUser,
                            replacementUser);
                    }
                    else
                    {
                        ((ICollection<KeyValuePair<string, IUserPeerExtension>>)loggedInUsers).Remove(
                            new KeyValuePair<string, IUserPeerExtension>(account.Id, replacementUser));
                    }

                    if (ReferenceEquals(
                            peer.GetExtension<IUserPeerExtension>(),
                            replacementUser))
                    {
                        peer.ClearExtension<IUserPeerExtension>();
                    }

                    ResetPeerPermissionLevel(peer);

                    if (activeTargetUser != null && activeTargetUser.Peer.IsConnected &&
                        !ReferenceEquals(
                            activeTargetUser.Peer.GetExtension<IUserPeerExtension>(),
                            activeTargetUser))
                    {
                        activeTargetUser.Peer.AddExtension<IUserPeerExtension>(activeTargetUser);
                        activeTargetUser.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                        activeTargetUser.Peer.OnConnectionCloseEvent += OnUserDisconnectedEventListener;
                        ApplyAuthenticatedPermissionLevel(activeTargetUser);
                    }

                    if (!peer.IsConnected)
                    {
                        failureStatus = ResponseStatus.NotConnected;
                        failureCode = MstErrorCodes.AUTH_SESSION_CONFLICT;
                    }
                    else
                    {
                        logger.Error(
                            $"Failed to attach taken-over account session. " +
                            $"PeerId={peer.Id}, AccountId={account.Id}, Error={exception}");
                        failureStatus = ResponseStatus.Error;
                        failureCode = MstErrorCodes.AUTH_INTERNAL_ERROR;
                    }
                }
            });

            if (!replacementAttached)
            {
                message.RespondError(failureStatus, failureCode);
                return;
            }

            await CompleteSessionReplacementAsync(
                account,
                message,
                replacementUser,
                null,
                committedDisplacedUser,
                cancellationToken);
        }

        private async Task CompleteSessionReplacementAsync(
            IAccountInfoData account,
            IIncomingMessage message,
            IUserPeerExtension replacementUser,
            IUserPeerExtension currentPeerUser,
            IUserPeerExtension displacedTargetUser,
            CancellationToken cancellationToken)
        {
            if (currentPeerUser != null)
                NotifyOnUserLoggedOutEvent(currentPeerUser);

            if (displacedTargetUser != null &&
                !ReferenceEquals(displacedTargetUser, currentPeerUser))
            {
                logger.Warn(
                    $"Replacing active account session. AccountId={account.Id}, " +
                    $"OldPeerId={displacedTargetUser.Peer.Id}, " +
                    $"NewPeerId={replacementUser.Peer.Id}");
                DisconnectReplacedSession(displacedTargetUser);
            }

            Task loginLifecycleTask =
                NotifyOnUserLoggedInEventAsync(replacementUser, cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                message.Respond(replacementUser.CreateAccountInfoPacket(), ResponseStatus.Success);
            }
            finally
            {
                await loginLifecycleTask;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private void RollBackPendingSignIn(string accountId, IUserPeerExtension user)
        {
            ((ICollection<KeyValuePair<string, IUserPeerExtension>>)loggedInUsers).Remove(
                new KeyValuePair<string, IUserPeerExtension>(accountId, user));

            lock (GetPeerSignInLock(user.Peer.Id))
            {
                if (ReferenceEquals(user.Peer.GetExtension<IUserPeerExtension>(), user))
                {
                    user.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                    ResetPeerPermissionLevel(user.Peer);
                    user.Peer.ClearExtension<IUserPeerExtension>();
                }
            }

            user.Account = null;
        }

        private void RestoreReplacedSession(
            string accountId,
            IUserPeerExtension replacementUser,
            IUserPeerExtension displacedUser)
        {
            if (displacedUser != null)
            {
                loggedInUsers.TryUpdate(accountId, displacedUser, replacementUser);
                return;
            }

            ((ICollection<KeyValuePair<string, IUserPeerExtension>>)loggedInUsers).Remove(
                new KeyValuePair<string, IUserPeerExtension>(accountId, replacementUser));
        }

        private void DisconnectReplacedSession(IUserPeerExtension user)
        {
            NotifyOnUserLoggedOutEvent(user);
            user.Peer.Disconnect("You have logged in from another device");
        }

        /// <summary>
        /// Applies server-side permission level that belongs to the authenticated account.
        /// </summary>
        /// <param name="user">Authenticated user peer extension.</param>
        protected virtual void ApplyAuthenticatedPermissionLevel(IUserPeerExtension user)
        {
            if (user?.Peer == null)
                return;

            var securityExtension = user.Peer.GetExtension<SecurityInfoPeerExtension>();

            if (securityExtension == null)
                return;

            securityExtension.ResetAccountPermissionLevel();

            if (IsConfiguredAdminAccount(user.Account) &&
                TryGetPermissionLevel(MstPermissionKeys.Admin, out int adminPermissionLevel))
            {
                securityExtension.SetAccountPermissionLevel(adminPermissionLevel);
            }
        }

        /// <summary>
        /// Determines whether the account matches the configured admin account identifier.
        /// </summary>
        /// <param name="account">Authenticated account.</param>
        /// <returns><c>true</c> when the account is configured as the admin account.</returns>
        protected virtual bool IsConfiguredAdminAccount(IAccountInfoData account)
        {
            return account != null &&
                   !string.IsNullOrWhiteSpace(Mst.Args.AdminId) &&
                   string.Equals(account.Id, Mst.Args.AdminId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Resets peer permission level to the public default level.
        /// </summary>
        /// <param name="peer">Peer whose permission level should be reset.</param>
        protected virtual void ResetPeerPermissionLevel(IPeer peer)
        {
            var securityExtension = peer?.GetExtension<SecurityInfoPeerExtension>();

            if (securityExtension != null)
                securityExtension.ResetAccountPermissionLevel();
        }

        private bool TryRespondVerificationCodeFailure(
            IIncomingMessage message,
            VerificationCodeResult result,
            string invalidCode,
            string expiredCode,
            string attemptsExceededCode)
        {
            if (result == VerificationCodeResult.Success)
                return false;

            string errorCode;

            switch (result)
            {
                case VerificationCodeResult.Expired:
                    errorCode = expiredCode;
                    break;
                case VerificationCodeResult.AttemptsExceeded:
                    errorCode = attemptsExceededCode;
                    break;
                default:
                    errorCode = invalidCode;
                    break;
            }

            message.RespondError(ResponseStatus.Invalid, errorCode);
            return true;
        }

        #region MESSAGE HANDLERS

        /// <summary>
        /// Handles client's request to change password
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task ChangePasswordMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = MstProperties.FromBytes(message.AsBytes());

            string code = data.AsString(MstParamKeys.RESET_PASSWORD_CODE);
            string email = NormalizeEmail(data.AsString(MstParamKeys.RESET_PASSWORD_EMAIL));
            string password = data.AsString(MstParamKeys.RESET_PASSWORD);

            if (string.IsNullOrEmpty(code))
            {
                logger.Error("Invalid password change request. Code required");
                message.RespondError(
                    ResponseStatus.Invalid,
                    MstErrorCodes.PASSWORD_RESET_CODE_REQUIRED);
                return;
            }

            if (IsEmailValid(email) == false)
            {
                logger.Error("Invalid password change request. Email required");
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_EMAIL);
                return;
            }

            if (IsPasswordValid(password) == false)
            {
                logger.Error("Invalid password change request. Wrong length of the password or it is empty");
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_PASSWORD);
                return;
            }

            IAccountInfoData account;
            string passwordHash;

            cancellationToken.ThrowIfCancellationRequested();
            // A valid one-time code is consumed here, so the password update must then finish.
            var passwordResetCodeResult = await databaseAccessor.CheckPasswordResetCodeAsync(
                email,
                code,
                CancellationToken.None);

            if (TryRespondVerificationCodeFailure(
                    message,
                    passwordResetCodeResult,
                    MstErrorCodes.PASSWORD_RESET_CODE_INVALID,
                    MstErrorCodes.PASSWORD_RESET_CODE_EXPIRED,
                    MstErrorCodes.PASSWORD_RESET_CODE_ATTEMPTS_EXCEEDED))
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.Warn($"Password reset code validation failed for {email}: {passwordResetCodeResult}");
                return;
            }

            if (TryGetLoggedInUserByEmail(email, out IUserPeerExtension user))
            {
                account = user.Account;
            }
            else
            {
                account = await databaseAccessor.GetAccountByEmailAsync(
                    email,
                    CancellationToken.None);
            }

            if (account == null)
            {
                logger.Error($"No account found for password change by email {email}");
                message.RespondError(ResponseStatus.NotFound, MstErrorCodes.AUTH_INTERNAL_ERROR);
                return;
            }

            using (await AcquirePasswordHashOperationAsync(CancellationToken.None))
                passwordHash = Mst.Security.CreateHash(password);

            await databaseAccessor.IncrementAuthTokenRevisionAsync(
                account.Id,
                CancellationToken.None);

            account.Password = passwordHash;
            account.Token = string.Empty;
            await databaseAccessor.UpdateAccountAsync(account, CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
            message.Respond(ResponseStatus.Success);
        }

        /// <summary>
        /// Handles password reset request
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task GetPasswordResetCodeMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userEmail = NormalizeEmail(message.AsString());

            if (!IsEmailValid(userEmail))
            {
                logger.Error("Invalid password reset request. Email required");
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_EMAIL);
                return;
            }

            if (mailer == null)
            {
                logger.Error($"Couldn't send password reset code to e-mail {userEmail}. Mailer is not configured");
                message.RespondError(
                    ResponseStatus.Error,
                    MstErrorCodes.EMAIL_SERVICE_UNAVAILABLE);
                return;
            }

            if (!TryReserveEmailOperation(
                    "password-reset",
                    userEmail,
                    message.Peer.Id,
                    out EmailOperationReservation emailOperation))
            {
                message.Respond(ResponseStatus.Success);
                return;
            }

            using var emailOperationScope = emailOperation;

            var userAccount = await databaseAccessor.GetAccountByEmailAsync(userEmail, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (userAccount == null)
            {
                logger.Debug($"Password reset requested for an unknown email by client {message.Peer.Id}");
                message.Respond(ResponseStatus.Success);
                return;
            }

            var passwordResetCode = CreateSecureRandomDigits(serviceCodeMinChars);

            StringBuilder emailBody = new StringBuilder();
            emailBody.Append($"<h3>You have requested reset password</h3>");
            emailBody.Append($"<p>Here is your reset code</p>");
            emailBody.Append($"<h1>{passwordResetCode}</h1>");
            emailBody.Append($"<p>Copy this code and paste it to your reset password form</p>");

            bool sentResult;

            try
            {
                sentResult = await mailer.SendMailAsync(
                    userAccount.Email,
                    "Password Reset Code",
                    emailBody.ToString(),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ReleaseEmailOperation(emailOperation);
                throw;
            }
            catch (Exception exception)
            {
                ReleaseEmailOperation(emailOperation);
                logger.Error($"Password reset mailer failed for {userAccount.Email}: {exception}");
                message.Respond(ResponseStatus.Success);
                return;
            }

            if (!sentResult)
            {
                ReleaseEmailOperation(emailOperation);
                cancellationToken.ThrowIfCancellationRequested();
                logger.Error($"Couldn't send an activation code to email {userAccount.Email}");
                message.Respond(ResponseStatus.Success);
                return;
            }

            try
            {
                // The code has already been delivered and must remain usable after shutdown starts.
                await databaseAccessor.SavePasswordResetCodeAsync(
                    userAccount.Email,
                    passwordResetCode,
                    DateTime.UtcNow.AddMinutes(verificationCodeLifetimeMinutes),
                    verificationCodeMaxAttempts,
                    CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.Error($"Couldn't save password reset code for e-mail {userAccount.Email}: {e.Message}");
                message.Respond(ResponseStatus.Success);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            message.Respond(ResponseStatus.Success);
        }

        /// <summary>
        /// Handles e-mail confirmation request
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task ConfirmEmailMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var confirmationCode = message.AsString();
            var userPeerExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userPeerExtension == null || userPeerExtension.Account == null)
            {
                message.RespondError(
                    ResponseStatus.Unauthorized,
                    MstErrorCodes.AUTHENTICATION_REQUIRED);
                return;
            }

            if (userPeerExtension.Account.IsGuest)
            {
                logger.Error("Guests cannot confirm e-mails");
                message.RespondError(
                    ResponseStatus.Unauthorized,
                    MstErrorCodes.GUEST_EMAIL_CONFIRMATION_FORBIDDEN);
                return;
            }

            if (userPeerExtension.Account.IsEmailConfirmed)
            {
                message.Respond(ResponseStatus.Success);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            // A valid one-time code is consumed here, so confirmation must then finish.
            var confirmationResult = await databaseAccessor.CheckEmailConfirmationCodeAsync(userPeerExtension.Account.Email, confirmationCode, CancellationToken.None);

            if (TryRespondVerificationCodeFailure(
                    message,
                    confirmationResult,
                    MstErrorCodes.EMAIL_CONFIRMATION_CODE_INVALID,
                    MstErrorCodes.EMAIL_CONFIRMATION_CODE_EXPIRED,
                    MstErrorCodes.EMAIL_CONFIRMATION_CODE_ATTEMPTS_EXCEEDED))
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.Warn($"Email confirmation code validation failed for {userPeerExtension.Account.Email}: {confirmationResult}");
                return;
            }

            // Confirm e-mail
            userPeerExtension.Account.IsEmailConfirmed = true;

            try
            {
                await databaseAccessor.UpdateAccountAsync(userPeerExtension.Account, CancellationToken.None);
            }
            catch (Exception e)
            {
                userPeerExtension.Account.IsEmailConfirmed = false;
                logger.Error($"Couldn't confirm e-mail {userPeerExtension.Account.Email}: {e.Message}");
                message.RespondError(
                    ResponseStatus.Error,
                    MstErrorCodes.EMAIL_CONFIRMATION_FAILED);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Respond with success
            message.Respond(ResponseStatus.Success);

            NotifyEmailConfirmed(userPeerExtension.Account);
        }

        private void NotifyEmailConfirmed(IAccountInfoData account)
        {
            try
            {
                OnUserEmailConfirmedEvent?.Invoke(account);
            }
            catch (Exception exception)
            {
                logger.Error($"Email confirmation subscriber failed: {exception}");
            }
        }

        /// <summary>
        /// Handles request to get email confirmation code
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task GetEmailConfirmationCodeMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userPeerExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userPeerExtension == null || userPeerExtension.Account == null)
            {
                message.RespondError(
                    ResponseStatus.Unauthorized,
                    MstErrorCodes.AUTHENTICATION_REQUIRED);
                return;
            }

            if (userPeerExtension.Account.IsGuest)
            {
                logger.Error("Guests cannot confirm e-mails");
                message.RespondError(
                    ResponseStatus.Invalid,
                    MstErrorCodes.GUEST_EMAIL_CONFIRMATION_FORBIDDEN);
                return;
            }

            if (mailer == null)
            {
                logger.Error($"Couldn't send a confirmation code to e-mail {userPeerExtension.Account.Email}. Mailer is not configured");
                message.RespondError(
                    ResponseStatus.Error,
                    MstErrorCodes.EMAIL_SERVICE_UNAVAILABLE);
                return;
            }

            var newEmailConfirmationCode = Mst.Helper.CreateRandomAlphanumericString(serviceCodeMinChars);

            StringBuilder emailBody = new StringBuilder();
            emailBody.Append($"<h3>You have requested email activation</h3>");
            emailBody.Append($"<p>Here is your email activation code</p>");
            emailBody.Append($"<h1>{newEmailConfirmationCode}</h1>");
            emailBody.Append($"<p>Copy this code and paste it to your account activation form</p>");

            bool sentResult = await mailer.SendMailAsync(userPeerExtension.Account.Email, "E-mail confirmation", emailBody.ToString(), cancellationToken);

            if (!sentResult)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.Error($"Couldn't send a confirmation code to e-mail {userPeerExtension.Account.Email}");
                message.RespondError(
                    ResponseStatus.Error,
                    MstErrorCodes.EMAIL_DELIVERY_FAILED);
                return;
            }

            try
            {
                // The code has already been delivered and must remain usable after shutdown starts.
                await databaseAccessor.SaveEmailConfirmationCodeAsync(
                    userPeerExtension.Account.Email,
                    newEmailConfirmationCode,
                    DateTime.UtcNow.AddMinutes(verificationCodeLifetimeMinutes),
                    verificationCodeMaxAttempts,
                    CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.Error($"Couldn't save confirmation code for e-mail {userPeerExtension.Account.Email}: {e.Message}");
                message.RespondError(
                    ResponseStatus.Error,
                    MstErrorCodes.EMAIL_CONFIRMATION_CODE_SAVE_FAILED);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Respond with success
            message.Respond(ResponseStatus.Success);
        }

        /// <summary>
        /// Handles request to get account info by username
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private Task GetAccountInfoByUsernameMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!HasGetPeerInfoPermissions(message.Peer))
            {
                message.RespondError(
                    ResponseStatus.Unauthorized,
                    MstErrorCodes.AUTH_PERMISSION_DENIED);
                return Task.CompletedTask;
            }

            string username = message.AsString();
            var userPeerExtension = GetLoggedInUserByUsername(username);

            if (userPeerExtension == null)
            {
                logger.Error($"User with a given username {username} is not in the game");
                message.RespondError(ResponseStatus.NotFound, MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                return Task.CompletedTask;
            }

            var userAccount = userPeerExtension.Account;

            var userAccountPacket = new RoomUserAccountInfoPacket()
            {
                PeerId = userPeerExtension.Peer.Id,
                ExtraProperties = userAccount.ExtraProperties,
                Username = userAccount.Username,
                UserId = userAccount.Id,
                IsGuest = userAccount.IsGuest,
                IsAdmin = userAccount.IsAdmin
            };

            message.Respond(userAccountPacket, ResponseStatus.Success);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a request to retrieve account information by peer
        /// </summary>
        /// <param name="message"></param>
        protected virtual Task GetAccountInfoByPeerMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!HasGetPeerInfoPermissions(message.Peer))
            {
                message.RespondError(
                    ResponseStatus.Unauthorized,
                    MstErrorCodes.AUTH_PERMISSION_DENIED);
                return Task.CompletedTask;
            }

            var userPeerId = message.AsInt();
            var userPeer = Server.GetPeer(userPeerId);

            if (userPeer == null)
            {
                logger.Error($"Peer with a given Id {userPeerId} is not in the game");
                message.RespondError(ResponseStatus.NotFound, MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                return Task.CompletedTask;
            }

            var userPeerExtension = userPeer.GetExtension<IUserPeerExtension>();

            if (userPeerExtension == null || userPeerExtension.Account == null)
            {
                logger.Error($"Peer with a given ID {userPeerId} is not authenticated");
                message.RespondError(
                    ResponseStatus.NotFound,
                    MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                return Task.CompletedTask;
            }

            var userAccount = userPeerExtension.Account;

            var userAccountPacket = new RoomUserAccountInfoPacket()
            {
                PeerId = userPeerId,
                ExtraProperties = userAccount.ExtraProperties,
                Username = userAccount.Username,
                UserId = userAccount.Id,
                IsGuest = userAccount.IsGuest,
                IsAdmin = userAccount.IsAdmin
            };

            message.Respond(userAccountPacket, ResponseStatus.Success);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles client requests to set account metadata.
        /// Extra properties are intentionally client-writable metadata. Do not treat them as authoritative
        /// gameplay, economy, permission, or entitlement state in server modules.
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task SetPropertiesMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userPeerExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userPeerExtension == null || userPeerExtension.Account == null)
            {
                logger.Error($"Some user has tried to set account properties but hi is not logged in");
                message.RespondError(
                    ResponseStatus.Unauthorized,
                    MstErrorCodes.AUTHENTICATION_REQUIRED);
                return;
            }

            var data = MstProperties.FromBytes(message.AsBytes());

            foreach (var property in data)
            {
                if (!userPeerExtension.Account.ExtraProperties.ContainsKey(property.Key))
                {
                    userPeerExtension.Account.ExtraProperties.Add(property.Key, property.Value);
                }
                else
                {
                    userPeerExtension.Account.ExtraProperties[property.Key] = property.Value;
                }
            }

            await databaseAccessor.UpdateAccountAsync(userPeerExtension.Account, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            message.Respond(ResponseStatus.Success);
        }

        /// <summary>
        /// Handles account registration request
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task SignUpMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using IDisposable authOperation = TryAcquirePeerAuthOperation(message.Peer.Id);

            if (authOperation == null)
            {
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.AUTH_OPERATION_IN_PROGRESS);
                return;
            }

            // Get peer extension
            var userPeerExtension = message.Peer.GetExtension<IUserPeerExtension>();

            // If user is logged in
            bool isLoggedIn = userPeerExtension != null;

            // If user is already logged in and he is not a guest
            if (isLoggedIn && userPeerExtension.Account.IsGuest == false)
            {
                logger.Error($"Player {userPeerExtension.Account.Username} is already logged in");
                message.RespondError(
                    ResponseStatus.Error,
                    MstErrorCodes.PEER_ALREADY_AUTHENTICATED);
                return;
            }

            // Get security extension
            var securityExt = message.Peer.GetExtension<SecurityInfoPeerExtension>();

            if (securityExt == null)
            {
                logger.Warn($"Security extension is missing for peer {message.Peer.Id}");
                message.RespondError(
                    ResponseStatus.Unauthorized,
                    MstErrorCodes.AUTH_SECURITY_CONTEXT_MISSING);
                return;
            }

            if (!TryReadEncryptedCredentials(
                    message,
                    MstSecurityPurposes.AuthSignUp,
                    out var userCredentials))
            {
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.AUTH_PAYLOAD_INVALID);
                return;
            }

            string userName = userCredentials.AsString(MstParamKeys.USER_NAME);
            string userPassword = userCredentials.AsString(MstParamKeys.USER_PASSWORD);
            string userEmail = NormalizeEmail(userCredentials.AsString(MstParamKeys.USER_EMAIL));

            // Check if length of our password is valid
            if (IsPasswordValid(userPassword) == false)
            {
                logger.Error("Invalid password");
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_PASSWORD);
                return;
            }

            // Check if username is valid
            if (IsUsernameValid(userName) == false)
            {
                logger.Error($"Invalid username [{userName}]");
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_USERNAME);
                return;
            }

            // Check if email is valid
            if (IsEmailValid(userEmail) == false)
            {
                logger.Error($"Invalid email [{userEmail}]");
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_EMAIL);
                return;
            }

            // Create account instance
            var userAccount = isLoggedIn ? userPeerExtension.Account : databaseAccessor.CreateAccountInstance();
            string currentAccountId = isLoggedIn ? userAccount.Id : null;
            SemaphoreSlim[] accountLocks = await AcquireAccountIdentityLocksAsync(
                userName,
                userEmail,
                cancellationToken);

            try
            {
                var existingAccountByUsername = await databaseAccessor.GetAccountByUsernameAsync(userName, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (existingAccountByUsername != null && !IsSameAccount(existingAccountByUsername, currentAccountId))
                {
                    logger.Error($"User with username {userName} already exists");
                    message.RespondError(
                        ResponseStatus.AlreadyExists,
                        MstErrorCodes.USERNAME_ALREADY_EXISTS);
                    return;
                }

                var existingAccountByEmail = await databaseAccessor.GetAccountByEmailAsync(userEmail, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (existingAccountByEmail != null && !IsSameAccount(existingAccountByEmail, currentAccountId))
                {
                    logger.Error($"User with email {userEmail} already exists");
                    message.RespondError(
                        ResponseStatus.AlreadyExists,
                        MstErrorCodes.EMAIL_ALREADY_EXISTS);
                    return;
                }

                IDisposable passwordHashOperation = TryAcquirePasswordHashOperation();

                if (passwordHashOperation == null)
                {
                    message.RespondError(
                        ResponseStatus.ServiceUnavailable,
                        MstErrorCodes.AUTH_SERVICE_BUSY);
                    return;
                }

                userAccount.Username = userName;
                userAccount.Email = userEmail;
                userAccount.IsGuest = false;

                using (passwordHashOperation)
                    userAccount.Password = Mst.Security.CreateHash(userPassword);

                // Let's set user email as confirmed if confirmation is not required by default
                userAccount.IsEmailConfirmed = !emailConfirmRequired;

                try
                {
                    if (isLoggedIn)
                    {
                        // Persist the regular credentials on the existing guest account.
                        await databaseAccessor.UpdateAccountAsync(userAccount, cancellationToken);
                    }
                    else
                    {
                        // Persist a new regular account.
                        await databaseAccessor.InsertAccountAsync(userAccount, cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    logger.Error($"Couldn't save account {userName}: {e.Message}");
                    message.RespondError(
                        ResponseStatus.Error,
                        MstErrorCodes.ACCOUNT_REGISTRATION_FAILED);
                    return;
                }
            }
            finally
            {
                ReleaseAccountIdentityLocks(accountLocks);
            }

            await FinalizeSingInWithSessionReplacement(
                userAccount,
                message,
                cancellationToken,
                createToken: true,
                allowTargetSessionTakeover: false);

            OnUserRegisteredEvent?.Invoke(message.Peer, userAccount);
        }

        /// <summary>
        /// Handles sign out request
        /// </summary>
        /// <param name="message"></param>
        protected virtual Task SignOutMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userPeerExtension = message.Peer.GetExtension<IUserPeerExtension>();
            SignOut(userPeerExtension);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a request to log in
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task SignInMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using IDisposable authOperation = TryAcquirePeerAuthOperation(message.Peer.Id);

            if (authOperation == null)
            {
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.AUTH_OPERATION_IN_PROGRESS);
                return;
            }

            // Get security extension of a peer
            var securityExt = message.Peer.GetExtension<SecurityInfoPeerExtension>();

            // If security ext is missing
            if (securityExt == null)
            {
                // No security context attached – handshake was not completed
                logger.Warn($"Security extension is missing for peer {message.Peer.Id}");
                message.Peer.Disconnect(string.Empty);
                return;
            }

            if (!TryReadEncryptedCredentials(
                    message,
                    MstSecurityPurposes.AuthSignIn,
                    out var userCredentials))
            {
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.AUTH_PAYLOAD_INVALID);
                return;
            }

            // Let's run auth factory
            await RunAuthFactory(userCredentials, message, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// This is auth factory to help you to extend auth module without global changes
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        protected virtual async Task RunAuthFactory(MstProperties userCredentials, IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Guest Authentication
            if (userCredentials.Has(MstParamKeys.USER_IS_GUEST))
            {
                await SignInAsGuest(userCredentials, message, cancellationToken);
            }
            // Token Authentication
            else if (userCredentials.Has(MstParamKeys.USER_AUTH_TOKEN))
            {
                await SignInWithToken(userCredentials, message, cancellationToken);
            }
            // Username / Password authentication
            else if (userCredentials.Has(MstParamKeys.USER_NAME) && userCredentials.Has(MstParamKeys.USER_PASSWORD))
            {
                await SignInWithLoginAndPassword(userCredentials, message, cancellationToken);
            }
            // Email authentication confirmation
            else if (userCredentials.Has(MstParamKeys.USER_EMAIL) &&
                     userCredentials.Has(MstParamKeys.USER_EMAIL_SIGN_IN_CODE))
            {
                await ConfirmEmailSignIn(userCredentials, message, cancellationToken);
            }
            // Email authentication request
            else if (userCredentials.Has(MstParamKeys.USER_EMAIL))
            {
                await SignInWithEmail(userCredentials, message, cancellationToken);
            }
            // Phone authentication is not implemented by the base module.
            else if (userCredentials.Has(MstParamKeys.USER_PHONE_NUMBER))
            {
                await SignInWithPhoneNumber(userCredentials, message, cancellationToken);
            }
            else
            {
                message.RespondError(
                    ResponseStatus.Invalid,
                    MstErrorCodes.UNSUPPORTED_AUTH_CREDENTIALS);
            }
        }

        protected virtual Task SignInWithPhoneNumber(MstProperties userCredentials, IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            message.RespondError(
                ResponseStatus.Invalid,
                MstErrorCodes.PHONE_SIGN_IN_UNSUPPORTED);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends a one-time email sign-in code without changing account credentials.
        /// </summary>
        protected virtual async Task SignInWithEmail(MstProperties userCredentials, IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userEmail = NormalizeEmail(userCredentials.AsString(MstParamKeys.USER_EMAIL));

            if (!IsEmailValid(userEmail))
            {
                logger.Warn($"Client {message.Peer.Id} requested email sign-in with an invalid address");
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_EMAIL);
                return;
            }

            if (mailer == null)
            {
                logger.Error("Email sign-in is unavailable because no mailer is configured");
                message.RespondError(
                    ResponseStatus.Error,
                    MstErrorCodes.EMAIL_SERVICE_UNAVAILABLE);
                return;
            }

            if (!TryReserveEmailOperation(
                    "email-sign-in",
                    userEmail,
                    message.Peer.Id,
                    out EmailOperationReservation emailOperation))
            {
                message.Respond(ResponseStatus.Success);
                return;
            }

            using var emailOperationScope = emailOperation;

            string code = CreateSecureRandomDigits(serviceCodeMinChars);
            var challenge = new EmailSignInChallenge(
                userEmail,
                code,
                DateTime.UtcNow.AddMinutes(emailSignInCodeLifetimeMinutes));

            if (!TryReservePendingEmailSignInChallenge(userEmail))
            {
                ReleaseEmailOperation(emailOperation);
                logger.Warn("Email sign-in challenge capacity reached");
                message.Respond(ResponseStatus.Success);
                return;
            }

            StringBuilder emailBody = new();
            emailBody.Append("<h3>Email sign-in requested</h3>");
            emailBody.Append("<p>Enter this one-time code to continue:</p>");
            emailBody.Append($"<h1>{code}</h1>");
            emailBody.Append($"<p>The code expires in {emailSignInCodeLifetimeMinutes} minutes.</p>");

            bool sentResult;

            try
            {
                sentResult = await mailer.SendMailAsync(
                    userEmail,
                    "Email Sign-In Code",
                    emailBody.ToString(),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CompletePendingEmailSignInChallenge(userEmail, challenge, false);
                ReleaseEmailOperation(emailOperation);
                throw;
            }
            catch (Exception exception)
            {
                CompletePendingEmailSignInChallenge(userEmail, challenge, false);
                ReleaseEmailOperation(emailOperation);
                logger.Error($"Email sign-in mailer failed for {userEmail}: {exception}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.EMAIL_DELIVERY_FAILED);
                return;
            }

            if (!sentResult)
            {
                CompletePendingEmailSignInChallenge(userEmail, challenge, false);
                ReleaseEmailOperation(emailOperation);
                cancellationToken.ThrowIfCancellationRequested();
                logger.Error($"Couldn't send an email sign-in code to {userEmail}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.EMAIL_DELIVERY_FAILED);
                return;
            }

            if (!CompletePendingEmailSignInChallenge(userEmail, challenge, true))
            {
                ReleaseEmailOperation(emailOperation);
                cancellationToken.ThrowIfCancellationRequested();
                logger.Error($"Email sign-in challenge state was reset for {userEmail}");
                message.RespondError(
                    ResponseStatus.Error,
                    MstErrorCodes.EMAIL_SIGN_IN_STATE_FAILED);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            message.Respond(ResponseStatus.Success);
        }

        /// <summary>
        /// Completes email sign-in after a one-time code proves control of the address.
        /// </summary>
        protected virtual async Task ConfirmEmailSignIn(
            MstProperties userCredentials,
            IIncomingMessage message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userEmail = NormalizeEmail(userCredentials.AsString(MstParamKeys.USER_EMAIL));
            string code = userCredentials.AsString(MstParamKeys.USER_EMAIL_SIGN_IN_CODE)?.Trim();

            if (!IsEmailValid(userEmail) || string.IsNullOrEmpty(code) || code.Length > 64)
            {
                message.RespondError(
                    ResponseStatus.Invalid,
                    MstErrorCodes.EMAIL_SIGN_IN_REQUEST_INVALID);
                return;
            }

            if (message.Peer.GetExtension<IUserPeerExtension>() != null)
            {
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.PEER_ALREADY_AUTHENTICATED);
                return;
            }

            IAccountInfoData userAccount = null;
            bool accountCreated = false;
            bool emailBecameConfirmed = false;
            SemaphoreSlim[] accountLocks = await AcquireAccountIdentityLocksAsync(
                userEmail,
                userEmail,
                cancellationToken);

            try
            {
                userAccount = await databaseAccessor.GetAccountByEmailAsync(
                    userEmail,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                bool shouldCreateAccount = userAccount == null;

                IDisposable passwordHashOperation = null;

                if (shouldCreateAccount)
                {
                    passwordHashOperation = TryAcquirePasswordHashOperation();

                    if (passwordHashOperation == null)
                    {
                        message.RespondError(
                            ResponseStatus.ServiceUnavailable,
                            MstErrorCodes.AUTH_SERVICE_BUSY);
                        return;
                    }
                }

                using (passwordHashOperation)
                {
                    if (!TryConsumeEmailSignInChallenge(userEmail, code))
                    {
                        message.RespondError(
                            ResponseStatus.Invalid,
                            MstErrorCodes.EMAIL_SIGN_IN_CODE_INVALID_OR_EXPIRED);
                        return;
                    }

                    if (shouldCreateAccount)
                    {
                        userAccount = databaseAccessor.CreateAccountInstance();
                        userAccount.Username = userEmail;
                        userAccount.Email = userEmail;
                        userAccount.IsGuest = false;
                        userAccount.IsEmailConfirmed = true;
                        userAccount.Password = Mst.Security.CreateHash(MstSecurity.CreateRandomSecret());
                    }
                }

                if (shouldCreateAccount)
                {
                    try
                    {
                        await databaseAccessor.InsertAccountAsync(userAccount, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        accountCreated = true;
                        emailBecameConfirmed = true;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        userAccount = await databaseAccessor.GetAccountByEmailAsync(
                            userEmail,
                            cancellationToken);

                        if (userAccount == null)
                        {
                            logger.Error($"Couldn't create account for confirmed email {userEmail}: {exception.Message}");
                            message.RespondError(
                                ResponseStatus.Error,
                                MstErrorCodes.ACCOUNT_REGISTRATION_FAILED);
                            return;
                        }
                    }
                }

                if (IsUserLoggedInByEmail(userEmail))
                {
                    message.RespondError(
                        ResponseStatus.DuplicateLogin,
                        MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED);
                    return;
                }

                if (await TryRespondAccountBlocked(userAccount, message, cancellationToken))
                    return;

                if (!userAccount.IsEmailConfirmed)
                {
                    userAccount.IsEmailConfirmed = true;
                    await databaseAccessor.UpdateAccountAsync(userAccount, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    emailBecameConfirmed = true;
                }
            }
            finally
            {
                ReleaseAccountIdentityLocks(accountLocks);
            }

            if (accountCreated)
            {
                try
                {
                    OnUserRegisteredEvent?.Invoke(message.Peer, userAccount);
                }
                catch (Exception exception)
                {
                    logger.Error($"Email sign-in registration subscriber failed: {exception}");
                }
            }

            if (emailBecameConfirmed)
                NotifyEmailConfirmed(userAccount);

            await FinalizeSingIn(
                userAccount,
                message,
                userCredentials.AsBool(MstParamKeys.USER_REMEMBER_ME),
                cancellationToken);
        }

        private bool TryReserveEmailOperation(
            string operation,
            string email,
            int peerId,
            out EmailOperationReservation reservation)
        {
            reservation = null;
            long nowTicks = DateTime.UtcNow.Ticks;
            long cooldownTicks = TimeSpan.FromSeconds(emailOperationCooldownSeconds).Ticks;
            string emailKey = $"{operation}:email:{email}";
            string peerKey = $"{operation}:peer:{peerId}";

            lock (emailOperationSync)
            {
                if (activeEmailOperationCount >= MaximumActiveEmailOperations)
                    return false;

                if (emailOperationRequestTicks.Count >= 4096)
                {
                    long oldestAllowedTicks = nowTicks - cooldownTicks;
                    string[] expiredKeys = emailOperationRequestTicks
                        .Where(entry => entry.Value < oldestAllowedTicks)
                        .Select(entry => entry.Key)
                        .ToArray();

                    foreach (string key in expiredKeys)
                        emailOperationRequestTicks.Remove(key);
                }

                bool emailThrottled =
                    emailOperationRequestTicks.TryGetValue(emailKey, out long emailRequestTicks) &&
                    nowTicks - emailRequestTicks < cooldownTicks;
                bool peerThrottled =
                    emailOperationRequestTicks.TryGetValue(peerKey, out long peerRequestTicks) &&
                    nowTicks - peerRequestTicks < cooldownTicks;

                if (emailThrottled || peerThrottled)
                    return false;

                int newKeyCount =
                    (emailOperationRequestTicks.ContainsKey(emailKey) ? 0 : 1) +
                    (emailOperationRequestTicks.ContainsKey(peerKey) ? 0 : 1);

                if (emailOperationRequestTicks.Count + newKeyCount > 8192)
                    return false;

                emailOperationRequestTicks[emailKey] = nowTicks;
                emailOperationRequestTicks[peerKey] = nowTicks;
                activeEmailOperationCount++;
                reservation = new EmailOperationReservation(
                    this,
                    emailKey,
                    peerKey,
                    nowTicks);
                return true;
            }
        }

        private void ReleaseEmailOperation(EmailOperationReservation reservation)
        {
            lock (emailOperationSync)
            {
                RemoveEmailOperationReservation(reservation.EmailKey, reservation.RequestTicks);
                RemoveEmailOperationReservation(reservation.PeerKey, reservation.RequestTicks);
            }
        }

        private void CompleteEmailOperation(EmailOperationReservation reservation)
        {
            lock (emailOperationSync)
            {
                if (!reservation.TryMarkCompleted())
                    return;

                activeEmailOperationCount = Math.Max(0, activeEmailOperationCount - 1);
            }
        }

        private void RemoveEmailOperationReservation(string key, long requestTicks)
        {
            if (emailOperationRequestTicks.TryGetValue(key, out long currentTicks) &&
                currentTicks == requestTicks)
            {
                emailOperationRequestTicks.Remove(key);
            }
        }

        private bool TryConsumeEmailSignInChallenge(string email, string code)
        {
            if (!emailSignInChallenges.TryGetValue(email, out EmailSignInChallenge challenge))
                return false;

            EmailSignInChallengeResult result = challenge.TryConsume(
                email,
                code,
                emailSignInMaxAttempts,
                DateTime.UtcNow);

            if (result == EmailSignInChallengeResult.Invalid)
                return false;

            bool removed = RemoveEmailSignInChallenge(email, challenge);
            return result == EmailSignInChallengeResult.Success && removed;
        }

        private bool RemoveEmailSignInChallenge(string email, EmailSignInChallenge challenge)
        {
            bool removed =
                ((ICollection<KeyValuePair<string, EmailSignInChallenge>>)emailSignInChallenges).Remove(
                    new KeyValuePair<string, EmailSignInChallenge>(email, challenge));

            if (removed)
                challenge.Invalidate();

            return removed;
        }

        private bool TryReservePendingEmailSignInChallenge(string email)
        {
            lock (emailChallengeSync)
            {
                PruneExpiredEmailSignInChallenges();

                if (pendingEmailSignInChallenges.Contains(email))
                    return false;

                bool alreadyTracked = emailSignInChallenges.ContainsKey(email);

                if (!alreadyTracked &&
                    emailSignInChallenges.Count + pendingEmailSignInChallenges.Count >= 4096)
                {
                    return false;
                }

                pendingEmailSignInChallenges.Add(email);
                return true;
            }
        }

        private bool CompletePendingEmailSignInChallenge(
            string email,
            EmailSignInChallenge challenge,
            bool activate)
        {
            lock (emailChallengeSync)
            {
                if (!pendingEmailSignInChallenges.Remove(email))
                    return false;

                if (!activate)
                    return true;

                emailSignInChallenges.AddOrUpdate(
                    email,
                    challenge,
                    (_, previousChallenge) =>
                    {
                        previousChallenge.Invalidate();
                        return challenge;
                    });
                return true;
            }
        }

        private void PruneExpiredEmailSignInChallenges()
        {
            DateTime nowUtc = DateTime.UtcNow;

            foreach (KeyValuePair<string, EmailSignInChallenge> entry in emailSignInChallenges)
            {
                if (entry.Value.ExpiresAtUtc > nowUtc)
                    continue;

                ((ICollection<KeyValuePair<string, EmailSignInChallenge>>)emailSignInChallenges).Remove(entry);
            }
        }

        private static string CreateSecureRandomDigits(int length)
        {
            int safeLength = Mathf.Clamp(length, 1, 64);
            char[] result = new char[safeLength];
            byte[] randomBytes = new byte[Mathf.Max(16, safeLength * 2)];
            int written = 0;

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                while (written < result.Length)
                {
                    random.GetBytes(randomBytes);

                    foreach (byte value in randomBytes)
                    {
                        if (value >= 250)
                            continue;

                        result[written++] = (char)('0' + value % 10);

                        if (written == result.Length)
                            break;
                    }
                }
            }

            return new string(result);
        }

        private async Task<SemaphoreSlim[]> AcquireAccountIdentityLocksAsync(
            string firstIdentity,
            string secondIdentity,
            CancellationToken cancellationToken)
        {
            int firstIndex = GetAccountIdentityLockIndex(firstIdentity);
            int secondIndex = GetAccountIdentityLockIndex(secondIdentity);

            if (firstIndex == secondIndex)
            {
                SemaphoreSlim singleLock = accountIdentityLocks[firstIndex];
                await singleLock.WaitAsync(cancellationToken);
                return new[] { singleLock };
            }

            int lowerIndex = Math.Min(firstIndex, secondIndex);
            int upperIndex = Math.Max(firstIndex, secondIndex);
            SemaphoreSlim lowerLock = accountIdentityLocks[lowerIndex];
            SemaphoreSlim upperLock = accountIdentityLocks[upperIndex];

            await lowerLock.WaitAsync(cancellationToken);

            try
            {
                await upperLock.WaitAsync(cancellationToken);
            }
            catch
            {
                lowerLock.Release();
                throw;
            }

            return new[] { lowerLock, upperLock };
        }

        private int GetAccountIdentityLockIndex(string identity)
        {
            uint hash = unchecked(
                (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(identity ?? string.Empty));
            return (int)(hash % (uint)accountIdentityLocks.Length);
        }

        private static void ReleaseAccountIdentityLocks(SemaphoreSlim[] accountLocks)
        {
            for (int i = accountLocks.Length - 1; i >= 0; i--)
                accountLocks[i].Release();
        }

        private async Task<SemaphoreSlim[]> AcquireSessionTransitionLocksAsync(
            string firstAccountId,
            string secondAccountId,
            CancellationToken cancellationToken)
        {
            int firstIndex = GetSessionTransitionLockIndex(firstAccountId);

            if (string.IsNullOrEmpty(secondAccountId))
            {
                SemaphoreSlim singleLock = sessionTransitionLocks[firstIndex];
                await singleLock.WaitAsync(cancellationToken);
                return new[] { singleLock };
            }

            int secondIndex = GetSessionTransitionLockIndex(secondAccountId);

            if (firstIndex == secondIndex)
            {
                SemaphoreSlim singleLock = sessionTransitionLocks[firstIndex];
                await singleLock.WaitAsync(cancellationToken);
                return new[] { singleLock };
            }

            int lowerIndex = Math.Min(firstIndex, secondIndex);
            int upperIndex = Math.Max(firstIndex, secondIndex);
            SemaphoreSlim lowerLock = sessionTransitionLocks[lowerIndex];
            SemaphoreSlim upperLock = sessionTransitionLocks[upperIndex];

            await lowerLock.WaitAsync(cancellationToken);

            try
            {
                await upperLock.WaitAsync(cancellationToken);
            }
            catch
            {
                lowerLock.Release();
                throw;
            }

            return new[] { lowerLock, upperLock };
        }

        private int GetSessionTransitionLockIndex(string accountId)
        {
            uint hash = unchecked(
                (uint)StringComparer.Ordinal.GetHashCode(accountId ?? string.Empty));
            return (int)(hash % (uint)sessionTransitionLocks.Length);
        }

        private static void ReleaseSessionTransitionLocks(SemaphoreSlim[] sessionLocks)
        {
            for (int i = sessionLocks.Length - 1; i >= 0; i--)
                sessionLocks[i].Release();
        }

        private object GetPeerSignInLock(int peerId)
        {
            return peerSignInLocks[GetPeerSignInLockIndex(peerId)];
        }

        private int GetPeerSignInLockIndex(int peerId)
        {
            return (int)(unchecked((uint)peerId) % (uint)peerSignInLocks.Length);
        }

        private void ExecuteWithPeerSignInLocks(IPeer firstPeer, IPeer secondPeer, Action action)
        {
            if (firstPeer == null)
                throw new ArgumentNullException(nameof(firstPeer));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            int firstIndex = GetPeerSignInLockIndex(firstPeer.Id);

            if (secondPeer == null)
            {
                lock (peerSignInLocks[firstIndex])
                    action();

                return;
            }

            int secondIndex = GetPeerSignInLockIndex(secondPeer.Id);

            if (firstIndex == secondIndex)
            {
                lock (peerSignInLocks[firstIndex])
                    action();

                return;
            }

            object lowerLock = peerSignInLocks[Math.Min(firstIndex, secondIndex)];
            object upperLock = peerSignInLocks[Math.Max(firstIndex, secondIndex)];

            lock (lowerLock)
            {
                lock (upperLock)
                    action();
            }
        }

        private static SemaphoreSlim[] CreateSemaphorePool(int size)
        {
            var pool = new SemaphoreSlim[size];

            for (int i = 0; i < pool.Length; i++)
                pool[i] = new SemaphoreSlim(1, 1);

            return pool;
        }

        private static object[] CreateSyncPool(int size)
        {
            var pool = new object[size];

            for (int i = 0; i < pool.Length; i++)
                pool[i] = new object();

            return pool;
        }

        /// <summary>
        /// Acquires one of the bounded slots used by CPU-intensive password hashing.
        /// </summary>
        /// <returns>A disposable slot, or <c>null</c> when the service is at capacity.</returns>
        protected virtual IDisposable TryAcquirePasswordHashOperation()
        {
            return passwordHashOperations.Wait(0)
                ? new SemaphoreOperation(passwordHashOperations)
                : null;
        }

        protected virtual async Task<IDisposable> AcquirePasswordHashOperationAsync(
            CancellationToken cancellationToken)
        {
            await passwordHashOperations.WaitAsync(cancellationToken);
            return new SemaphoreOperation(passwordHashOperations);
        }

        protected virtual IDisposable TryAcquirePeerAuthOperation(int peerId)
        {
            return activePeerAuthOperations.TryAdd(peerId, 0)
                ? new PeerAuthOperation(activePeerAuthOperations, peerId)
                : null;
        }

        private sealed class SemaphoreOperation : IDisposable
        {
            private SemaphoreSlim semaphore;

            public SemaphoreOperation(SemaphoreSlim semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref semaphore, null)?.Release();
            }
        }

        private sealed class PeerAuthOperation : IDisposable
        {
            private ConcurrentDictionary<int, byte> operations;
            private readonly int peerId;

            public PeerAuthOperation(
                ConcurrentDictionary<int, byte> operations,
                int peerId)
            {
                this.operations = operations;
                this.peerId = peerId;
            }

            public void Dispose()
            {
                ConcurrentDictionary<int, byte> activeOperations =
                    Interlocked.Exchange(ref operations, null);

                activeOperations?.TryRemove(peerId, out _);
            }
        }

        private sealed class EmailOperationReservation : IDisposable
        {
            private readonly AuthModule owner;
            private int isCompleted;

            public EmailOperationReservation(
                AuthModule owner,
                string emailKey,
                string peerKey,
                long requestTicks)
            {
                this.owner = owner;
                EmailKey = emailKey;
                PeerKey = peerKey;
                RequestTicks = requestTicks;
            }

            public string EmailKey { get; }
            public string PeerKey { get; }
            public long RequestTicks { get; }

            public void Dispose()
            {
                owner.CompleteEmailOperation(this);
            }

            public bool TryMarkCompleted()
            {
                return Interlocked.Exchange(ref isCompleted, 1) == 0;
            }
        }

        private enum EmailSignInChallengeResult
        {
            Success,
            Invalid,
            Expired,
            AttemptsExceeded
        }

        private sealed class EmailSignInChallenge
        {
            private readonly object sync = new();
            private readonly byte[] salt;
            private readonly byte[] codeHash;
            private int failedAttempts;
            private bool consumed;

            public EmailSignInChallenge(string email, string code, DateTime expiresAtUtc)
            {
                ExpiresAtUtc = expiresAtUtc;
                salt = new byte[16];

                using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                    random.GetBytes(salt);

                codeHash = ComputeHash(salt, email, code);
            }

            public DateTime ExpiresAtUtc { get; }

            public void Invalidate()
            {
                lock (sync)
                    consumed = true;
            }

            public EmailSignInChallengeResult TryConsume(
                string email,
                string code,
                int maxAttempts,
                DateTime nowUtc)
            {
                lock (sync)
                {
                    if (consumed || nowUtc >= ExpiresAtUtc)
                    {
                        consumed = true;
                        return EmailSignInChallengeResult.Expired;
                    }

                    byte[] candidateHash = ComputeHash(salt, email, code);

                    if (MstSecurity.FixedTimeEquals(codeHash, candidateHash))
                    {
                        consumed = true;
                        return EmailSignInChallengeResult.Success;
                    }

                    failedAttempts++;

                    if (failedAttempts >= maxAttempts)
                    {
                        consumed = true;
                        return EmailSignInChallengeResult.AttemptsExceeded;
                    }

                    return EmailSignInChallengeResult.Invalid;
                }
            }

            private static byte[] ComputeHash(byte[] salt, string email, string code)
            {
                byte[] value = Encoding.UTF8.GetBytes($"{email}\n{code}");
                byte[] input = new byte[salt.Length + value.Length];
                Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
                Buffer.BlockCopy(value, 0, input, salt.Length, value.Length);

                using (SHA256 sha256 = SHA256.Create())
                    return sha256.ComputeHash(input);
            }
        }

        /// <summary>
        /// Signs in user with his login and password
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        protected virtual async Task SignInWithLoginAndPassword(MstProperties userCredentials, IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Trying to get user extension from peer
            var userPeerExtension = message.Peer.GetExtension<IUserPeerExtension>();

            bool replacesGuestSession = userPeerExtension?.Account?.IsGuest == true;

            // Only a guest session may be replaced by validated username/password credentials.
            if (userPeerExtension != null && !replacesGuestSession)
            {
                logger.Warn($"User {message.Peer.Id} has already logged into his account");
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.PEER_ALREADY_AUTHENTICATED);
                return;
            }

            var userName = userCredentials.AsString(MstParamKeys.USER_NAME);
            var userPassword = userCredentials.AsString(MstParamKeys.USER_PASSWORD);
            bool userRememberMe = userCredentials.AsBool(MstParamKeys.USER_REMEMBER_ME);

            if (string.IsNullOrEmpty(userName) ||
                userName.Length > usernameMaxChars ||
                string.IsNullOrEmpty(userPassword) ||
                userPassword.Length > userPasswordMaxChars)
            {
                logger.Warn($"Invalid credential length for client {message.Peer.Id}");
                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_CREDENTIALS);
                return;
            }

            // If another session found
            if (IsUserLoggedInByUsername(userName))
            {
                logger.Error($"Another user with username {userName} is already logged into his account");
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED);
                return;
            }

            // Get account by its username
            IAccountInfoData account = await databaseAccessor.GetAccountByUsernameAsync(userName, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (account == null)
            {
                logger.Error($"No account with username {userName} found for client {message.Peer.Id}");
                message.RespondError(ResponseStatus.NotFound, MstErrorCodes.INVALID_CREDENTIALS);
            }
            else
            {
                bool passwordValid;
                bool passwordNeedsRehash;

                using (IDisposable passwordHashOperation = TryAcquirePasswordHashOperation())
                {
                    if (passwordHashOperation == null)
                    {
                        message.RespondError(
                            ResponseStatus.ServiceUnavailable,
                            MstErrorCodes.AUTH_SERVICE_BUSY);
                        return;
                    }

                    passwordValid = Mst.Security.ValidatePassword(userPassword, account.Password);
                    passwordNeedsRehash = passwordValid &&
                                          Mst.Security.NeedsPasswordRehash(account.Password);
                }

                if (!passwordValid)
                {
                    logger.Error($"Invalid credentials for client {message.Peer.Id}");
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.INVALID_CREDENTIALS);
                    return;
                }

                if (await TryRespondAccountBlocked(account, message, cancellationToken))
                    return;

                if (passwordNeedsRehash)
                {
                    IDisposable passwordHashOperation = TryAcquirePasswordHashOperation();

                    if (passwordHashOperation != null)
                    {
                        string previousPasswordHash = account.Password;
                        string upgradedPasswordHash;

                        using (passwordHashOperation)
                            upgradedPasswordHash = Mst.Security.CreateHash(userPassword);

                        try
                        {
                            account.Password = upgradedPasswordHash;
                            await databaseAccessor.UpdateAccountAsync(account, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        catch (OperationCanceledException)
                        {
                            account.Password = previousPasswordHash;
                            throw;
                        }
                        catch (Exception exception)
                        {
                            account.Password = previousPasswordHash;
                            logger.Warn(
                                $"Password hash upgrade failed for account {account.Id}: {exception.Message}");
                        }
                    }
                }

                if (replacesGuestSession)
                {
                    await FinalizeSingInWithSessionReplacement(
                        account,
                        message,
                        cancellationToken,
                        createToken: userRememberMe,
                        allowTargetSessionTakeover: false);
                }
                else
                {
                    await FinalizeSingIn(account, message, userRememberMe, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Signs in user with auth token
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        protected virtual async Task SignInWithToken(MstProperties userCredentials, IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Trying to get user extension from peer
            var userPeerExtension = message.Peer.GetExtension<IUserPeerExtension>();

            // If user peer has IUserPeerExtension means this user is already logged in
            if (userPeerExtension != null)
            {
                logger.Warn($"User {message.Peer.Id} has already logged into an account");
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.PEER_ALREADY_AUTHENTICATED);
                return;
            }

            // Reject malformed, forged, or expired tokens before touching account data.
            if (!ValidateToken(
                    userCredentials,
                    out string tokenAccountId,
                    out string tokenUsername,
                    out int tokenRevision))
            {
                logger.Warn("Session token is invalid or expired");
                message.RespondError(
                    ResponseStatus.TokenExpired,
                    MstErrorCodes.AUTH_TOKEN_INVALID_OR_EXPIRED);
                return;
            }

            // The signature protects the account identity. Resolve that identity directly
            // so a previously issued token remains usable until its own expiration time.
            IAccountInfoData account = await databaseAccessor.GetAccountByIdAsync(
                tokenAccountId,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // if no account found
            if (account == null)
            {
                logger.Warn("Session token account does not exist");
                message.RespondError(
                    ResponseStatus.Invalid,
                    MstErrorCodes.AUTH_TOKEN_INVALID_OR_EXPIRED);
            }
            else if (!string.Equals(account.Id, tokenAccountId, StringComparison.Ordinal) ||
                     !string.Equals(account.Username, tokenUsername, StringComparison.Ordinal))
            {
                logger.Warn("Session token account identity does not match stored account data");
                message.RespondError(
                    ResponseStatus.Invalid,
                    MstErrorCodes.AUTH_TOKEN_INVALID_OR_EXPIRED);
            }
            else if (tokenRevision != await databaseAccessor.GetAuthTokenRevisionAsync(
                         account.Id,
                         cancellationToken))
            {
                logger.Warn("Session token was revoked by an account security change");
                message.RespondError(
                    ResponseStatus.TokenExpired,
                    MstErrorCodes.AUTH_TOKEN_INVALID_OR_EXPIRED);
            }
            // If another session found
            else if (IsUserLoggedInByUsername(account.Username))
            {
                logger.Warn($"Another user with {account.Username} is already logged in");
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.ACCOUNT_ALREADY_AUTHENTICATED);
            }
            else if (await TryRespondAccountBlocked(account, message, cancellationToken))
            {
                return;
            }
            // Finalize login
            else
            {
                await FinalizeSingIn(account, message, true, cancellationToken);
            }
        }

        /// <summary>
        /// Signs in user as guest using his guest parameters such as device id
        /// </summary>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        protected virtual async Task SignInAsGuest(MstProperties userCredentials, IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Check if guest login is allowed
            if (!allowGuestLogin)
            {
                logger.Error("Guest login is not allowed in this game");
                message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.GUEST_LOGIN_DISABLED);
                return;
            }

            // Trying to get user extension from peer
            var userPeerExtension = message.Peer.GetExtension<IUserPeerExtension>();

            // If user peer has IUserPeerExtension means this user is already logged in
            if (userPeerExtension != null)
            {
                logger.Error($"User {message.Peer.Id} has already logged into his account");
                message.RespondError(
                    ResponseStatus.DuplicateLogin,
                    MstErrorCodes.PEER_ALREADY_AUTHENTICATED);
                return;
            }

            // Create new guest account
            IAccountInfoData userAccount = databaseAccessor.CreateAccountInstance();
            userAccount.Username = GenerateUsername();

            // Save account and return its id in DB
            await databaseAccessor.InsertAccountAsync(userAccount, cancellationToken);
            await FinalizeSingIn(userAccount, message, true, cancellationToken);
        }

        #endregion
    }
}
