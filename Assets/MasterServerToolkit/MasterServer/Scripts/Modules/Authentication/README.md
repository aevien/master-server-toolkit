# Authentication Module

`AuthModule` is the authoritative account module for MST. It handles account creation, guest
accounts, username/password login, token login, email login, password reset, email confirmation,
client-writable account metadata and account lookup for room servers.

## Key Files

- `AuthModule.cs` - server module and authentication authority.
- `AuthClient.cs` - client facade for sign in/up/out, token login, guest login, password/email flows
  and account metadata updates.
- `AuthServer.cs` - room/server-side facade for account lookup by peer or username.
- `UserPeerExtension.cs` and `IUserPeerExtension.cs` - authenticated identity attached to `IPeer`.
- `IAccountsDatabaseAccessor.cs` - persistence contract implemented by database bridges.
- `Packets` - account info packets sent over the wire.

## Flow

Clients use `Mst.Security.EncryptForMaster` to seal credentials for one exact `SignIn` or `SignUp`
opcode and purpose. The master issues a short-lived one-time challenge; the client encrypts with a
fresh AES-256-GCM key and wraps that key with the master's RSA-OAEP-SHA256 public key. The server
consumes the challenge once, authenticates the envelope, validates credential format, checks
duplicates and active account blocks, uses the configured account database accessor, creates or
updates auth tokens and attaches an `IUserPeerExtension` after successful login.

Email sign-in is a two-step flow. `AuthClient.SignInWithEmail(...)` requests a short-lived one-time
code and never changes an existing account password. `AuthClient.ConfirmEmailSignIn(...)` proves
control of the address and only then signs in the existing account or creates a confirmed email
account. Challenges are kept in master memory, are single-use, expire, and have bounded attempts;
restarting the master invalidates outstanding codes. Operations for the same normalized address are
serialized inside one master process so sign-up and email confirmation cannot create competing
accounts. A replacement code becomes active only after the mail is sent successfully, so a mail
failure does not invalidate the previously delivered code.

Password-reset and post-registration email-confirmation codes use a separate persisted lifecycle.
Each newly issued code replaces the previous record for the normalized email, remains valid for the
configured lifetime and starts with the configured attempt budget. Validation consumes a successful
code atomically. An expired code or the last failed attempt also removes the record. The default
contract is 10 minutes and 5 attempts, so a master restart does not invalidate a delivered code.

Client operations that establish an account session use `AuthClient.AccountCallback`. Its arguments
are the exact `ResponseStatus`, the authenticated `AccountInfoPacket` on success, and an error message
for presentation or diagnostics. Callers must use the status as the authoritative result and must not
infer the failure category from the error text. Local failures use explicit statuses such as
`NotConnected`, `Conflict`, `TokenExpired` or `Error`. Requesting an email sign-in code does not create
a session and therefore uses `SuccessCallback`; completing that flow uses `AccountCallback`.

New password hashes use the versioned `v2` PBKDF2-HMAC-SHA256 format. Existing PBKDF2-SHA1 hashes
remain valid and are replaced with the current format after a successful password login. No database
migration is required. CPU-intensive password verification and creation are limited to two concurrent
operations per `AuthModule`; requests above that capacity receive `ServiceUnavailable` instead of
building an unbounded work queue.

Standalone builds must configure a non-default `-mstTokenSecret` containing at least 32 UTF-8 bytes,
a positive token lifetime, issuer and audience. Changing the token secret invalidates tokens issued
with the legacy format but does not change accounts or password hashes. `AuthModule` validates this
configuration through the server startup preflight, before the listening socket is opened. The
persistent `-mstSecurityKeyRingFile` protects version `v2` tokens and must be retained across master
updates.

Successful token sign-in always issues a replacement `v2` token. Any previously issued `v2` token
with a valid authentication tag and unexpired payload can still resolve the same account by its
protected `accountId` and `username`, even when the database already stores a newer token. Valid
legacy CBC+HMAC tokens follow the same path and are automatically replaced with `v2`; no database
migration script is required. This prevents a lost response from making an account unrecoverable.
The database `Token` field therefore stores the latest token but is not an immediate revocation list;
earlier tokens normally remain valid until their own expiration. A separate server-owned token
revision is embedded into every new token. Normal token rotation keeps the revision unchanged so a
lost replacement response remains recoverable. A successful password change atomically advances the
stored revision before returning success, clears the latest token field and makes every previously
issued token fail with `TokenExpired`. The client also removes its saved token after that response.
Changing the account username, deleting the account, removing the corresponding key-ring key, or
changing `-mstTokenSecret` while legacy tokens remain can also invalidate tokens.
Signing out clears the current client token and peer session but does not revoke another copy of an
already issued token before its expiration.

The client replaces an exact saved token with the replacement returned by the server even when
`RememberMe` is currently disabled. An explicitly supplied token does not overwrite a different saved
token unless `RememberMe` is enabled or the account is a guest. Automatic rejection handling clears a
saved token only after an explicit `TokenExpired`, `Invalid` or `Unauthorized` response; temporary
transport and service failures keep it. Applications and Editor test services may explicitly call
`AuthClient.ClearAuthToken()` when they intentionally need to suppress automatic token sign-in. This
method removes only the saved token and does not sign out the active account.

