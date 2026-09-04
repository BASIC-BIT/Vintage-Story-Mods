---
name: moddb-release-playwright
description: Conduct Vintage Story ModDB releases through the AWS-backed broker (session status, prepare, owner-confirmed publish, human-assisted Playwright renewal) and draft owner-reviewed public release copy.
---

# moddb-release-playwright

This is the Codex wrapper for the repo source skill:

- `.opencode/skills/moddb-release-playwright/SKILL.md`

Codex translation notes:

- Read the OpenCode source skill before acting. It holds the broker command grammar, exit codes, release sequence, and the maintainer-only credential commands.
- Read its public release-note reference when drafting, reviewing, or converting GitHub or ModDB release copy.
- The broker in `tools/moddb-release` reads the session from AWS Secrets Manager in-process. Never ask the user for a password or cookie, and never paste one into a command or file.
- Playwright is used only by the broker's renewal command on an approved Windows machine, where the human completes reCAPTCHA in a visible Chrome window. Do not drive the ModDB site with Codex browser tools.
- In Codex cloud, expect `renewal-required` and stop; the cloud path is the manual `ModDB Release` GitHub workflow from `main`.
- Use Codex shell/app tools for commands, and follow `AGENTS.md` plus `AGENTS.local.md` when present.
- Keep this wrapper thin. Put detailed workflow changes in the OpenCode source skill.
