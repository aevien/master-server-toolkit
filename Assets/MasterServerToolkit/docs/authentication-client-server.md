# Master Server Toolkit 5: Authentication Module

This guide explains how to set up and use `AuthModule` in Master Server Toolkit 5,
and how client-server authentication flows work for sign up, sign in, guest accounts,
tokens, email confirmation, password reset, and account lookup from game/room servers.

## What AuthModule Does

`AuthModule` is responsible for:

- account registration;
- username/password sign in;
- guest sign in;
- saved-token sign in;
- email confirmation;
- password reset through an email code;
- client-writable account metadata through `ExtraProperties`;
- returning `AccountInfoPacket` to the client after successful authentication;
- attaching authenticated account state to the master server `IPeer` through `IUserPeerExtension`;
- exposing short account information to game/room servers through `AuthServer`.

The authentication module does not store gameplay profiles. Progress, inventory, currency,
characters, rewards, unlocks, and other authoritative gameplay data should live in `ProfilesModule`
or in your own server-owned game database.

`AccountInfoPacket.ExtraProperties` is client-writable account metadata. Do not use it for
economy state, permissions, purchases, anti-cheat, or any other data the client must not control.

## Main Types

- `AuthModule` - authoritative master server module.
- `AuthClient` - client-side facade available through `Mst.Client.Auth`.
- `AuthServer` - server-side facade available through `Mst.Server.Auth`.
- `AccountInfoPacket` - full account data returned to the authenticated client.
- `RoomUserAccountInfoPacket` - short account data returned to a game/room server.
- `IAccountsDatabaseAccessor` - account persistence interface.
- `IAccountInfoData` - server-side account model.
- `IUserPeerExtension` - authenticated user state attached to a master server peer.

## Server Setup

Your master server scene must include the normal MST server bootstrap and an `AuthModule`
component. `AuthModule` also needs an implementation of `IAccountsDatabaseAccessor`.

This is usually provided by one of the database bridges:

- `Bridges/LiteDB` - embedded local database, useful for development.
- `Bridges/MongoDB` - MongoDB provider.
- `Bridges/SqlSugar` - SQL provider, for example MySQL.

You can assign a `DatabaseAccessorFactory` to `AuthModule` in the Inspector. If the factory is
assigned, `AuthModule` calls `CreateAccessors()` during initialization. After that it reads the
account accessor from the global MST accessor registry:

```csharp
Mst.Server.DbAccessors.GetAccessor<IAccountsDatabaseAccessor>();
```

If no account accessor is registered, authentication cannot work and `AuthModule` logs a fatal
error.

## AuthModule Settings

Important Inspector settings:

- `Username Min Chars` - minimum username length for normal accounts.
- `Username Max Chars` - maximum username length for normal accounts.
- `User Password Min Chars` - minimum password length.
- `User Password Max Chars` - maximum accepted password length. The default is `1024`.
- `Email Confirm Required` - whether new accounts must confirm email after sign up.
- `Allow Guest Login` - whether guest login is allowed.
- `Guest Prefix` - generated guest username prefix, for example `player-`.
- `Email Sign-In Code Lifetime Minutes` - lifetime of a one-time email sign-in code.
- `Email Sign-In Max Attempts` - invalid attempts allowed before a code is consumed.
- `Email Operation Cooldown Seconds` - per-peer and per-address cooldown for email operations.
- `Get Peer Data Permission Level` - minimum permission level required to query peer account data.
- `Mailer` - SMTP mailer used for email confirmation and password reset.
- `Token Secret`, `Token Expires In Days`, `Token Issuer`, `Token Audience` - token settings.

Token settings can also be overridden from config or command line:

```ini
-mstTokenSecret=change-this-token-secret
-mstTokenExpiresInDays=7
-mstTokenIssuer=my-game
-mstTokenAudience=my-game-client
```

Outside the Unity Editor, `AuthModule` validates these settings before the server opens its listening
socket. The token secret must not use an MST placeholder and must contain at least 32 UTF-8 bytes.
Token lifetime must be positive, and issuer and audience must be present. Invalid production settings
prevent server startup instead of creating insecure tokens.