Guest login creates a persisted guest account and uses the module guest prefix plus
`Mst.Helper.CreateFriendlyId()`.

Successful registration is also a successful authenticated sign-in. The server returns an
`AccountInfoPacket` with a fresh token instead of requiring a second username/password request.
When the peer already owns a guest session, registration updates that same persisted account and
keeps its account ID, profile, peer extension and room-independent state. The client replaces the
saved guest token with the returned regular-account token. A peer signed in to a regular account
cannot call registration again.

`ResponseStatus.Banned` is a terminal authentication result for the current attempt. The client
forwards the server block message through `AccountCallback` and preserves the saved token. Application
code may present a dedicated block view, but it must not silently replace the blocked account with a
guest session. Automatic guest fallback after token sign-in is appropriate only for
`TokenExpired`, `Invalid` and `Unauthorized`.

A validated username/password sign-in may replace the same peer's current guest session. The
replacement uses the existing session-transition path, issues a remember-me token when requested,
publishes the normal logout/login lifecycle, and updates the peer account atomically. A peer already
signed in to a non-guest account still receives `DuplicateLogin`; callers cannot use this path to
silently replace an ordinary authenticated session. Password sign-in also never displaces another
peer that is already using the requested account; that race returns `DuplicateLogin`.

Validated external identity integrations may use `FinalizeSingInWithSessionReplacement` to transfer
an account to a new peer. This is a privileged finalization path, not a second public login policy:
the caller must complete platform signature/token validation before invoking it. The default
username/password, guest and token handlers continue to reject an account already owned by another
peer. Application integrations that permit takeover must release any authoritative room session
before finalization; the base module rejects a target session whose `JoinedRoomID` is still active.

Session preparation updates `LastLoginAt` and creates the requested token before publishing either
`loggedInUsers` or the peer extension. Publication, replacement and sign-out pair the exact account
mapping with the exact `IUserPeerExtension` under stable peer locks. A delayed disconnect or `SignOut`
from an older peer therefore cannot clear the replacement session, and a peer that disconnects while
preparation is pending cannot remain registered as an authenticated owner.

## Inspector Configuration

- Username and password limits apply to normal accounts; guest names are generated separately from
  `Guest Prefix`.
- `Mailer` is required by confirmation, password-reset and email sign-in flows unless those flows are
  replaced by custom code.
- `Email Address Validation Template` is a full regular expression. An invalid expression causes email
  validation to fail.
- Email sign-in lifetime is measured in minutes; cooldown is measured in seconds; maximum attempts
  consumes and invalidates the challenge when reached.
- Persisted verification-code lifetime is measured in minutes. Maximum attempts is shared by
  password-reset and post-registration email-confirmation codes; both values must be at least 1.
- `Token Secret` must contain at least 32 UTF-8 bytes in standalone production builds. Token lifetime
  is measured in days and must be positive.
- `Token Issuer` and `Token Audience` are validated claims. Changing either rejects tokens issued with
  the previous values.
- `Database Accessor Factory` is mandatory for persisted accounts, blocks, token resolution and account
  updates.

## Extension Points

- Override validation methods for username/email/password rules.
- Override `RunAuthFactory` or specific `SignInWith...` methods for new auth types.
- Override `CreateUserPeerExtension` for custom peer identity data.
- Use database accessors for persistence; do not add provider-specific code here.
- Store account blocks through `IAccountBlockData` and `IAccountsDatabaseAccessor`; do not put
  block state into `IAccountInfoData` or client-writable `ExtraProperties`.

## Risks

- `loggedInUsers` is the in-memory session authority. Keep it in sync on sign-out and disconnect.
- Only a validated server-side identity integration may opt into target-session takeover. Never call
  the replacement finalizer directly from untrusted client data or from ordinary password/token login.
- Removing a session must match the exact `IUserPeerExtension` stored for that user ID. A delayed
  disconnect from an older peer must not remove or publish logout for the replacement session.
- Account `ExtraProperties` are client-writable metadata. Do not treat them as authoritative
  economy, permission or entitlement data.
- Account block records are server-owned security state and must be checked before successful
  sign-in finalization.
- Email sign-in request responses must not reveal whether an address exists, is blocked, or currently
  has an active session. Those account-specific checks happen only after code confirmation.
- A single `SmtpClient` does not support parallel sends. `SmtpMailer` serializes sends while preserving
  async handler execution. The configured timeout includes queue wait and send time. Timeout and
  lifecycle cancellation have a bounded grace period; a client that remains stuck is retired so it
  cannot block later sends.
- `System.Net.Mail.SmtpClient` supports STARTTLS, not implicit SMTPS. For Yandex Mail configure port
  `587` with SSL enabled. Port `465` cannot be used by this implementation without replacing the SMTP
  client library.
- Auth handlers are async. Avoid Unity main-thread APIs inside them.
