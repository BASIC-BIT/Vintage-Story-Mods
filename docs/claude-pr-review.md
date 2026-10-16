# Claude pull request review

This branch prepares the shared GitHub App reviewer and retires the repository's
legacy publisher. Source preparation is not live acceptance. Merge/activation
requires the separately approved pilot and single-publisher cutover below.

## Repository policy

The live default branch owns `.github/claude-review.json` and `REVIEW.md`.
The App reads those files and explicit `instructionPaths` at the captured base
SHA. PR edits cannot weaken their own review. Missing/invalid policy fails
closed. REVIEW.md retains this repository's calibration.

- Base: `main`; open, same-repository, non-draft PRs only.
- Skip label: `skip-claude-review`; unskip-triggered review: `true`.
- Bot authors: `false`; allowed bot triggering actors: `["*"]`.
- Additional trusted instruction files: `[]`.
- No unconditional changed-file exclusions. Docs-only exclusion: `false`.

`allowlist` with `contextAllowGlobs: ["**"]` retains repository source navigation as inert PR-head data. Where the old job exposed a base checkout, this deliberately replaces stale-base navigation with captured current-head context. Files remain data, including proposed instructions; they never become executable code or trusted policy.

Preserve world persistence, synchronization, client/server and game-version calibration in REVIEW.md. The prior isolated PR-head navigation remains available as inert allowlisted data. Do not run the gameplay build-and-package script to validate reviewer configuration: it also uploads to the test server. Gameplay QA/release approval remains separate.

## Results and controls

The App publishes a COMMENT review with validated inline findings, one mutable
sticky summary and an exact-head coverage check. It never approves or merges.
Partial coverage, failures, missing context and old-base comparisons are
explicit; a green job or prior prose alone is not completion. Compatible
structured scope results may be reused only with current integration.

New eligible heads supersede stale work. Skip, draft, close, disable, retarget
and installation revocation cancel work and suppress stale publication.
Unrelated labels do not start inference. Base-only pushes invalidate existing
comparisons without inference or new comments on never-reviewed PRs.

A human with current write, maintain or admin permission may comment exactly
`/claude-review rerun`, or rerequest the App's current-head check. Bot actor
allowlists do not relax command permission checks or bot-author policy. The
App may resolve only its own verified-fixed finding threads without human
replies; human discussions and other reviewers remain with the implementer.

The shared service uses the canonical Claude Code subscription through its
isolated worker. No API-key fallback or repository credential copy is added.
PR code, hooks, project commands and head-owned instructions never execute.

## Cutover and rollback

1. Obtain exact target, App/access, infrastructure and inference/publication
   approval. Establish trusted policy on the real default branch through an
   approved baseline procedure while legacy remains the sole publisher.
2. Prove App reads, COMMENT/inline/sticky/check publication, actual thread
   permissions and finite shadow behavior. Record exact engine/runtime,
   repository/install, head/base/policy and coverage identities. Missing proof
   remains UNKNOWN. Keep service execution/publication durably paused.
3. For the approved target, stop/drain App work, disable the recorded legacy
   workflows, cancel queued/in-flight legacy jobs and verify terminal status.
   Only then enable App publication and prove one inference owner/publisher.
4. Verify new push, labels, skip/unskip, draft/ready, retarget, close/reopen,
   base invalidation, authorized rerun, publication failure and revocation on
   the approved disposable acceptance PR. Preserve branch protection and
   required product checks; workflow removal is not review evidence.

Rollback pauses App execution/publication and stops its in-flight attempts,
restores the recorded prior workflows, then proves only the legacy owner is
active. Never enable both publishers. Engine rollback alone does not restore
retired workflows. Private exact source identities and live evidence belong in
the basic-infra migration inventory.

## Delivery and verification

Validate JSON with the deployed engine schema, inspect calibration and context
scope against the prior trusted policy, parse/lint remaining workflow YAML,
run applicable repository checks and `git diff --check`. Live label behavior,
App permissions, credential-account route and end-to-end publication require
separate acceptance; local tests do not prove them.

Before a follow-up push, reply to and resolve handled review threads. Before
calling a PR merge-ready, keep the latest head pushed for at least 30 minutes,
then refresh checks, ordinary comments, inline threads, formal reviews, mutable
sticky summaries and mergeability. Every new commit restarts that window.
Preserve repository-specific reviewer, owner-approval and release gates.

Retired paths in this change:

- `.github/workflows/claude-review.yml`
- `.github/workflows/claude-review-control.yml`
- `.github/workflows/claude-review-reconcile.yml`
