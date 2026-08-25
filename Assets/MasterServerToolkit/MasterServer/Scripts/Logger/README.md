# Logger

This folder contains MST runtime logging. Use it instead of raw Unity logging in framework,
server and service code.

## Main Types

- `Logger` is a named logger instance with per-logger `LogLevel`.
- `Logs` is the global fallback logger helper.
- `LogManager` owns logger creation, appenders, global levels and pre-initialization pooling.
- `LogAppenders` and `ConsoleLogAppender` route MST logs to Unity console output.
- `FileLogAppender` writes logs to a configured file and can include Unity log messages.
- `LogChannels` and `LogChannelFilter` group/filter logs by channel.

## Rules

- Prefer a component's existing `logger`; otherwise use `Mst.Create.Logger(...)` or `Logs`.
- Runtime modules should not call `UnityEngine.Debug.Log` directly.
- When adding appenders, provide a removal/disposal path.
- Appenders are invoked outside the manager lock and may run concurrently; implementations must be thread-safe.
- Keep channel names stable if configs or tooling rely on them.
- File appenders must be disposed before replacing or shutting down logging.
