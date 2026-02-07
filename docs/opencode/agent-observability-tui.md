# Agent Observability TUI (Idea Notes)

Goal: an operator-friendly terminal UI for managing *multiple concurrent agent threads* with good attention ergonomics.

This is a "dogfooding accelerant": it increases throughput by making parallel agent work easy to monitor and intervene in.

## Operator experience (ideal)

- Multiple "main" agents visible as tabs/panes.
- The operator can switch between agents quickly (arrow keys / tab switcher).

Attention signals:

- Request attention: flashes/highlights tab (non-invasive).
- Demand attention: can optionally auto-switch focus (invasive; must be restricted).

Policy:

- Configure which agent types are allowed to demand attention.
- Default everything to "request attention".
- Demand attention should be reserved for:
  - destructive operations about to run
  - needing credentials
  - needing in-game verification

## Why this likely belongs in OpenCode

- OpenCode already has:
  - sessions
  - events (`session.status`, `session.idle`, etc.)
  - a TUI
  - a plugin system + SDK

The cleanest implementation is probably a first-class OpenCode feature.
The next best is a plugin that adds a "control plane" panel/overlay.

## Incremental plan

Level 0: "Poor man's observability"

- Use `todo.updated` and `session.status` to log concise progress.
- Use a loop driver plugin to continue work (already started).

Level 1: Session dashboard

- A plugin writes a local JSON state file with:
  - active session IDs
  - titles
  - last activity time
  - owner tag
- A separate TUI command reads and displays it.

Level 2: True multi-session TUI

- Fork OpenCode and add a multi-session tab UI.
- Add an "attention" API that plugins can call.

## Constraints / Risks

- Too much automation without attention ergonomics turns into silent failure.
- Auto-focus/demand attention should be rate-limited.
- Avoid storing secrets in any observability state.
