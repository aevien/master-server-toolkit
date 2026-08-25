# Itch Service

This service integrates Itch desktop identity and keeps a path for future Itch web support.

## Main Types

- `ItchService` selects Itch service modules.
- `ItchPlayerModule` reads Itch desktop user data/token where available.
- `ItchUnsupportedModules` reports unsupported optional features.

## Rules

- Desktop Itch auth depends on the launcher/environment token being available.
- Empty Itch player names should fall back to generated MST guest names.
- Non-empty platform names, including placeholders, are valid service values.
- Purchases and ads are currently unsupported in this bridge.
- Keep future Web support separated from desktop token behavior.
- Interactive OAuth reports unsupported until the Web callback can return a verified token and
  complete the original authentication callback. Reserved OAuth settings do not advertise a
  capability by themselves.
- Guest identity and fallback storage are persisted immediately through `PlayerPrefs`. Storage
  exceptions complete `OnSaveEvent` with a failure result.
- Storage waits until the player module resolves its final guest or Itch user ID before selecting
  the local key. Loading and saving therefore use the same per-player key.
