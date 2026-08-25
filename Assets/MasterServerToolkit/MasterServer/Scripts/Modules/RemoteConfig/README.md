# Remote Config Module

`RemoteConfigModule` is a small server-side key/value store exposed to clients and room processes.
It is not a full configuration distribution system and should not carry secrets to untrusted clients.

## Key Files

- `RemoteConfigModule.cs` - server-side config store and `GetRemoteConfig` handler.
- `RemoteConfigModuleClient.cs` - client/server facade for downloading remote values.
- `RemoteConfigModuleServer.cs` - server facade variant.

## Current Defaults

On initialization the module exposes:

- `Mst.Args.Names.AdminId` from `Mst.Args.AdminId`;
- `Mst.Args.Names.AdminUsername` from `Mst.Args.AdminUsername`.

Call `Set` and `Remove` from another server module or bootstrap code to expose additional values.

## Rules

- Do not expose passwords, private keys, database strings or service secrets.
- Treat received values as hints/config, not authority.
- Use stable MST argument names when the remote value mirrors a command-line/config value.
