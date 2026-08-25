# GameServiceBridge

`GameServiceBridge` abstracts external game platforms and browser/native services. It is separate
from MST username/password authentication. It provides platform identity, storage, ads, purchases,
leaderboards, analytics and share capability modules.

## Key Files

- `GameBridge.cs` - singleton entry point. Detects platform and adds the concrete service component.
- `Services/Generic` - service/module interfaces and enums.
- `Services/Base` - reusable base implementations for service and feature modules.
- `Services/EditorService` - editor testing service.
- `Services/DesktopService` - native desktop/web fallback guest service.
- `Services/ItchService` - Itch desktop token profile support and future Web OAuth hooks.
- `Services/VKPlayService` - VK Play launch credentials and optional Steamworks profile lookup.
- `Services/VkGamesService` - VK Games WebGL identity, storage, ads, analytics and favorites through VK Bridge.
- `Services/YandexGamesService` - Yandex Games SDK bridge through WebGL `.jslib` calls.
- `Keys` and `Models` - shared service opcodes/keys and DTOs.

## Service Lifecycle

`GameBridge.Awake` detects a `GameServiceId`, adds the service component, assigns options and calls
`OnBeforeInit`. `Start` then calls `OnInit`, yields one frame and calls `OnAfterInit`. `BaseService`
waits until all modules are ready or timeout, then calls module `OnReady`.

An authenticated platform player exposes its current public name and avatar through `IPlayerModule`.
Game integration may forward those values as bridge-auth credentials, but they remain display metadata:
platform identity validation must use the service player ID and signature/token contract, never the
avatar URL.

## Module Contract

- `IsSupported` means the active service supports that feature module.
- `IsReady` means the module has completed its current initialization path.
- `IPlayerModule.IsAuthenticationSupported` means external platform authentication only.
- Desktop/Web fallback services can provide a guest player but do not support platform auth.
- Unsupported modules should become ready and report `IsSupported = false` so game UI can hide them.

## Rules

- Platform-specific calls stay inside the matching service folder.
- Do not leak service secrets to client-side WebGL code.
- Use `GameServiceKeys.PLAYER_SIGNATURE` for platform validation payloads/signatures.
- Preserve fake/editor service behavior because demos and local testing depend on it.
- Treat empty platform display names as missing; generated/fallback names are valid values.

## VK Games

The VK Games service is selected automatically only when the WebGL URL contains a VK API URL plus the signed `api_id`, `viewer_id`, `auth_key`, `sign`, and `sign_keys` launch contract. There is no generic URL override because each platform has different trustworthy detection markers. It loads the pinned VK Bridge URL configured in `GameBridge.vkGames.bridgeScriptUrl`; a hosting template may preload `window.vkBridge` and leave that URL empty.

Settings:

- `bridgeScriptUrl` - exact reviewed browser bundle URL; changing it changes executable code loaded by the page.
- `storageKey` - stable VK Storage key for the complete JSON save document.
- `saveInterval` - 6-60 seconds; rapid saves are coalesced.
- `interstitialAdInterval` - 180-600 seconds between interstitial requests.

Standard launch values populate the generic service properties. Only additional non-authentication values are exposed under `Service.Options["launchParameters"]`; the raw signed query remains authentication data. The master server validates HMAC-SHA256 and `auth_key` with `-master.vkGamesAppId` and `-master.vkGamesSecretKey`. Never put the protected key in Unity settings or the WebGL build. IAP and leaderboards deliberately report unsupported until their server-authoritative purchase callback and application-side leaderboard configuration are implemented.
