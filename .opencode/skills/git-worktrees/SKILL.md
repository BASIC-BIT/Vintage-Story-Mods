---
name: git-worktrees
description: Use git worktrees for parallel mod development across branches and Vintage Story version targets.
compatibility: opencode
metadata:
  audience: maintainers
  domain: git
---

## Why worktrees
- Keep multiple branches checked out simultaneously (e.g., `master` and `compat/vs1.20-dotnet7`).
- Enable parallel agent work without stomping on the same working directory.
- Reduce context churn: each worktree can focus on one feature.

## Core commands
From the main repo directory:

List worktrees:
```bash
git worktree list
```

Add a worktree for an existing branch:
```bash
git worktree add ../ws-compat-vs120 compat/vs1.20-dotnet7
```

Create a new branch + worktree:
```bash
git worktree add -b feat/typing-indicator ../ws-typing-indicator
```

Remove a worktree:
```bash
git worktree remove ../ws-typing-indicator
```

## Safety notes
- Do not remove a worktree if it has uncommitted changes.
- Prefer one feature per worktree.
- Keep naming predictable (`../ws-<branch-or-topic>`).

## Suggested coordination convention
If using shared state (file or Redis), record:
- worktree path
- branch name
- owning agent/session
- status (active/idle)
