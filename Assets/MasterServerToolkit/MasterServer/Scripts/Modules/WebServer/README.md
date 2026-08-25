# Web Server And Dashboard

This folder contains the built-in HTTP server, web controllers, HTML helpers and result types.
`Dashboard` builds on this server to expose admin/status pages and APIs.

## Key Files

- `HttpServerModule.cs` - `HttpListener` wrapper, route registry, Basic Auth, CORS, security
  headers, bounded concurrent request processing and lifecycle shutdown.
- `WebController.cs`, `HtmlWebController.cs`, `IWebController.cs` - route/controller contract.
- `HtmlServerModule.cs`, `DashboardModule.cs`, `DashboardPageWebController.cs` - HTML dashboard
  layer and navbar/page handling.
- `Results` - HTTP result types such as JSON, HTML, string, binary, no content, unauthorized,
  not found and server error.
- `Dashboard/Controllers` - built-in dashboard pages and APIs for rooms, users, spawners,
  notices, server info and analytics.

## Config

Use `Mst.Args.Names.WebAddress`, `WebPort`, `WebUseCredentials`, `AdminUsername`,
`AdminPassword` and `WebRealm`. Legacy web username/password args are still read as
fallback for compatibility.
When `WebAddress` is `localhost` or `127.0.0.1`, the listener registers both loopback
prefixes so either host can be used in a browser.

CORS can be configured with `WebCorsEnabled`, `WebAllowedOrigins`, `WebAllowedMethods`,
`WebAllowedHeaders`, `WebAllowCredentials` and `WebCorsMaxAge`. `WebAllowedOrigins`
accepts `*` or a comma-separated list of exact origins. Credentialed CORS is disabled
when allowed origins contains `*`; configure explicit origins to allow credentials.

HTML controllers register a relative route and combine a page fragment with either their own template
or the shared `HtmlServerModule` template. Page-specific and shared JavaScript use separate assets.
Sprite icons are encoded as Base64 and therefore require Read/Write on the source texture. Controller
HTML/about content is trusted authored content and is not automatically sanitized.

`WebController.Log Level` controls controller diagnostics. `Use Credentials` protects that
controller's routes even when server-wide credentials are disabled; enabling server-wide credentials
still protects every route. The HTTP safety limit must remain positive and bounds concurrent request
tasks, not the number of registered routes.

## Rules

- Register routes through the `RegisterGet/Post/Put/DeleteHandler` methods.
- Decide per route whether credentials are required. Server-level `WebUseCredentials`
  protects all registered routes when enabled.
- Use plain/web result types such as `BadRequest`, `NotFound`, `Unauthorized` and
  `InternalServerError` when a route should return text or an HTML page with a status code.
- Use explicit JSON result types such as `BadRequestJson`, `NotFoundJson`,
  `UnauthorizedJson` and `InternalServerErrorJson` for API endpoints that should return
  the shared JSON error body.
- Built-in fallback errors for `/api/v1` routes, including auth failure, missing route and
  unhandled handler exceptions, are returned with the JSON error result types. Non-API
  routes keep the plain/web result types.
- Do not put long-running Unity main-thread work directly into HTTP request tasks.
- `Stop()` requests shutdown and returns immediately; use and await `StopAsync()` when code must know
  that accepted requests finished and controllers were disposed before starting the listener again.
- Accepted requests are registered before their worker task is queued, so shutdown cannot dispose the
  semaphore or controllers while a delayed request task still owns them.
- `OnDestroy` uses non-blocking `Stop()` and does not guarantee that active requests finish during
  Editor Stop, application quit or a process crash.
- Keep admin/service identity (`-mstAdminId`) separate from HTTP Basic Auth.
