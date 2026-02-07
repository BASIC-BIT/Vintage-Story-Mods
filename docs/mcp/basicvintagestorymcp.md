# BasicVintageStoryMCP (Concept)

Goal: a small MCP server to make Vintage Story mod development more reliable and less manual.

Why:
- Important artifacts live outside the repo (logs, saves, mod configs, multiple profiles).
- Some ops are repetitive (fetch logs, restart server, verify deployed mod version).
- MCP tools can be exposed as structured actions instead of ad-hoc shell/file poking.

## MVP (read-only)
Local:
- List known VS data directories (profiles)
- List logs and crash reports
- Read a log tail (last N lines)
- Regex search across logs

Remote (read-only):
- Fetch latest server logs (SFTP)
- Get server status (panel API)

## Phase 2 (opt-in destructive)
- Upload a mod zip to the server (SFTP)
- Restart server (panel API)
- Rotate/backup saves

## Security posture
- Never store credentials in git.
- Require explicit user approval for destructive tools.
- Avoid returning huge outputs into context; provide summaries + file references.

## Integration with OpenCode
- Configure as a local MCP server in `opencode.json`.
- Use `permission` to make all MCP tools `ask` by default.