SQL provider and SMTP setup usually need parameters like this:

```ini
-mstDatabaseProvider=MySql
-mstDatabaseConnectionString=Server=127.0.0.1;Database=mst_game;Uid=root;Pwd=password;Port=3306;

-mstSmtpHost=smtp.example.com
-mstSmtpUsername=no-reply@example.com
-mstSmtpPassword=password
-mstSmtpPort=587
-mstSmtpEnableSSL=true
-mstSmtpTimeout=10
-mstSmtpMailFrom=no-reply@example.com
-mstSmtpSenderDisplayName=My Game
```

SMTP sends are serialized because one `SmtpClient` cannot process multiple sends concurrently.
`-mstSmtpTimeout` bounds the complete operation, including time spent waiting for that serialization
lock. An operation that exceeds the deadline is cancelled with a bounded grace period. If the
underlying SMTP operation does not complete, MST retires that client so later sends are not blocked.
Standard platform certificate validation remains enabled.

## Client Setup

The client scene needs `ClientToMasterConnector` or another component that creates an
`IClientSocket` and connects to the master server. After connection, MST client facades are
available through `Mst.Client`.

Minimal connection parameters:

```ini
-mstMasterIp=127.0.0.1
-mstMasterPort=25200
-mstStartClientConnection=true
-mstUseSecure=false
```

Authentication calls must run after the client is connected to the master server. A common pattern
is to start authentication from `ClientToMasterConnector.OnConnectedEvent`.

## Sign Up

Sign up uses `MstProperties` with keys from `MstParamKeys`.

```csharp
using MasterServerToolkit.MasterServer;

public void SignUp(string username, string email, string password)
{
    var credentials = new MstProperties();
    credentials.Set(MstParamKeys.USER_NAME, username);
    credentials.Set(MstParamKeys.USER_EMAIL, email);
    credentials.Set(MstParamKeys.USER_PASSWORD, password);

    Mst.Client.Auth.SignUp(credentials, (account, error) =>
    {
        if (account == null)
        {
            ShowError(error);
            return;
        }

        ShowAuthenticatedAccount(account);
    });
}
```

A successful registration also authenticates the connection and returns its `AccountInfoPacket`.
Do not send a second username/password sign-in request. If the connection already belongs to a guest,
the server upgrades that persisted account in place: its account ID and profile ownership stay the
same, while the client receives and stores the replacement regular-account token. Registration from
an already authenticated regular account is rejected.

On the server, `AuthModule` validates that:

- the peer is not already signed in as a normal account;
- the sealed credential envelope is authenticated and belongs to a live one-time challenge;
- username length and format are valid;
- optional `CensorModule` check passes;
- password length is valid;
- email format is valid;
- username and email are unique.

If `Email Confirm Required` is enabled, the account is created with `IsEmailConfirmed = false`.

## Username And Password Sign In

```csharp
using MasterServerToolkit.MasterServer;

public void SignIn(string username, string password, bool rememberMe)
{
    Mst.Client.Auth.RememberMe = rememberMe;

    Mst.Client.Auth.SignInWithLoginAndPassword(username, password, (account, error) =>
    {
        if (account == null)
        {
            ShowError(error);
            return;
        }

        string userId = account.Id;
        string displayName = account.Username;
        bool isGuest = account.IsGuest;
        bool isEmailConfirmed = account.IsEmailConfirmed;

        ShowSignedInState(userId, displayName, isGuest, isEmailConfirmed);
    });
}
```

If `RememberMe = true`, the server creates a new token and the client stores it in `PlayerPrefs`
under `MstParamKeys.USER_AUTH_TOKEN`.

New password hashes use versioned PBKDF2-HMAC-SHA256 records. Existing MST PBKDF2-SHA1 records remain
readable and are upgraded after a successful password sign in. This is a lazy per-account upgrade; no
bulk migration is required.

After successful sign in:

- `Mst.Client.Auth.Account` contains `AccountInfoPacket`;
- `Mst.Client.Auth.IsSignedIn == true`;
- `Mst.Client.Auth.OnSignedInEvent` is invoked;
- the master server peer receives `IUserPeerExtension`.

