---
name: mcp-authoring
description: Add or create MCP servers for OpenCode (local/remote), with safe defaults for Vintage Story dev.
compatibility: opencode
metadata:
  audience: maintainers
  domain: mcp
---

## Goal
Use MCP servers to give OpenCode new tools (server panels, log access, web automation, etc.) while keeping context and security under control.

## Add an MCP server (config)
MCP servers are configured in OpenCode config under `mcp`.

Example (local MCP server):
```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "my-local": {
      "type": "local",
      "command": ["npx", "-y", "my-mcp-command"],
      "enabled": true,
      "environment": {
        "MY_ENV": "value"
      }
    }
  }
}
```

Example (remote MCP server):
```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "my-remote": {
      "type": "remote",
      "url": "https://my-mcp-server.com/mcp",
      "enabled": true,
      "headers": {
        "Authorization": "Bearer {env:MY_API_KEY}"
      }
    }
  }
}
```

## OAuth
OpenCode can handle OAuth for remote MCP servers.
You can authenticate via:
- `opencode mcp auth <server-name>`

## Permissions (recommended)
Default to requiring approval for new MCP tools:
```jsonc
{
  "permission": {
    "my-mcp_*": "ask"
  }
}
```

## Vintage Story MCP ideas (MVP)
A "BasicVintageStoryMCP" could provide read-only primitives:
- list log files for a given profile
- read last N lines from a log
- search logs for a regex
- list installed mods

Then (opt-in/destructive) tools:
- upload mod zip to server
- restart server via panel API

## Safety
- Never store tokens in the repo.
- Keep the tool surface area minimal.
- Avoid tools that dump huge outputs into context.
