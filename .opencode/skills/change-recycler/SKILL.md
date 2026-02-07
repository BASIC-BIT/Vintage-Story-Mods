---
name: change-recycler
description: Apply review feedback safely (review -> decide -> patch -> re-check)
compatibility: opencode
metadata:
  audience: maintainers
  domain: quality
---

## Purpose

Turn cold-context review feedback into safe, incremental fixes without starting a debate in the main thread.

The recycler is the bridge between:

- a reviewer agent (fresh eyes)
- the working tree (concrete changes)

## When To Use

- After receiving a review (human or AI)
- When feedback is high-signal but scattered
- When you want to keep forward momentum ("report, then continue")

## Inputs

- Review notes (bullets)
- `git status` / `git diff`
- Any relevant logs if the change is ops-related

## Recycler Algorithm

1) Classify each feedback item:

- `must-fix`: correctness, crashes, security, destructive ops footguns
- `should-fix`: clarity, determinism, ergonomics, docs drift
- `nice-to-have`: polish
- `reject`: incorrect or out-of-scope

2) For accepted items:

- implement the smallest safe patch
- keep changes localized
- preserve backwards compatibility (no ProtoMember renumbering)

3) Verify:

- build/package if code changed
- sanity-check docs/examples if docs changed

4) Persist:

- commit in small chunks
- update skills/docs if the fix encodes a new rule

5) Re-check:

- optionally re-run the reviewer on the updated diff

## Output Format

- One short report:
  - what was fixed
  - what was rejected (and why)
  - what remains open