## Guest Sign In

```csharp
using MasterServerToolkit.MasterServer;

public void SignInAsGuest()
{
    Mst.Client.Auth.SignInAsGuest((account, error) =>
    {
        if (account == null)
        {
            ShowError(error);
            return;
        }

        ShowGuestSignedIn(account.Username);
    });
}
```

A guest account is created on the server, gets a username from `AuthModule.GenerateUsername()`,
and is saved through the account database accessor. The generated username uses `Guest Prefix`
and a friendly id.

If the server returns a token, the guest token is saved automatically. The next
`SignInAsGuest()` call first tries saved-token sign in.

## Saved Token Sign In

```csharp
using MasterServerToolkit.MasterServer;

public void TryTokenSignIn()
{
    Mst.Client.Auth.SignInWithToken((account, error) =>
    {
        if (account == null)
        {
            ShowLoginForm(error);
            return;
        }

        ShowSignedInState(account.Id, account.Username, account.IsGuest, account.IsEmailConfirmed);
    });
}
```

The server validates that:

- token format and expiration are valid;
- account exists in the database;
- the account is not already signed in from another peer session;
- no active server-owned account block exists for the account.

Successful token sign in issues a replacement token. A previously issued token remains accepted until
its signed expiration time when its `accountId` and `username` still match the stored account, even if
the database already contains a newer token. This makes token rotation recoverable when the response
carrying the replacement token is lost.

The account `Token` database field stores the latest issued token but is not an immediate revocation
list. Older correctly signed tokens remain valid until their own expiration. Changing the username,
deleting the account, or changing the server token secret invalidates them. Applications that require
per-token immediate revocation need an additional server-owned revocation/version mechanism.
Signing out clears the current client token and peer session but does not revoke another copy of an
already issued token before its expiration. Changing the account password also does not revoke
previously issued tokens.

When sign in started with the exact token currently stored in `PlayerPrefs`, `AuthClient` replaces it
with the returned token even if `RememberMe` is currently disabled. An explicitly supplied token does
not overwrite a different saved token unless `RememberMe` is enabled or the account is a guest.

Automatic rejection handling removes a saved token only when the server reports that the token is
expired, invalid, or unauthorized. Transport failures and temporary service errors preserve it for a
later retry. An application or Editor test service may explicitly call `AuthClient.ClearAuthToken()`
before authentication when it intentionally needs to skip remembered-token sign-in. This removes only
the saved token; it does not sign out an account that is already active.

## Validated Identity Transitions

Custom authentication modules that validate an external platform identity may use
`FinalizeSingInWithSessionReplacement()` after validation and persistence are complete. Password
authentication uses the same transition only when replacing the current peer's guest session and
disables target-session takeover. Regular email, guest, token, and repeated authenticated password
sign-in keep using `FinalizeSingIn()` so duplicate sessions remain rejected.

The replacement flow has the following contract:

- initial sign in attaches a new `IUserPeerExtension`;
- refreshing the same account keeps the existing extension and does not repeat login/logout events;
- changing to another account emits logout for the old identity and login for the new identity;
- an existing session of the validated target account is disconnected after ownership moves;
- callers that disable target-session takeover receive `DuplicateLogin` without changing either
  session;
- identity changes are rejected with `Conflict` while either the current or displaced target session
  is joined to a room;
- transitions for the same account are serialized so concurrent takeovers cannot interleave lifecycle events;
- account permissions are reset before the new identity becomes visible to message handlers.

This is a trusted server-side API. Do not expose it as a client-selectable account identifier without
first validating the platform signature or token that proves ownership of the target identity.

## Email Sign In

Email sign in is a two-step flow. Requesting a code does not authenticate the peer and does not change
an existing account password.

```csharp
using MasterServerToolkit.MasterServer;

public void RequestEmailSignIn(string email)
{
    Mst.Client.Auth.SignInWithEmail(email, (account, error) =>
    {
        if (!string.IsNullOrEmpty(error))
        {
            ShowError(error);
            return;
        }

        ShowEmailCodeForm(email);
    });
}
```

Confirm the delivered one-time code:

