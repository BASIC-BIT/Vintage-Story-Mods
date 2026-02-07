---
name: skill-authoring
description: Create, validate, and maintain OpenCode skills (SKILL.md) for this repo.
compatibility: opencode
metadata:
  audience: maintainers
  repo: vintage-story-mods
---

## Purpose
This skill describes how to create "skills" for OpenCode in a durable, repo-local way, and how to keep them safe for a public OSS project.

## What a skill is (in OpenCode)
- A skill is a folder containing a `SKILL.md` file.
- Skills are discovered and loaded on-demand via the native `skill` tool.
- Skills should be stable, reusable playbooks (not one-off notes).

## Where skills live
Project-local skills for this repo should be placed at:
- `.opencode/skills/<skill-name>/SKILL.md`

OpenCode also supports Claude/agents-compatible skill locations, but we prefer `.opencode/skills/` for repo clarity.

## File requirements
- File name must be exactly `SKILL.md` (all caps).
- Must start with YAML frontmatter.
- Frontmatter must include:
  - `name` (required)
  - `description` (required)

Only a few frontmatter fields are recognized by OpenCode. Unknown fields are ignored.

## Naming rules
`name` must:
- Be lowercase alphanumeric with single hyphen separators
- Match the containing directory name
- Be 1-64 characters

Regex: `^[a-z0-9]+(-[a-z0-9]+)*$`

## Content guidelines (ship-safe)
- Prefer checklists, commands, and decision points over long prose.
- Make safety/rollback explicit.
- Avoid secrets:
  - Never paste tokens/passwords into a skill.
  - Document variable names and where they should be set instead.
- Prefer progressive disclosure:
  - A short "Quick path" for common usage.
  - A "Deep dive" section only when needed.

## Updating skills
- Skills are part of the repo history. Treat changes as documentation changes.
- Keep skills accurate for the branch they ship on (this repo uses compatibility branches).

## Verification
After adding/editing skills:
1. Ensure the file exists at `.opencode/skills/<name>/SKILL.md`.
2. Ensure `name` matches the folder name.
3. Restart OpenCode (skills are typically discovered at startup).
4. Use the `skill` tool to list and load the skill.

## When to create a new skill
- A workflow repeats more than once.
- A workflow has sharp edges (deployments, releases, UI automation, credentials handling).
- A workflow depends on tribal knowledge (Vintage Story versions, multi-install, ModDB quirks).
