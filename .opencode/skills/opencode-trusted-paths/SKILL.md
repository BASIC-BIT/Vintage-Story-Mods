---
name: opencode-trusted-paths
description: Configure OpenCode permissions to safely access external Vintage Story folders (vs_source, VintagestoryData, multi-profile installs).
compatibility: opencode
metadata:
  audience: maintainers
  domain: opencode
---

## Goal
Make it easy for OpenCode to read (and optionally search) trusted folders *outside* the repo, such as:
- Decompiled VS sources (`vs_source`)
- Local Vintage Story data (`VintagestoryData`) for logs/configs/saves
- Multi-profile data folders
- Game install folder (DLLs/assets)

OpenCode gates this via the `external_directory` permission.

## Why this matters
- Many VS dev artifacts live outside the git worktree.
- Without `external_directory` allow-rules, OpenCode will prompt on every external read/list/grep.
- We want reads/searches to be frictionless while keeping edits outside the repo safe.

## Recommended approach
1. Allow external directories for reads/search.
2. Deny edits outside the repo by default.
3. Keep the allowlist tight.

## Where to configure
Prefer a *global* config (per developer machine):
- `~/.config/opencode/opencode.json`

We avoid committing machine-specific absolute paths into the repo.

## Minimal example (safe defaults)
Add to your global `~/.config/opencode/opencode.json`:

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "permission": {
    "external_directory": {
      // Vintage Story data path (Windows): %APPDATA%\VintagestoryData
      // On other OSes, ApplicationData resolves differently; adjust as needed.
      "~/AppData/Roaming/VintagestoryData/**": "allow",

      // Decompiled source next to your workspace (example; adjust to your setup)
      // "D:/bench/vs/vs_source/**": "allow"
    },

    // Avoid accidental edits outside the repo.
    "edit": {
      "~/AppData/Roaming/VintagestoryData/**": "deny"
    }
  }
}
```

## Paths worth allowing (common for this repo)
- VS decompilation: `../vs_source` (your absolute path)
- VS data: `%APPDATA%\VintagestoryData`
- Additional profile data: `D:\Games\VSProfiles\Profile2` (or your equivalent)
- Game install: whatever `VINTAGE_STORY` points at (DLLs live here)

## Tip: keep worktree clean
When you need logs/config from external directories, consider copying them into repo-local `logs/` (ignored) for easy sharing.
