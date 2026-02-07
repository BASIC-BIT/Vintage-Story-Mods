---
name: opencode-troubleshooting
description: Troubleshoot OpenCode setup issues (missing tools/skills, config precedence, external_directory prompts, and MCP connectivity).
compatibility: opencode
metadata:
  audience: maintainers
  domain: opencode
---

## When tools/skills "don't show up"
Common causes:
- OpenCode needs a restart to discover new files under `.opencode/`.
- You're running OpenCode from a different directory (so a different `.opencode/` tree is active).
- You're attached to an already-running backend server (`opencode attach`) that was started before the change.
- `OPENCODE_CONFIG_DIR` points to a custom config directory (overrides project `.opencode`).

## Verify the active project
- Start from the repo root:
  - `opencode D:\bench\vs\Vintage-Story-Mods`

## Verify MCP servers
- `opencode mcp list`

## Debug logs
Run with logging:
- `opencode --print-logs --log-level DEBUG`

## Custom tools (.opencode/tools)
Custom tools are loaded at startup.
If a tool fails to load (TypeScript error, missing module), debug logs will show it.

## Permission gotchas
- If you set `permission: "ask"` globally, you might not notice tools exist because you keep rejecting.
- Use patterns:
  - allow read-only tools automatically
  - require ask for destructive tools