```csharp
using MasterServerToolkit.MasterServer;

public void ConfirmEmailSignIn(string email, string code)
{
    Mst.Client.Auth.ConfirmEmailSignIn(email, code, (account, error) =>
    {
        if (account == null)
        {
            ShowError(error);
            return;
        }

        ShowSignedInState(account.Id, account.Username, account.IsGuest, account.IsEmailConfirmed);
    });
}
```

Codes are stored in memory as salted SHA-256 hashes, expire automatically, are single-use, and are
consumed after the configured number of invalid attempts. A successfully delivered replacement code
invalidates the previous code. If delivery of the replacement fails, the previous code remains valid.
For an unknown address, a new confirmed account is created only after a valid code is submitted.

## Sign Out

```csharp
using MasterServerToolkit.MasterServer;

public void SignOut()
{
    Mst.Client.Auth.SignOut();
}
```

The client:

- clears `Mst.Client.Auth.Account`;
- removes the saved token from `PlayerPrefs`;
- sends `MstOpCodes.SignOut`;
- invokes `OnSignedOutEvent`.

The server:

- removes the user from `AuthModule.LoggedInUsers`;
- clears `IUserPeerExtension`;
- invokes `OnUserLoggedOutEvent`.

## Email Confirmation

Request a confirmation code:

```csharp
using MasterServerToolkit.MasterServer;

public void RequestEmailCode()
{
    Mst.Client.Auth.RequestEmailConfirmationCode((isSuccessful, error) =>
    {
        if (!isSuccessful)
        {
            ShowError(error);
            return;
        }

        ShowCodeSent();
    });
}
```

Confirm the code:

```csharp
using MasterServerToolkit.MasterServer;

public void ConfirmEmail(string code)
{
    Mst.Client.Auth.ConfirmEmail(code, (isSuccessful, error) =>
    {
        if (!isSuccessful)
        {
            ShowError(error);
            return;
        }

        ShowEmailConfirmed();
    });
}
```

This flow requires a configured `Mailer` and SMTP settings.

## Password Reset

Request a reset code:

```csharp
using MasterServerToolkit.MasterServer;

public void RequestPasswordReset(string email)
{
    Mst.Client.Auth.RequestPasswordReset(email, (isSuccessful, error) =>
    {
        if (!isSuccessful)
        {
            ShowError(error);
            return;
        }

        ShowPasswordResetCodeSent();
    });
}
```

Change password:

```csharp
using MasterServerToolkit.MasterServer;

public void ChangePassword(string email, string code, string newPassword)
{
    Mst.Client.Auth.ChangePassword(email, code, newPassword, (isSuccessful, error) =>
    {
        if (!isSuccessful)
        {
            ShowError(error);
            return;
        }

        ShowPasswordChanged();
    });
}
```

## Account ExtraProperties

`ExtraProperties` is account metadata that can be changed by the client through `AuthClient`.
Changing `Mst.Client.Auth.Account.ExtraProperties` locally does not persist anything.

Correct usage:

```csharp
using MasterServerToolkit.MasterServer;

public void SaveDisplayLanguage(string language)
{
    Mst.Client.Auth.SetProperty("language", language, (isSuccessful, error) =>
    {
        if (!isSuccessful)
        {
            ShowError(error);
            return;
        }

        ShowSaved();
    });
}
```

Multiple values:

```csharp
using MasterServerToolkit.MasterServer;

public void SaveAccountMetadata()
{
    var properties = new MstProperties();
    properties.Set("language", "en");
    properties.Set("avatar", "default");

    Mst.Client.Auth.SetProperties(properties, (isSuccessful, error) =>
    {
        if (!isSuccessful)
        {
            ShowError(error);
            return;
        }

        ShowSaved();
    });
}
```

Do not use `ExtraProperties` for:

- currency;
- inventory;
- purchases;
- permissions;
- progress;
- anti-cheat;
- any state that must be server-authoritative.

## Account Lookup From A Room Or Game Server

A room server usually connects to the master server as a trusted server process and uses
`Mst.Server.Auth`. It can request short account information by master peer id or username.

Lookup by peer id:

