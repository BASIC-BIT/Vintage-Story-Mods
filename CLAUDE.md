# CLAUDE.md

Claude Code: this repository's primary agent guidance lives in [AGENTS.md](AGENTS.md). Read that first — autonomy rules, sensitive-action gates, build commands, and the toolbox pointer all apply to Claude Code identically to OpenCode/Codex.

## Skills (Claude Code parity with OpenCode)

The `basics-agentic-dogfooding` toolbox skills (under `D:\bench\basics-agentic-dogfooding\.opencode\skills\`) are mirrored into `~/.claude/skills/` via directory junctions, so they appear as Claude Code skills automatically. Same `SKILL.md` content; OpenCode-specific tool references (`autocontinue_*`, `vsdev_*`, `ptero_*`, `task` subagent calls) should be interpreted as conceptual workflow steps and translated to the closest Claude Code equivalent:

- OpenCode `task` / subagent → Claude Code `Agent` tool (subagent_type: Explore / Plan / general-purpose)
- OpenCode `todowrite` → Claude Code `TodoWrite` tool
- OpenCode `webfetch` / `websearch` → Claude Code `WebFetch` / `WebSearch`
- OpenCode `playwright_*` MCP → Claude Code `mcp__Claude_in_Chrome__*` (no Playwright MCP installed)
- OpenCode `windows_*` MCP → Claude Code `mcp__Windows-MCP__*`
- OpenCode autocontinue / supervisor loop / inbox → not present in Claude Code; treat those skills as OpenCode-only operator runbooks
- OpenCode `vsdev_*` deterministic helper → use `Bash`/`PowerShell` with the equivalent script under `mods-dll\thebasics\scripts\`
- OpenCode `ptero_*` deterministic helper → use the human-qa skill's documented Pterodactyl Client API workflow

If you re-link skills (e.g. after adding a new one in the toolbox), run from PowerShell:

```powershell
$src = 'D:\bench\basics-agentic-dogfooding\.opencode\skills'
$dst = "$env:USERPROFILE\.claude\skills"
Get-ChildItem $src -Directory | ForEach-Object {
    $linkPath = Join-Path $dst $_.Name
    if (-not (Test-Path $linkPath)) { cmd /c mklink /J "$linkPath" "$($_.FullName)" | Out-Null }
}
```

The `unity-mcp-orchestrator` toolbox skill is intentionally not linked because Claude Code already ships an equivalent `unity-mcp-skill`.

## MCP parity gaps

OpenCode's global `opencode.json` configures more MCP servers than Claude Code currently has. Claude Code already provides equivalents for the most useful ones (Context7, Windows-MCP, vrchat, chronote, notion, Claude_in_Chrome ≈ playwright). Genuine gaps that have not been ported: `playwright`, `brave-search`, `greptile`, `aws-*`, `blender-mcp`, `unity-mcp` server, `google-developer-knowledge`, `daytona`, `godaddy-dns`, `irc-admin`. Add via the `update-config` skill if/when needed; do not install reflexively.

## Improvement log

The toolbox publishes a shared change feed at `D:\bench\basics-agentic-dogfooding\docs\agentic\agentic-improvement-log.md`. From any sibling repo:

```powershell
node D:\bench\basics-agentic-dogfooding\scripts\improvement-log-delta.mjs
node D:\bench\basics-agentic-dogfooding\scripts\improvement-log-delta.mjs --mark-read
```

Skim unread items at the start of a session before coding in a target repo.
