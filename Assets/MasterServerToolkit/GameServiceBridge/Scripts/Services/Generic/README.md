# Generic Service Contracts

This folder defines the public contract implemented by all game service bridges.

## Main Types

- `IGameService` and `IService` describe service lifecycle and module access.
- `IServiceModule` is the base capability contract.
- `IPlayerModule` exposes platform player identity and authentication capability.
- `ReferrerInfo` exposes normalized launch referrer data with the same shape for every service.
- `IAdvertisementModule`, `IInAppPurchaseModule`, `IStorageModule`, `ILeaderboardsModule`,
  `IAnalyticsModule` and `IShareModule` describe optional platform features.
- `IPlayerValidator` validates signed platform identity payloads on the server side.
- `GameServiceId` and `ServiceDeviceType` identify active service/device families.

## Rules

- Interface changes are framework API changes; update every service implementation.
- Capability properties must be truthful for the active service.
- Keep callback signatures stable unless all call sites and examples are updated.
- Do not put SDK-specific types into generic interfaces.
- `ReferrerInfo.HasData` is controlled by `Type`; services must not fill optional referrer fields without a type.
- `ILeaderboardsModule.SetScore` mirrors a canonical signed `long` and completes its `SuccessCallback`.
  A platform may reject values outside its own numeric range; it must report that failure instead of
  truncating or silently accepting a different score.
