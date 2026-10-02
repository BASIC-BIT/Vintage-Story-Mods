# Agent Control

Agent Control is an explicitly enabled, owner-controlled client mod for deterministic local automation. It exposes six JSON RPC methods over a Windows current-user named pipe:

`hello`, `observe`, `execute`, `cancel`, `extensions.list`, and `shutdownSession`.

It is tested against Vintage Story 1.22.7. It does not expose arbitrary C#, shell commands, filesystem access, an HTTP listener, screenshots, UI crawling, clicking, or pathfinding.

## Safety boundary

The controller starts disabled. Press `Ctrl+Alt+F8` in game to create a session and show the persistent HUD. Press `Ctrl+Alt+F9` to cancel the active execution and release every control asserted by Agent Control. Toggling it off also revokes the in-memory session secret and stops the pipe.

The pipe uses Windows current-user access and each non-hello request requires the random session secret returned by `hello`. The CLI keeps this secret in memory and redacts it from output. Mutations are granted per enabled session by `GrantMutationOnEnable`; sending chat or a command is written to the Vintage Story audit log with redacted content by default.

## Extensions

Client mods reference `agentcontrol.abstractions.dll`, depend on `agentcontrol`, retrieve `IAgentExtensionRegistry` from `api.ObjectCache["agentcontrol:registry"]`, and register a synchronous delegate. Registration and execution happen on the game thread. Restart the client after changing extension assemblies.

See `agentcontrol.sample` for a complete `selection.describe` operation.
