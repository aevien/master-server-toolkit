# Desktop Service

This service is the default native PC fallback path.

## Main Types

- `DesktopService` creates desktop service modules.
- `DesktopPlayerModule` provides a guest/fallback player identity.
- `DesktopUnsupportedModules` contains unsupported optional feature modules.

## Rules

- Desktop service supports guest player identity, not external platform authentication.
- `IsAuthenticationSupported` should remain false unless a real desktop platform auth provider is added.
- Keep this service usable for regular PC builds that connect directly to MST.
- Optional modules should report unsupported instead of throwing during UI checks.
- Guest identity and storage use `PlayerPrefs` and force `PlayerPrefs.Save()` after writes. In a WebGL
  `WebService` build Unity persists these values in browser-managed local storage.
- `WebService` selects `ru` and `tr` from `Application.systemLanguage` on first launch and falls back
  to `en`; a language later saved in the storage module remains the user's explicit preference.
- Storage save failures must complete `OnSaveEvent` with `isSuccess = false`; callers must never wait
  forever because a local persistence API threw.
