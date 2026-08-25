# Base Service Implementations

This folder contains reusable base classes for service and feature modules.

## Main Types

- `BaseService` manages common service lifecycle and module readiness.
- `BaseServiceModule` implements common module state (`IsReady`, `IsSupported`) and lifecycle hooks.
- `BasePlayerModule`, `BaseAdvertisementModule`, `BaseInAppPurchaseModule`, `BaseStorageModule`
  and `BaseLeaderboardsModule` provide default feature behavior.

## Rules

- Default unsupported behavior should be explicit and predictable.
- A module can be ready while unsupported. UI should check both readiness and support.
- Do not block service readiness forever when a platform feature is unavailable.
- Base classes should stay SDK-independent.
- Unsupported leaderboard score submissions complete immediately with `leaderboards_not_supported`;
  callers must never be left waiting for a platform callback that cannot arrive.
