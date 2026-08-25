# GameServiceBridge Scripts

This folder contains the runtime platform-service abstraction used by MST clients.

## Main Areas

- `GameBridge.cs` - service detection, singleton lifecycle and access to the active service.
- `Services` - service interfaces, base modules and platform implementations.
- `Models` - small DTOs returned by platform modules.
- `Keys` - shared platform-service string keys.
- `Plugins` - platform-specific JS/native plugin assets.

## Rules

- Keep platform SDK details inside the matching service folder.
- Game UI should read module capabilities (`IsSupported`, `IsReady`, `IsAuthenticationSupported`)
  instead of checking concrete service types.
- Do not expose platform secrets in client builds.
- Empty platform names are missing values; non-empty fallback/generated names are valid display names.
- Player modules start in the guest state. A platform implementation may clear `IsGuest` only after
  it has confirmed the platform-specific identity; a non-empty identifier alone is not a universal
  proof of platform authorization.
- `IsAuthenticationSupported` means `Authenticate(...)` can start and complete an interactive
  platform login. Launch-credential-only services and reserved future OAuth settings report false.

## Inspector Configuration

`GameBridge` owns one settings block per detectable platform. Only the block selected by runtime
platform detection is serialized into the active service options. `Wait For Ready Time` is a realtime
SDK initialization timeout in seconds and must remain positive.

- `Yandex Games` controls data-save, advertising, leaderboard-cache, and automatic API-ready timing.
- `VK Play` maps launcher argument names and optional Steam profile data. Editor detection uses the
  explicit test identity only when enabled.
- `Itch` reads desktop app-token credentials from configured environment-variable names. Editor
  detection is opt-in. The OAuth fields are reserved for the future Web implementation.
- `Editor` provides the local identity, language, guest status, and simulated IAP catalogue. Keep its
  user ID stable when validating account persistence and change it intentionally when testing a new
  platform account.

The bridge settings are client-side configuration. Never place master-server secrets or database
credentials in these fields.

## Demo Component

`GameBridgeDemo` is an optional UI driver for manually checking the active platform service:

- `Output` is a required TextMeshPro label. The demo appends service, referrer, player, storage, ad,
  and purchase results without clearing existing text.
- `Auth Button` is a required GameObject. It is visible only while the active service player is a
  guest and the service supports interactive platform authentication; its state is updated every
  frame.

The public `OnClick...` methods are intended for Unity UI Button events. Add the component only after a
`GameBridge` service is configured for the scene; the demo assumes both Inspector references and
`GameBridge.Service` are available.
