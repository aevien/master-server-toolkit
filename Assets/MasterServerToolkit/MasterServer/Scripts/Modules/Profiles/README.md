# Profiles Module

The profiles module synchronizes observable player profile data between master, client and room
servers. It is generic persistence/synchronization infrastructure, not game-specific profile policy.

## Key Files

- `ProfilesModule.cs` - authoritative profile load/save/update server module.
- `ProfilesClient.cs` - client facade for loading local profile values and receiving updates.
- `ProfilesServer.cs` - room/server facade for filling and sending profile changes.
- `ObservableProfile.cs` and `ObservableServerProfile.cs` - profile containers and dirty tracking.
- `Properties` - observable value/list/dictionary implementations.
- `Populators` - ScriptableObject schema/default-value providers.
- `IProfilesDatabaseAccessor.cs` - database persistence contract.

## Flow

On auth login, `ProfilesModule` starts one shared initial-load operation per user ID. A new profile
candidate stays private while the database restore, `PrepareProfileAsync`, `ProfileLoaded` and
`OnProfileLoaded` hooks run. Only then is it added to the ready profile lookup and attached to the
current authenticated peer through `ProfilePeerExtension`. Concurrent logins await the same operation.
Each failed retry uses a new candidate so partially restored state cannot be published.

`PrepareProfileAsync` is the awaitable extension point for server-side managed-data preparation that
must finish before publication. It does not guarantee Unity main-thread affinity. Existing synchronous
profile initialization remains in `ProfileLoaded` or `OnProfileLoaded`; load hooks must be idempotent
because an exception can restart the complete attempt.

Room servers request full profiles through `ServerFillInProfileValues` and send deltas through
`ServerUpdateProfileValues`. Clients request initial values through `ClientFillInProfileValues`
and then receive `UpdateClientProfile` messages.

`ProfilesClient.UnloadProfile()` unregisters the client update handler, clears `Current` and disposes
the local observable profile. A game must call it when the authenticated account is signed out or
replaced so UI and gameplay code cannot continue reading values owned by the previous account.
`OnProfileLoadedEvent` is a persistent subscription: a late subscriber receives the current profile
immediately and remains subscribed for later account/profile replacements until it unsubscribes.

Every `QueueProfileToSave` call replaces the pending entry for that user with a new ownership token. A save
batch keeps its entries in the pending lookup until the database call succeeds, then removes only the
exact tokens that were persisted. A modification queued while the batch is in flight therefore remains
for the next batch. Failed batches remain pending and are retried by the normal save throttle.
`SaveProfileAsync` shares the same persistence semaphore with those batches, so a master-side caller
can establish a durable boundary without racing an older in-flight save.

On a room server, `Mst.Server.Profiles.SaveProfile(profile, callback)` captures only the profile delta
that exists at the time of the call and sends it through `ServerUpdateProfileValues` as a confirmed
request. The master applies that delta under the per-user operation lock and responds only after the
resulting profile state has been persisted. Changes made in the room after the capture remain a separate
delta. Confirmed saves for the same room profile are serialized. A failed or ambiguous confirmed save is
not retried automatically because the master may already have persisted it; the caller must treat that
outcome as unknown.
`TryUpdateOfflineProfileAsync` additionally coordinates dashboard-style offline edits with profile
login/restore. The edit is rejected while the user is logged in or while an in-memory profile still
awaits its final logout save, preventing a detached snapshot from overwriting newer state.

## Inspector Configuration

- `Unload Profile After` is the post-logout delay in seconds before an in-memory profile is removed.
  `0` starts unload immediately.
- `Save Profile Debounce Time` and `Client Update Debounce Time` are throttle intervals in seconds.
  `0` removes the intentional delay but still uses the normal queued execution path.
- `Max Update Size` is the maximum accepted serialized delta in bytes. Oversized room/client updates
  are rejected before deserialization.
- `Profile Load Timeout Seconds` bounds database restore and asynchronous profile preparation and must
  remain positive.
- `Database Accessor Factory` owns profile persistence. `Populators Database` owns the shared profile
  schema, defaults and synchronization behavior; clients and room servers must use a compatible schema.

`ObservableIntPopulator` can enable delta updates for additive values. Full profile serialization and
database persistence still use absolute integers, while incremental room/master/client updates carry
the signed change. Use this only for properties such as currencies whose runtime mutations are additive.
`ApplySynchronizedDelta` mirrors an already-authoritative remote change while preserving any local
pending delta; it must not be used to grant an unconfirmed reward.

Every populator `Key` is a stable persisted property identifier and must be unique in the populators
database. `Default Value` is used only when a property is first created; restored profiles keep their
stored value. Dictionary defaults use unique keys and assign the configured value to each new entry.
`ObservableProfile.TryGet<T>` succeeds only when the key exists and the property has the requested
runtime type. A schema/type mismatch returns `false` and must be corrected in the populator database.

Deferred logout unload follows the same confirmation boundary. The ready in-memory profile remains
available until its final save succeeds. A reconnect during that save cancels pending unload and reuses
the current profile instead of restoring older database state.

The periodic profile timer belongs to a server run. Graceful shutdown stops that timer, waits for the
active throttled save and then persists the latest pending ownership tokens before transport teardown.
The managed shutdown flush remains valid when Unity has already destroyed the native module component;
Unity API calls are skipped in that case. A failed final flush leaves its entries pending and fails the
server-run stop instead of reporting unsaved profiles as durable. Once shutdown begins, new queued,
confirmed and offline persistence operations are rejected; operations that already entered the shared
persistence semaphore finish before the final pending snapshot is written. Pending logout unloads are
cancelled when a run stops, and their generation token prevents an old callback from entering a later run.

## Rules

- Add new profile fields through populators/properties, not ad hoc packet fields.
- Keep binary serialization order stable for each property type.
- Bound update payload size before applying updates.
- Do not expose a profile to clients before database restore and load hooks finish.
- Treat `profilesList`, `Profiles` and `GetProfileByUserId` as ready-profile lookups only.
- Publish a profile only to the current auth session; stale login/logout callbacks must not own it.
- Keep session attachment and deferred profile unload synchronized so a reconnect cannot attach a profile while an older unload callback removes it.
- Remove pending saves only by exact ownership token after the database confirms success.
- Use the confirmed room `SaveProfile` boundary before acknowledging irreversible operations such as purchases.
- Do not automatically retry a failed confirmed delta; its persistence result may be ambiguous.
- Use delta integer updates only when every runtime mutation has additive semantics.
- Do not start required asynchronous profile initialization as fire-and-forget work from load hooks.
- Unsubscribe `OnModifiedInServerEvent` and auth events when unloading/destroying.

## Known Sharp Edges

Observable collections can be modified while serializing if code mutates them from another path.
Prefer snapshots before enumeration when adding new collection serialization/update logic.

The shutdown flush protects orderly server stop, application quit and scene teardown. It cannot run
after a hard process termination, operating-system crash or power loss. Critical operations must still
use the confirmed `SaveProfileAsync`/room `SaveProfile` durability boundary before acknowledging success.
