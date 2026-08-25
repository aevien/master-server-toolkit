# VK Play Service

This service integrates the VK Play browser JavaScript API for WebGL and keeps
the existing guarded desktop launch-argument path for compatibility.

Official contracts used by this implementation:

- [VK Play browser JS API](https://documentation.vkplay.ru/f2p_vkp/f2pb_js_vkp)
- [VK Play GAS authorization](https://documentation.vkplay.ru/f2p_vkp/f2pc_authgc_vkp)
- [VK Play billing](https://documentation.vkplay.ru/f2p_vkp/f2pc_billing_vkp)

## Main Types

- `VkPlayService` loads `mailru.core.js`, completes the `iframeApi` handshake,
  reads the VK Play launch environment and creates the platform modules.
- `VkPlayPlayerModule` handles browser login status, registration, profile data
  and the one-time GAS authentication token.
- `MstGameServiceBridge_VkPlayPlatform.jslib` owns browser-only calls and sends
  JSON callbacks to the `MST_GAME_BRIDGE` object.
- VK Play ad, purchase, leaderboard, analytics, share and storage modules implement optional features.

## WebGL Setup

- Set `GameBridge > VK Play > App Id` to the VK Play GMRID. A signed VK Play
  launch may also supply it as the `appid` query parameter.
- Keep `Web Script Url Template` at
  `https://vkplay.ru/app/{0}/static/mailru.core.js`, unless the hosting page
  already loads the matching script and exposes `window.iframeApi`.
- The generic WebGL detector selects VK Play only when the URL contains
  `appid`, `uid` and `sign`. VK Games detection remains separate.
- Login status `0` opens VK Play authorization. Status `1` registers the user
  and reloads the iframe, as required by VK Play before signed values and tokens
  can be relied on. Status `2` or `3` loads the profile and GAS token directly.

## Server Authentication

The client sends the VK Play `uid` and one-time `hash` returned by
`getAuthToken()`. `VkPlayPlayerValidator` validates them against GAS on the
master server.

Required master arguments:

- `-master.vkplayAppId=<GMRID>`
- `-master.vkplaySecretKey=<server secret>`

Optional arguments:

- `-master.vkplayValidateUrl=<HTTPS template>` overrides the GAS endpoint for
  controlled testing. The template must contain `{0}` for GMRID.
- `-master.vkplayValidateIpOverride=<IPv4>` is a diagnostic/development escape
  hatch only. Production should validate the real player IPv4.

GAS accepts the same OTP only once. The validator therefore remembers an
accepted token for reconnects, keyed by app, user, token and IPv4, without
storing or logging the raw token. This cache is process-local and lasts up to
24 hours; restarting master requires the browser to obtain a fresh token.

When master runs behind a reverse proxy, its socket endpoint may be the proxy
address rather than the player address. Production deployment must preserve a
trusted original IPv4 path before VK Play authentication can be considered
complete.

## Feature Status

- Browser authorization/profile/GAS identity: implemented; requires a real
  VK Play iframe and master configuration test.
- Friends: `VkPlayPlayerModule.GetFriends(...)` exposes VK Play game-friends
  and social-friends responses to platform-specific UI code. There is no
  cross-platform generic friends contract yet.
- Payments: intentionally remain unsupported until the server-authoritative
  billing callback, signature verification, transaction idempotency and reward
  journal integration are complete.
- Leaderboards: browser operations require a server-side proxy and publisher
  secret; the existing desktop/Steam-compatible path is not used by WebGL. The guarded Steam-compatible
  implementation mirrors a confirmed MST value with `ForceUpdate` and reports the asynchronous upload
  result to its caller. Steam scores are signed 32-bit integers, so larger MST values are rejected
  without truncation. The platform table sort order and display type must be configured to match MST.
- Advertising: remains unsupported because the current public F2P JS reference
  does not publish a callable advertising method contract.

## Rules

- Platform credentials and signed values must be validated through service-specific server validation.
- Feature modules must report actual support/readiness for the active runtime.
- Keep Steamworks or other optional lookup paths guarded by availability checks.
- Do not leak service secrets into client-side code.
