# Networking Plugins

This folder contains third-party or heavily adapted networking dependencies used by MST transports.

## Current Dependency

- `WebSocketSharp` - `websocket-sharp v1.3.1` managed plugin used by the MST
  WebSocketSharp transport, plus the separate JavaScript adapter used by WebGL.

## Rules

- Treat plugin code as dependency code. Prefer adapters in `Networking/Scripts/Transports` over
  broad plugin rewrites.
- If a plugin patch is required, keep the change narrow and document why the adapter layer was not enough.
- Do not place MST module logic in plugin folders.
- Re-test desktop and WebGL transport behavior after changing plugin code.
- Preserve the plugin's FIFO `SendAsync` contract; MST access ACKs and the first
  post-authentication message rely on call-order delivery.
