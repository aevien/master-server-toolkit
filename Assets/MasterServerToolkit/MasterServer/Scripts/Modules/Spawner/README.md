# Spawner Module

The spawner module starts and supervises external room server processes.

## Key Files

- `SpawnersModule.cs` - authoritative master-side spawner registry, task list and queue routing.
- `SpawnerBehaviour.cs` - process-side component that registers as a spawner.
- `SpawnerController.cs` - local controller that starts/kills OS processes.
- `RegisteredSpawner.cs` - master-side spawner state and request queue.
- `SpawnTask.cs`, `SpawnTaskController.cs`, `SpawnRequestController.cs` - spawn task state,
  status notifications and client-side task control.
- `SpawnerOptions.cs`, `SpawnerConfig.cs`, `SpawnStatus.cs` - options and status contracts.
- `Packets` - registration, spawn, kill, started, status and finalization packets.

## Flow

Spawner process connects to master and registers capacity/region. Client or lobby requests a spawn.
Master picks a spawner, creates a `SpawnTask`, queues the task and sends `SpawnProcessRequest`.
`SpawnerController` starts the room executable with generated MST args such as master endpoint,
room port, redirect port, spawn task id and unique code. The spawned room registers back and
finalizes the task with room data.

## SpawnerBehaviour Inspector

- `Machine Ip` is advertised to clients for spawned rooms and must be externally reachable where
  required. Runtime arguments can override it.
- `Executable File Path` is the normal room/server executable. `Max Processes = 0` disables new starts.
- `Region` participates in region selection; an empty value becomes `International`.
- `Kill Processes When Stop` controls whether supervised child processes are terminated during spawner
  shutdown. Disabling it deliberately leaves them running outside subsequent MST supervision.
- Editor auto-start waits for the master connection. When editor path override is enabled, `Exe Path
  From Editor` must be an absolute path to an existing built executable.
- Started/stopped Unity events are presentation or integration hooks; they do not replace registration
  state checks.

## SpawnersModule Inspector

- `Queue Update Frequency` is a realtime dispatch interval in seconds; values below `0.01` use `0.01`.
- `Max Concurrent Spawn Requests` is per registered spawner and has a runtime minimum of `1`.
- `Spawn Request Throttle Interval Ms = 0` removes the delay between requests to one spawner.
- `Enable Client Spawn Requests` affects ordinary clients, not lobby/server-owned spawn flows.
- `Shutdown Confirmation Timeout Ms` bounds supervision while a spawner closes and has a minimum of
  one millisecond.

## Rules

- Allocate and release room ports in matched pairs when spawn fails or exits.
- `SpawnTaskUniqueCode` verifies that a spawned room belongs to the task.
- Process start runs on a background thread; do not use Unity APIs there.
- Dispose controllers to unregister spawn/kill handlers.
- Keep user-driven spawn permission checks in `CanClientSpawn`.
- Spawner and spawn-task ids are allocated atomically because master handlers may execute concurrently.
- Room and redirect port allocation is synchronized across socket callbacks and process-exit threads;
  released ports are reused before new sequential ports.
- Spawner capacity check and queue reservation are one atomic operation. Duplicate start/kill reports
  are idempotent and only the registered spawner peer may mutate its process lifecycle.
- Publishing a queued spawn request is serialized with spawner closure. A task extracted from the queue
  cannot be sent after the spawner has been closed, and a request already being published is ordered
  before the matching shutdown kill request.
- A task canceled from its `StartingProcess` notification is still local until the spawn request is
  registered for transport. It completes as `Aborted` without sending either spawn or kill traffic.
- `SpawnTask` serializes status transitions and invokes each completion subscription once. A finalized
  process may still transition to `Killed`, preserving post-start process-death notifications.
- A successful kill response means that the local controller accepted the kill request. The master task
  remains `Aborting` until the terminal process notification arrives. Failed kill requests remain
  retryable, are retried after the same bounded delay and do not falsely complete the task. A bounded confirmation watchdog retries an accepted
  kill when the terminal notification is lost; `NotFound` confirms that the controller no longer owns
  the process operation, while a missing controller returns `ServiceUnavailable` and is not treated as
  proof that the operating-system process stopped.
- Client status updates are queued until the spawn-request ACK has exposed the task id to the client.
- Controller shutdown reserves pending launches before argument/port work, prevents late process starts,
  releases allocated ports once and invokes the spawn callback once. When process termination is enabled,
  worker shutdown waits are bounded and supervisory workers are background threads, so a failed child
  termination cannot keep the application alive forever. `killProcessesWhenStop = false` intentionally leaves spawned
  processes supervised until they exit instead of terminating them during controller disposal.
- Disposed controllers remove themselves from the process-side `SpawnersServer` registry. A late
  registration callback is rejected after `SpawnerBehaviour` stops or is destroyed. Shared spawn/kill
  message handlers are owned once by `SpawnersServer`, so disposing one controller cannot disable other
  controllers on the same connection. Controllers registered through the custom-connection overload
  retain handlers, lifecycle notifications and port ownership on that same connection/server instance.
- Master-side registered spawners use `Active -> Closing -> Closed`. `Closing` rejects registration of
  new tasks but retains every dispatched or running task until `ProcessKilled` or a `NotFound` kill
  response confirms that the controller no longer owns the process. Tasks that never left the master
  queue are aborted locally.
- Registration and destruction events use a per-spawner ordered queue. Once a spawner enters the master
  registry, its `OnSpawnerRegisteredEvent` is always published before `OnSpawnerDestroyedEvent`, even
  when registration races server shutdown. Callbacks run outside registry and lifecycle locks.
- Server-run shutdown closes admission before taking its spawner snapshot, sends kill requests for live
  tasks and waits for their request responses for a bounded interval. `NotFound` confirms terminal
  process absence. `Success` confirms only that shutdown kill was accepted, so the master abandons local
  supervision without reporting the process as `Killed`; normal unregister continues waiting for the
  terminal `ProcessKilled` notification. A missing response falls back to the bounded timeout.
- The bounded supervisor belongs to each closing spawner and is created exactly once. Manual unregister,
  peer disconnect and full server shutdown therefore share the same cleanup guarantee. Server shutdown
  awaits the supervisors that were already active instead of starting a second competing timeout.
- `ServerBehaviour` stops admitting new handler messages before module shutdown begins. Consequently a
  normal `ProcessKilled` notification is not expected to reach `SpawnersModule` during this phase. The
  kill response is therefore the final network observation owned by full server shutdown. Abandoned
  tasks stop every pending kill watchdog so no detached retry loop retains their object graph.
- `UnregisterSpawner` removes only the exact registration owned by the requesting peer. Requests for an
  unknown registration return `NotFound`; attempts to unregister another peer's spawner return
  `Forbidden`. The registration disappears immediately, while its live tasks remain supervised through
  the closing lifecycle.