```csharp
using MasterServerToolkit.MasterServer;

public void LoadRoomUserAccount(int masterPeerId)
{
    Mst.Server.Auth.GetAccountInfoByPeer(masterPeerId, (account, error) =>
    {
        if (account == null)
        {
            HandleAccountLookupError(error);
            return;
        }

        string userId = account.UserId;
        string username = account.Username;
        bool isGuest = account.IsGuest;

        ContinueRoomJoin(userId, username, isGuest);
    });
}
```

Lookup by username:

```csharp
using MasterServerToolkit.MasterServer;

public void LoadRoomUserAccount(string username)
{
    Mst.Server.Auth.GetAccountInfoByUsername(username, (account, error) =>
    {
        if (account == null)
        {
            HandleAccountLookupError(error);
            return;
        }

        ContinueRoomJoin(account.UserId, account.Username, account.IsGuest);
    });
}
```

`RoomUserAccountInfoPacket` contains:

- `PeerId`;
- `UserId`;
- `Username`;
- `IsGuest`;
- `ExtraProperties`.

The room server should treat this data as identity/context only. Gameplay data should be loaded
from `ProfilesModule` or your own authoritative server-side game logic.

## SignIn Protocol Flow

Simplified flow:

1. Client connects to the master server through `IClientSocket`.
2. `AuthClient.SignIn...` builds `MstProperties`.
3. `AuthClient` serializes the credentials and asks `Mst.Security.EncryptForMaster(...)` to
   protect one `MstOpCodes.SignIn` request with purpose `auth.sign-in`.
4. The master issues a short-lived one-time challenge containing its active RSA public seal key,
   a key ID and random challenge data.
5. The client generates a random AES-256 key, encrypts the credentials with AES-GCM and binds the
   purpose, target opcode, key ID and challenge data as authenticated associated data.
6. The client wraps the AES key with RSA-OAEP-SHA256 and sends the versioned sealed envelope as
   `MstOpCodes.SignIn`.
7. The master consumes the challenge once, unwraps the AES key and accepts the credentials only
   when the purpose, opcode and AES-GCM authentication tag are valid.
8. `AuthModule.RunAuthFactory(...)` chooses the flow:
   - `-userIsGuest` -> guest sign in;
   - `-userAuthToken` -> token sign in;
   - `-userName` + `-userPassword` -> username/password sign in;
   - `-userEmail` -> request an email sign-in code;
   - `-userEmail` + `-userEmailSignInCode` -> confirm email sign in;
   - `-userPhoneNumber` -> not supported by the base module.
9. Server checks database state, account blocking, duplicate login, and token validity.
10. Server responds with `AccountInfoPacket`.
11. Client stores the packet in `Mst.Client.Auth.Account`.

Native and Editor clients use the managed BouncyCastle backend. WebGL uses the browser Web Crypto
API through the MST WebGL bridge. Both backends produce the same protocol envelope and keep the
public callback-based `AuthClient` flow on the Unity thread.

The master stores persistent data and seal keys in `-mstSecurityKeyRingFile`. The file is generated
on first master startup and must be preserved across deployments. New tokens use authenticated
version `v2`; valid legacy CBC+HMAC tokens remain readable and are replaced with `v2` immediately
after a successful token sign-in.

## Main Opcodes

- `MstOpCodes.SignIn`
- `MstOpCodes.SignUp`
- `MstOpCodes.SignOut`
- `MstOpCodes.GetPasswordResetCode`
- `MstOpCodes.ChangePassword`
- `MstOpCodes.GetEmailConfirmationCode`
- `MstOpCodes.ConfirmEmail`
- `MstOpCodes.GetAccountInfoByPeer`
- `MstOpCodes.GetAccountInfoByUsername`
- `MstOpCodes.SetProperties`

## Credential Keys

