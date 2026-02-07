---
name: dotnet-quality
description: Improve code quality for this C# repo safely (formatting, analyzers, and build checks) without risky refactors.
compatibility: opencode
metadata:
  audience: maintainers
  domain: dotnet
---

## Goal
Increase readability and prevent regressions with low-risk, incremental tooling.

## Baseline checks
- `dotnet build` (Release) should stay green.
- Treat client crashes as release blockers.

## Formatting
Preferred approach:
- Add/maintain a `.editorconfig` with minimal, non-controversial defaults.
- Run formatting only when explicitly desired (formatting can produce noisy diffs).

Tooling:
- `dotnet format` (optional) for consistent formatting.

## Analyzers
Incremental approach:
- Enable analyzers gradually; do not turn on "treat warnings as errors" globally until the repo is clean.
- Prefer targeted rulesets per project if needed.

## CI hygiene
- Keep build scripts deterministic.
- Gate destructive ops (deploy/upload) behind explicit flags.
