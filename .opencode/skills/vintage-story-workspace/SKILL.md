---
name: vintage-story-workspace
description: Organize the local Vintage Story modding workspace, refresh decompiled Vintage Story source, and keep first-party work, external mods, and official game source separated.
compatibility: opencode
metadata:
  audience: maintainers
  domain: workspace
---

## Goal

Keep Vintage Story development work repeatable by using a predictable workspace layout and a single scripted path for official game source downloads and decompilation.

## Workspace Layout

Default workspace root discovery:

- `VS_WORKSPACE_ROOT`, when set.
- Existing maintainer workspace roots such as `D:\bench\vs` or `C:\bench\vs`.
- The parent directory of the active repository, or the parent of `work\` when the repository is a worktree under `work\`.

Use `-WorkspaceRoot` or `VS_WORKSPACE_ROOT` when working somewhere else.

```text
<workspace-root>\
  external-mods\
    archives\        Downloaded third-party mod zip files.
    extracted\       Unpacked third-party mods.
    source-repos\    Cloned third-party mod repositories.
  source\
    vintagestory\
      <version>\
        downloads\   Official VS package downloads.
        bin\         Extracted official VS binaries/assets.
        decompiled\  ilspycmd project output for selected VS assemblies.
      legacy-unversioned\
        decompiled\  Old unversioned decompile output retained for reference.
  work\              Clean worktrees and additional working clones.
  Vintage-Story-Mods\ Active local repository/worktree.
```

## Source Refresh Workflow

Use `scripts\Update-VintageStorySource.ps1` from this repository instead of manual downloads or ad hoc decompilation.

```powershell
# Latest stable Windows server package
.\scripts\Update-VintageStorySource.ps1

# Specific version
.\scripts\Update-VintageStorySource.ps1 -Version 1.22.0

# Recreate extracted/decompiled output
.\scripts\Update-VintageStorySource.ps1 -Version 1.22.0 -ForceExtract -ForceDecompile
```

The script reads `https://api.vintagestory.at/stable.json`, downloads the `windowsserver` package, verifies MD5, extracts it, and decompiles the key VS assemblies with `ilspycmd`.

## Git Safety

- Check `git status --short --branch` before pulling, moving repositories, or creating worktrees.
- If the active repo is dirty and current remote code is needed, prefer `git fetch` plus `git worktree add` under `work\`.
- Do not move or rewrite dirty repositories unless the user explicitly confirms.
- Do not commit downloaded packages, extracted binaries, or decompiled VS source into mod repositories.

## External Mods

- Put downloaded third-party archives in `external-mods\archives`.
- Put unpacked third-party packages in `external-mods\extracted`.
- Put cloned third-party source repositories in `external-mods\source-repos`.
- Keep external mod experiments out of the main `Vintage-Story-Mods` repository unless intentionally vendoring code.
