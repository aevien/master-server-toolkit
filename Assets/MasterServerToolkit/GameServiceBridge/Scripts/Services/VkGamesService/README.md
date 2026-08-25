# VK Games service

This folder owns the WebGL adapter for games launched inside VK Games. It is distinct from `VKPlayService`: VK Games uses browser URL launch parameters and VK Bridge, while VK Play is a separate store and desktop-launcher integration.

## Automatic detection

The service is selected only when the WebGL page URL contains all required VK Games identity fields:

- `api_url` with HTTPS host `api.vk.ru` or `api.vk.com`;
- `api_id`;
- `viewer_id`;
- `auth_key`;
- `sign`;
- `sign_keys`.

The generic URL value `platform=web` is VK launch metadata, not a GameServiceBridge override. There is no manual service-selection query parameter. A normal WebGL page without the complete VK Games contract continues to use `WebService`.

## Runtime ownership

- `VkGamesService` initializes VK Bridge, reads the launch environment, and owns pause/resume forwarding.
- `VkGamesPlayerModule` uses signed `viewer_id` as the platform identity and optionally enriches it with the public VK profile. The complete raw query is forwarded only as authentication proof.
- `VkGamesStorageModule` stores the client settings document under one stable VK Storage key. Account, profile, world, and economy state remain server-owned. Failed reads retain the current in-memory document, retry after five realtime seconds, and prevent queued writes from replacing remote data before a successful load. Storage requests time out after ten seconds instead of leaving the module permanently busy. A pending write is merged into the complete remote document and flushed when VK hides the game view.
- `VkGamesAdvertisementModule` maps interstitial, rewarded, and banner requests to VK Bridge. Every operation checks the current runtime capability and platform availability before showing an advertisement. Interstitial readiness is cached, refreshed after view restore, retried every 30 realtime seconds when unavailable, and enters cooldown only after a completed display.
- `VkGamesAnalyticsModule` forwards bridge analytics to the hosting page.
- `VkGamesShareModule` maps the shortcut prompt to VK favorites. Availability accounts for the signed `is_favorite` launch value and the runtime capability reported by VK Bridge. Review prompts are unsupported.
- IAP and leaderboards currently report `IsSupported = false`.

## Service contract mapping

Launch values that match the generic service contract are not duplicated in `Options`:

- `api_id` -> `IService.AppId`;
- `viewer_id` -> `IPlayerModule.Id`;
- VK profile name and avatar -> `IPlayerModule.Name` and `Avatar`;
- `language` -> `IService.Lang` (`0` is Russian and `3` is English; unknown numeric values fall back to browser language);
- `platform` -> `IService.Device`;
- `hash` -> `IService.Payload.hash` when non-empty;
- `referrer` -> `IService.Referrer.Type`, preserving the exact VK launch source.

Additional non-authentication VK fields are exposed under `IService.Options["launchParameters"]`. Authentication/session values such as `sid`, `secret`, `access_token`, `auth_key`, `timestamp`, `sign`, and `sign_keys` are deliberately excluded from `Options`. They remain available only inside the raw signed query sent to the master validator.

## Browser lifecycle

VK Bridge method support is checked through `supportsAsync()` with a compatibility fallback for older bridge instances. The service retains the exact event callback passed to `subscribe()` and removes it through `unsubscribe()` during disposal. Every initialization has its own generation, so pending asynchronous callbacks from a disposed or replaced service cannot reach the new Unity service. An accepted favorites action remains remembered for the lifetime of the browser page.

`VKWebAppViewHide` pauses the game-facing service and immediately flushes VK Storage data that has already passed the initial remote-load guard. `VKWebAppViewRestore` resumes the service and refreshes interstitial availability.

## Inspector settings

Configure `GameBridge.vkGames`:

- `Bridge Script Url`: exact reviewed HTTPS VK Bridge bundle URL. Leave empty only when the hosting page already provides `window.vkBridge`.
- `Storage Key`: stable VK Storage key. Changing it after release makes previous client settings inaccessible under the new key.
- `Save Interval`: realtime seconds between coalesced storage writes, from 6 to 60.
- `Interstitial Ad Interval`: minimum realtime seconds between interstitial requests, from 180 to 600.

## Master validation

Only the master server receives:

- `-master.vkGamesAppId`;
- `-master.vkGamesSecretKey`.

`VkGamesPlayerValidator` owns the complete validation flow. It reads the master configuration, parses the query strictly, rejects duplicate or malformed parameters, requires the identity fields to be listed in `sign_keys`, rebuilds the form-urlencoded canonical query, and validates HMAC-SHA256 Base64URL `sign`. It also validates `auth_key`, the VK API host, the expected application ID, a positive `viewer_id`, a positive timestamp, and equality between the signed `viewer_id` and the bridge player ID reported by the client. Validation failures log only a safe reason code and never expose the signed query or protected key.

The launch timestamp is not given a short expiry yet because reconnection can reuse the same page launch while the browser session remains open. Production transport must use HTTPS/WSS, and the protected application key must never enter a WebGL build or client configuration.

## Manual validation

Validate a real VK-hosted build for desktop and mobile launch detection, signed profile loading, guest handling when `viewer_id` is zero, language/device mapping, storage round-trip, every advertisement terminal status, view hide/restore, favorites acceptance/cancellation, and rejection of tampered launch data.

For advertisements, test available and unavailable interstitial, rewarded, and banner responses. Confirm that an unavailable advertisement reports `Error`, a rejected interstitial does not start its cooldown, and repeated input cannot replace the active callback.

For storage, test a normal reload, a temporary failed read followed by recovery, a settings change while the read is unavailable, and hiding the VK view with a pending save. A failed read may let the game continue with in-memory defaults, but it must not report remote data as loaded or write until a valid response is received. Existing remote settings must survive the failed-read scenario.
