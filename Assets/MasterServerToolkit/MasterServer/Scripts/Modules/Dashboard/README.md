# Dashboard Module

This folder implements the built-in admin/dashboard pages on top of the web server module.

## Main Types

- `DashboardModule` extends `HtmlServerModule` and builds the page navbar from child controllers.
- `DashboardPageWebController` is the page-controller base for dashboard routes.
- `Controllers/Pages` contains page controllers.
- `Controllers/Apis` contains JSON/API controllers for users, rooms, spawners, server state and notices.
- `Pages` contains HTML templates used by controllers.
- `Pages/dashboard.html` polls server/users/rooms/spawners summary APIs and surfaces refresh issues without repeating toast messages on every polling interval.
- `Pages/JS/template.js.html` contains shared frontend helpers, including `mst.apiFetch`, toast messages and API error parsing.
- `Pages/JS/users.js.html` contains the Tabulator users table, user-details modal, block/unblock action, block history view and direct user notification action.
- `Pages/notifications.html` contains the broadcast notification form that sends messages to all notification recipients through `/api/v1/notification/all`.
- `Pages/analytics.html` contains analytics lookup tools, loads analytics rows through a server-side Tabulator table and opens event details in a modal on row click.

Dashboard page controllers expose panel/about titles, trusted about HTML, a template-supported CSS icon
class and navbar visibility. Hiding a page from the navbar does not unregister its route.

## API Query Parameters

Dashboard API query parameter names should come from `MstParamKeys`.

Users endpoints expose account block state as Dashboard view-model fields, not as account model fields:

- `PUT /api/v1/users/block?id=...&blockReason=...&blockUntil=...` creates an active account block.
- `DELETE /api/v1/users/block?id=...&removeReason=...` removes the active account block without deleting history.
- `GET /api/v1/users/block/history?id=...&page=0&size=100` returns historical account block records, newest first.
- `PUT /api/v1/users/profile-property?id=...&key=...` updates one existing profile property with JSON body `{ "value": ... }`. Profile editing is rejected while the user is online.
- User detail JSON may include `profileSchema`, an inferred JSON-schema-like description generated from MST observable profile property types. Dashboard profile editing uses this schema to keep existing keys read-only, validate values, allow array item add/remove, and allow key add/remove only for dynamic dictionary-like objects.
- User JSON may include `isBlocked`, `blockedUntil` and `blockReason` from the active block record.

Analytics endpoints use readable query names:

- `/api/v1/analytics/search?page=0&size=25&sortField=timestamp&sortDirection=1&searchValue=...`
- `/api/v1/analytics/search?userId=...&page=0&size=25`
- `/api/v1/analytics/search?id=...&page=0&size=25`
- `/api/v1/analytics/search?key=...&page=0&size=25`
- `/api/v1/analytics/search?timestamp=...&page=0&size=25`
- `/api/v1/analytics/search?startTimestamp=...&endTimestamp=...&page=0&size=25`
- `/api/v1/analytics?page=0&size=1000`
- `/api/v1/analytics/user?userId=...&page=0&size=1000`
- `/api/v1/analytics/id?id=...`
- `/api/v1/analytics/key?key=...&page=0&size=1000`
- `/api/v1/analytics/timestamp?timestamp=...`
- `/api/v1/analytics/timestamp-range?startTimestamp=...&endTimestamp=...&page=0&size=1000`

## API Error Contract

Dashboard API endpoints under `/api/v1` should return JSON errors so frontend pages can show actionable messages instead of generic HTTP statuses.

Expected error shape:

```json
{
  "error": {
    "status": 400,
    "code": "bad_request",
    "message": "Query parameter 'size' must be between 1 and 200"
  }
}
```

Use explicit JSON result types such as `BadRequestJson`, `NotFoundJson`, `UnauthorizedJson` and `InternalServerErrorJson` for Dashboard API validation and not-found responses. Generic web-server result types such as `BadRequest`, `NotFound`, `Unauthorized` and `InternalServerError` still return plain text for backward compatibility outside this Dashboard API contract and can be combined with `HtmlResult` when page routes need an HTML body with an error status.

The frontend reads errors through `mst.getErrorMessage(response, fallback)`. It prefers `error.message` from JSON, then `message`, then a plain-text body, and finally the supplied fallback/status message. Local page handlers should use this helper instead of directly calling `response.json()` on error responses.

## Rules

- Dashboard code must use existing web-server auth checks and admin credentials.
- Do not put database/provider-specific logic into controllers; call module/server abstractions.
- HTML templates are framework admin UI, not game UI.
- Keep API query parameter names aligned with `MstParamKeys`.
- New Dashboard API error responses should follow the JSON error contract above.
- Dashboard tables use Tabulator through shared templates. Do not add a second table framework.
