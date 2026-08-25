# Database

This folder defines provider-independent database access for MST modules.

## Main Types

- `MstDbAccessor` is the runtime registry for typed database accessors.
- `DatabaseAccessorFactory` is the Unity component base used by provider bridges to create
  and register concrete accessors.
- `IDatabaseAccessor` is the disposable base contract for all module-specific accessors.
- `DatabaseEntriesInfo<T>` is a paging/search result wrapper used by web/admin endpoints.

## Integration Flow

1. A provider bridge creates concrete accessors for auth, profiles, analytics or other modules.
2. The bridge registers those accessors in `Mst.Server.DbAccessors`.
3. Modules request the accessor type they need and stay unaware of the provider.

## Inspector Configuration

`DatabaseAccessorFactory.Log Level` controls only diagnostics produced while that provider factory
creates and registers accessors. Individual accessors or owning modules may use separate log levels.

## Rules

- Keep provider code out of this folder.
- Accessors that hold connections, sessions, cursors or file handles must implement cleanup in `Dispose`.
- New module persistence should expose a narrow module-specific accessor interface next to that module.
- Do not let database models reference project-only gameplay classes from MST core.