- `MstParamKeys.USER_NAME` -> `-userName`
- `MstParamKeys.USER_EMAIL` -> `-userEmail`
- `MstParamKeys.USER_EMAIL_SIGN_IN_CODE` -> `-userEmailSignInCode`
- `MstParamKeys.USER_PASSWORD` -> `-userPassword`
- `MstParamKeys.USER_IS_GUEST` -> `-userIsGuest`
- `MstParamKeys.USER_AUTH_TOKEN` -> `-userAuthToken`
- `MstParamKeys.USER_REMEMBER_ME` -> `-userUserRememberMe`
- `MstParamKeys.RESET_PASSWORD_EMAIL` -> `-resetPasswordEmail`
- `MstParamKeys.RESET_PASSWORD_CODE` -> `-resetPasswordCode`
- `MstParamKeys.RESET_PASSWORD` -> `-resetPassword`

## Events

Client events:

```csharp
Mst.Client.Auth.OnSignedInEvent += OnSignedIn;
Mst.Client.Auth.OnSignedOutEvent += OnSignedOut;
Mst.Client.Auth.OnSignedUpEvent += OnSignedUp;
Mst.Client.Auth.OnEmailConfirmedEvent += OnEmailConfirmed;
Mst.Client.Auth.OnPasswordChangedEvent += OnPasswordChanged;
Mst.Client.Auth.OnExtraChangedEvent += OnExtraChanged;
```

Server events:

```csharp
authModule.OnUserLoggedInEvent += OnUserLoggedIn;
authModule.OnUserLoggedOutEvent += OnUserLoggedOut;
authModule.OnUserRegisteredEvent += OnUserRegistered;
authModule.OnUserEmailConfirmedEvent += OnUserEmailConfirmed;
```

If an event subscription lives longer than a short local workflow, unsubscribe when the owner is
destroyed or disposed.

## Common Errors

`connectionStatusDisconnected`
: The client called an auth method before connecting to the master server.

`DuplicateLogin`
: The account is already signed in from another peer session, or the current peer is already signed in.

`Invalid`
: Invalid credentials, invalid email, short password, invalid username, or invalid confirmation/reset code.

`AlreadyExists`
: Username or email is already taken.

`TokenExpired`
: The saved token has expired.

`Forbidden`
: Guest login is disabled.

`Unauthorized`
: The peer does not have the exact permission required by a protected server API.

Malformed, expired, replayed or tampered authentication envelopes are returned as `Invalid`.

## Minimal Client Workflow

```csharp
using MasterServerToolkit.MasterServer;
using UnityEngine;

public class LoginController : MonoBehaviour
{
    public void LoginAsGuest()
    {
        if (!ClientToMasterConnector.Instance.IsConnected)
        {
            return;
        }

        Mst.Client.Auth.SignInAsGuest((account, error) =>
        {
            if (account == null)
            {
                ShowError(error);
                return;
            }

            ShowSignedInState(account.Username);
        });
    }

    public void LoginWithPassword(string username, string password)
    {
        if (!ClientToMasterConnector.Instance.IsConnected)
        {
            return;
        }

        Mst.Client.Auth.RememberMe = true;
        Mst.Client.Auth.SignInWithLoginAndPassword(username, password, (account, error) =>
        {
            if (account == null)
            {
                ShowError(error);
                return;
            }

            ShowSignedInState(account.Username);
        });
    }

    private void ShowError(string error)
    {
        // Show the error in your UI.
    }

    private void ShowSignedInState(string username)
    {
        // Update your UI and continue loading player data.
    }
}
```

## Recommended Startup Order

1. Start the master server with `AuthModule` and an account database accessor.
2. Make sure the master server listens on the client connection port.
3. Start the client.
4. Wait for `ClientToMasterConnector.OnConnectedEvent`.
5. Run token sign in, guest sign in, or username/password sign in.
6. After successful authentication, load the player profile through `ProfilesModule` if needed.
7. When joining a room/game server, validate identity through the room access flow and `AuthServer`.

## Security Rules

- Never trust client-writable `ExtraProperties` as authoritative state.
- Do not store token secrets in client-side WebGL or desktop files.
- Change `-mstTokenSecret` for every production project.
- Do not log passwords, tokens, or full database connection strings.
- Check authentication on the server before room, profile, inventory, economy, or admin operations.
- If external platform authentication is provided through `GameServiceBridge`, validate signed identity
  on the server. Do not trust a player name sent by the client.
