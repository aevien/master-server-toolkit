# Ping Module

This module provides a minimal request/response health and latency check.

## Main Types

- `PingModule` is the authoritative server module.

## Request Surface

The module handles `MstOpCodes.Ping` and responds immediately to the requesting peer.

## Inspector Configuration

`Pong Message` is returned in full for every request. Keep it short and use another authenticated
diagnostic endpoint for detailed health information.

## Rules

- Keep ping free of database, auth and gameplay dependencies.
- Do not add expensive diagnostics to ping handlers.
- Use separate admin/debug endpoints for detailed health checks.
