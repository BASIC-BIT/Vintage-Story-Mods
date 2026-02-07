# OpenCode In This Repo

This repo is set up to work well with OpenCode (agentic coding + tools).

Key files:
- Repo rules: `AGENTS.md`
- Repo skills: `.opencode/skills/*/SKILL.md`

Optional local context:
- `AGENTS.local.md` (gitignored)
- Template: `AGENTS.local.md.example`

## Skills
OpenCode discovers skills under `.opencode/skills/<skill-name>/SKILL.md`.

After adding or changing skills, restart OpenCode so it can re-discover them.

## External folders (Vintage Story dev)
Vintage Story development often needs reading logs/configs/saves outside the git worktree.
OpenCode controls this via `permission.external_directory`.

We recommend configuring trusted external directories in your global OpenCode config:
- `~/.config/opencode/opencode.json`

See:
- `.opencode/skills/opencode-trusted-paths/SKILL.md`

## MCP servers and plugins
OpenCode can be extended via:
- MCP servers: https://opencode.ai/docs/mcp-servers/
- Plugins: https://opencode.ai/docs/plugins/
- Custom tools: https://opencode.ai/docs/custom-tools/

For this repo, prefer:
- starting with skills + scripts
- then graduating to MCP/tools when a workflow is stable and frequently repeated

## Repo-local custom tools
This repo includes a small set of OpenCode custom tools under `.opencode/tools/`.

Currently:
- `vsdev_*` tools help discover Vintage Story profiles and browse logs.

Troubleshooting:
- If you don't see new tools/skills, fully restart OpenCode.
- If you use `opencode attach`, restart the backend server too.
- Check config precedence and env vars (notably `OPENCODE_CONFIG_DIR`).
- See: `.opencode/skills/opencode-troubleshooting/SKILL.md`
