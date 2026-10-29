# Configuration editor PR review

[PR #256](https://github.com/BASIC-BIT/Vintage-Story-Mods/pull/256) contains the visual editor and its previously uncommitted preview, language, color, character-sheet and scene-marker dependencies. Human QA remains pending. Profile2 and the test server have not been changed during this review.

## Source checkpoint

The source checkpoint includes the 49 tracked and 76 non-cache untracked files identified by the inclusion audit. Python bytecode caches were excluded. The earlier local package and 256 standalone captures remain historical evidence for their recorded source identity, not a package of the latest PR head. Rebuild and verify a package before any approved staging.

## Initial review corrections

- The native screenshot collector now accepts scenes from the production registry while retaining its 26 default scenes and 32-job limit.
- The three wall-occlusion scenes now use the production 8.5-second pose. All other explicit timing branches were compared with production.
- The analytics relay accepts the 13 added page IDs and six sheet actions. Its contract revision and the producer's required revision are both 11. Consent and arbitrary-content rejection remain unchanged.

The relay must support revision 11 before a release of this producer. No relay deployment or deployed revision is verified here.

## Verification status

The tooling check, 20 screenshot-report tests, two workflow trust-boundary tests, 118 visual-workflow assertions and 29 analytics relay tests pass. Independent scoped reviews accepted the capture and relay corrections.

A fresh full native-enabled regression was started from the source checkpoint before the revision-only producer change. Its result and a subsequent focused Analytics retest are pending. CI and Claude review must finish for the final pushed head. The GitHub Codex reviewer could not run because the account's code-review quota was exhausted; that is unavailable review evidence, not approval.

## Remaining gates

Inspect all checks, comments, inline threads, formal reviews and mutable review summaries for the final pushed head after the repository's 30-minute readiness window. Address valid findings and refresh those surfaces again before reporting code readiness.

The [human QA cards](2026-10-28-config-retirement-human-qa.md) remain pending. Default `/tb config` routing, legacy-window removal and compatibility decisions remain separate follow-ups. No merge or auto-merge is authorized by this report.
