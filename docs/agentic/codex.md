# Codex Notes

Vintage-Story-Mods has historically kept durable agent playbooks in OpenCode skills. Codex should reuse those playbooks through thin wrappers rather than duplicating long workflow text.

## Startup

- Read `AGENTS.md` first, and `AGENTS.local.md` when present.
- Treat `.opencode/skills` as the detailed source of truth for repository workflows.
- Keep secrets out of public prose and committed files. Credential source paths belong in `AGENTS.local.md`, not shared docs.

## Skills

The OpenCode source skills live under `.opencode/skills/<name>/SKILL.md`.

Codex wrappers live under `.codex/skills/<name>/SKILL.md`. Each wrapper keeps Codex-valid frontmatter and points back to the OpenCode source skill. If a Codex session does not auto-discover repo-local skills, open the wrapper or source skill by path.

Current wrappers:

- `human-qa`
- `moddb-release-playwright`
- `rp-culture`
- `vintage-story-ci-dependencies`
- `vintage-story-workspace`

Keep `.opencode/skills` as the detailed source of truth. Keep `.codex/skills` as thin compatibility shims with only `name` and `description` in frontmatter.

## Tool Mapping

- Translate OpenCode-specific wording in source skills to the Codex tools available in the current session.
- For browser verification, use Codex Browser, Chrome, or Playwright tooling when available and appropriate.
- For GitHub and PR review loops, prefer a GitHub connector when available; otherwise use `gh`.
- For library, SDK, CLI, and cloud-service docs, use Codex documentation tools such as Context7 when available, or primary-source docs when required.
- For reminders, monitors, or follow-ups, use Codex automations only when the user asks for that behavior.

## MCPs

No project-scoped OpenCode MCP servers are committed for Vintage-Story-Mods today: there is no repo `opencode.json` MCP inventory to mirror into Codex.

Codex project MCP config lives at `.codex/config.toml`. Leave it without `[mcp_servers.*]` entries until the repository has an explicit repo-scoped MCP server to launch. Generic Codex MCPs such as Playwright, GitHub, Context7, or Node REPL may still be available from global config or plugins; verify the active tool surface before relying on one.

Run `.\scripts\check-agent-tooling.ps1` after changing `.opencode`, `.codex`, or this document.
