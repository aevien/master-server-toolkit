# Analytics Module

This module accepts generic analytics records and writes them through a configured database accessor.

## Main Types

- `AnalyticsModule` is the authoritative server module.
- `AnalyticsModuleClient` sends analytics records from a client connection.
- `AnalyticsModuleServer` is a server-side facade.
- `IAnalyticsDatabaseAccessor` is the persistence contract.
- `IAnalyticsInfoData` and `AnalyticsDataInfoPacket` describe analytics payloads.
- Analytics database accessors expose paged/search reads through `Search(Dictionary<string, object>)` and return `DatabaseEntriesInfo<IAnalyticsInfoData>`.

## Inspector Configuration

- `Use Analytics` controls persistence for received analytics records. It requires an analytics
  database accessor factory when enabled.
- `Save Debounce Time` is measured in seconds and batches events received before the next eligible
  database write. `0` removes the intentional delay.
- `Database Accessor Factory` creates the persistence implementation; leave it unassigned only when
  analytics is disabled or an accessor is registered from code before initialization.

## Rules

- Treat analytics input as untrusted and bounded.
- Do not put project-specific event schemas into MST core; keep records generic or define them outside MST.
- If analytics is optional, missing accessors should fail clearly without breaking unrelated modules.
- Avoid logging sensitive analytics payloads by default.
- Analytics events are skipped while the master connection is unavailable. Session events skipped or
  interrupted by a local disconnect are not marked as delivered and may be sent after reconnection.
- Local transport terminal responses are not decoded as structured master errors. Genuine master
  rejections keep their `MstProperties` error-code handling.
- A server run owns its pending batch and active database write. Shutdown waits for the active write,
  requeues failed snapshots and flushes records queued behind that write before transport teardown.
- Shutdown can be requested through the server-run interface after Unity has destroyed the native
  module component. Managed persistence work must still finish, while Unity API calls such as
  `CancelInvoke` must first verify that the component is still alive.
- Database accessors must propagate insert failures so the module can preserve the batch for retry.
- `Save` accepts data only during an active server run and reports that decision to callers. Network
  requests outside that lifecycle return `ServiceUnavailable` instead of acknowledging dropped data.
