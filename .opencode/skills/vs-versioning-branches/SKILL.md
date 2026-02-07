---
name: vs-versioning-branches
description: Manage multiple Vintage Story versions: branches, DLL dependencies, vs_source decompilations, and cherry-pick strategy.
compatibility: opencode
metadata:
  audience: maintainers
  domain: versioning
---

## Goal
Support multiple Vintage Story major/minor versions safely without breaking existing users.

## Branching model
- Keep incompatible targets on separate long-lived branches.
  - Example: `compat/vs1.20-dotnet7` vs `master` for newer VS.
- Prefer cherry-picking fixes between branches over conditional hacks.

## DLL dependencies
- Local builds use `VINTAGE_STORY` env var to reference game DLLs.
- CI uses `vs-build-dependencies` (proprietary binaries are not committed here).
- When VS updates, refresh dependencies deliberately and update CI version pins together.

## Decompiled sources (`vs_source`)
- This repo commonly uses `../vs_source` for API reference.
- For multi-version support, consider separate folders per target, e.g.:
  - `../vs_source_1_20`
  - `../vs_source_1_21`

Keep the workspace config explicit so the right sources are visible for the active branch.

## Release safety
- New features should be config-guarded where feasible.
- Do not renumber or reuse ProtoBuf `[ProtoMember(n)]` ids.
