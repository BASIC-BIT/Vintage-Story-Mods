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

Follow the shared [single-publisher handoff](https://github.com/BASIC-BIT/basic-pr-reviewer/blob/main/docs/operations.md#single-publisher-handoff)
with this repository's policy and retired paths below. Read-only checks and
finite shadow precede approved legacy terminal shutdown; target write proof
follows that shutdown and publication approval. Keep the production ledger and
durable pause until pending intents are explicitly approved or cancelled.
Preserve the local review and required-check gates. The private infrastructure
migration inventory owns exact source identities and target acceptance evidence;
missing live proof remains UNKNOWN.

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
