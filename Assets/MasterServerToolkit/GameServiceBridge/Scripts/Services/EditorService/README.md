# Editor Service

The Editor service simulates a game platform while the project runs in the Unity Editor. It provides local player identity, storage, purchases, leaderboards, advertisements, analytics, and sharing without contacting a real platform SDK.

## Player Identity

`Start As Guest` defines the identity available when the service starts:

- Enabled: the service loads a generated local guest identity. Its ID persists between Editor runs,
  while its platform display name remains empty so the game uses the normal MST guest-name flow.
  Before the game authentication workflow starts, the Editor service removes only the saved MST
  authentication token so a previously remembered account cannot replace the requested guest session.
- Disabled: the service immediately uses `User Id` and `Authenticated Display Name` as an authenticated platform identity.

Calling `Player.Authenticate()` while the service is using a guest identity simulates successful platform authentication. Before the authentication callback and player events run, the module:

1. Replaces the guest identity with the configured `User Id` and `Authenticated Display Name`.
2. Marks the player as non-guest.
3. Loads the storage that belongs to the authenticated user.

The authenticated identity always comes from the current Inspector settings. It is not restored from `PlayerPrefs`, so changing `User Id` reliably simulates signing in as a different platform user.

Disable `Interactive Authentication Supported` while `Start As Guest` is enabled to simulate a
guest-only desktop service. In that mode the game can expose its regular MST username/password login
and registration UI. A direct `Player.Authenticate()` call reports failure and leaves the guest
identity unchanged.

### Identity Settings

- `Start As Guest`: starts with a persistent generated guest identity when enabled, and removes the
  saved MST authentication token before startup authentication. It does not clear other
  `PlayerPrefs`, Editor service storage, or the persistent guest platform identity. The configured
  authenticated identity remains unused until `Player.Authenticate()` succeeds.
- `Interactive Authentication Supported`: exposes the Editor platform-authentication action when
  enabled. Disable it together with `Start As Guest` to test MST login and registration instead.
- `User Id`: non-empty stable platform identifier used after authentication, or immediately when
  `Start As Guest` is disabled. Change it to test another account.
- `Authenticated Display Name`: platform display name associated with `User Id`. An empty value
  remains empty and allows the game to use its generated-name fallback.
- `Lang`: initial ISO language code reported by the service, for example `en`, `ru`, or `tr`. Saved game settings may override it later.

## Local Storage

Editor storage is isolated by the current `Player.Id`. A guest and every configured authenticated user therefore have separate data. Switching from guest to authenticated identity loads the target user's data before the authentication callback runs; guest data is not copied into the authenticated account.

Storage values are serialized with `MstJson` and saved through `PlayerPrefs`. Invalid stored JSON is ignored and replaced in memory with an empty object. Every `SaveData()` attempt raises `OnSaveEvent`: success after `PlayerPrefs.Save()`, or failure with the exception message.

Old shared Editor storage keys are intentionally not migrated. They contain local test data only.

## In-App Purchase Catalogue

`In App Purchase Products` configures the products returned by the Editor purchase module:

- `Id`: stable product identifier used by purchase requests.
- `Title`: display title returned to store UI.
- `Description`: product description returned to store UI.
- `Img Url`: HTTP(S) product image URL.
- `Price`: formatted price displayed to the player, for example `100 USD`.
- `Price Value`: numeric test price in the smallest unit expected by the catalogue.
- `Price Currency Code`: ISO 4217 currency code, for example `usd`.

The Editor purchase module rejects product IDs that are not present in this catalogue.

## Pending Purchases

The Editor service keeps unconfirmed purchases in `PlayerPrefs`, isolated by the current
`Player.Id`. `Purchase()` creates a unique token and persists the receipt before invoking the
purchase callback. This reproduces the platform contract where a successful payment remains
available until the game explicitly acknowledges it.

- `GetPurchases()` returns every valid pending receipt for the current player.
- `ProcessPurchase(token)` removes only the receipt with that token.
- Unknown and empty tokens leave the pending list unchanged.
- Invalid stored JSON is ignored. The next successful purchase replaces it with a valid list.
- Guest and authenticated Editor identities never share pending purchases.

## Leaderboard Mirror

The Editor leaderboard module accepts the full signed 64-bit MST score and completes the same
`SuccessCallback` used by production platforms. The game's visible leaderboard still comes from MST;
the Editor module only simulates successful platform mirroring after MST confirms a score.

To test purchase recovery, complete an Editor purchase while the master is unavailable or stop
Play Mode before the game calls `ProcessPurchase()`. Start the same identity again and confirm that
the purchase is returned by `GetPurchases()`. After successful server processing, the game calls
`ProcessPurchase(token)`, and the receipt must not appear on the next run.

## Validation Scenarios

1. Enable `Start As Guest`, enter Play Mode twice, and confirm that the guest ID remains unchanged
   and the platform player name remains empty.
2. Authenticate the guest and confirm that player ID and name change to the Inspector values before the authentication callback uses them.
3. Save an MST account token, enable `Start As Guest`, and confirm that startup removes the token and
   creates a guest MST session instead of restoring the remembered account.
4. Disable `Interactive Authentication Supported` while keeping `Start As Guest` enabled and confirm
   that the account button opens the MST login and registration flow instead of platform login.
5. Save different values as the guest and authenticated user, switch identities, and confirm that each identity reloads only its own values.
6. Disable `Start As Guest`, change `User Id`, and confirm that the service immediately uses that
   identity and starts with empty storage for the newly simulated account.
7. Create an Editor purchase without acknowledging it, restart with the same identity, and confirm that `GetPurchases()` returns it.
8. Acknowledge one of several pending purchases and confirm that only its token is removed.

## Rules

- Preserve deterministic fake-platform behavior because demos and local tests depend on it.
- Keep unsupported or placeholder behavior visible through capability flags.
- Do not copy Editor-only persistence assumptions into real platform services.
- Keep Editor modules free of production secrets.
