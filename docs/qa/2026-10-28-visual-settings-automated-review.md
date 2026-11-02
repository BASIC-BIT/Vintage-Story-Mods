# Visual settings review and automated checks

The accumulated settings work has been reviewed for save safety, permissions, GUI behavior, scene persistence and screenshot evidence. Human QA is prepared in the [18-card plan](2026-10-28-visual-settings-human-qa.md); no human cards have been executed. VS launches, server operations and deployment remain paused.

## Review findings addressed

- Compact Languages could place validation and Remove/Discard controls in the shell's returned save-error area. The compact form now reserves separate space for validation, actions and status; Details and grants have regression coverage.
- Screenshot comparisons treated identical repository assets in different checkout directories as different dependencies. Comparisons now use the known mod-asset-relative identity and content hash, preserve game/unknown paths, and reject duplicate logical identities.

The reviewed server paths retain root authorization, appended protobuf fields, cloned validation before persistence, all-or-none template conflicts and stored-answer preservation. No additional concrete blocker was found in scene-color authority, persistence, renderer lifecycle or analytics emissions. This is source-review evidence, not live transport or GPU proof.

## Automated verification

| Check | Result |
|---|---|
| Complete native-enabled run before stale-check corrections | 2,009 passed, 8 failed, 0 skipped |
| Final native retest, all eight failures plus compact/error and capture guards | 26 passed, 0 failed, 0 skipped |
| Complete non-visual suite on final source | 1,558 passed, 0 failed, 183 visual cases intentionally skipped |
| Screenshot comparison reporter | 20 passed |
| Agent tooling validation | Passed |
| Local-only package | Built, archived, source stamp and ZIP hash verified |

All eight native failures were stale test expectations and pass after correction. The entire 15-minute native suite was not repeated a third time; the final retest covers every failed case and the affected capture checks. Other native cases passed in the complete run. The newly added compact-language status tests also pass. Counts overlap and should not be added together.

Failed exploratory runs are retained rather than counted as passes.

The full native run exposed stale test assumptions: catalog count, clipped scroll content, an outside-click target covered by the expanded picker, and review text now rendered as rich text. Corrections retain the original navigation, content, units and input-isolation requirements. The viewport check continues to require unclipped controls and clip rectangles to fit the screen.

## Human QA entry

Start with Batch A's draft-only checks after explicit approval to resume clients. Batch B uses disposable live data to verify saves, retained answers, locks, permission rejection and conflict recovery. Batch C reserves one approved restart for activation and invitation persistence. Record concrete observations per card and obtain explicit completion approval. A passing automated run or screenshot does not pass a human card.

## Evidence appendix

- Review base: `b22f1c1cf3dbc1234615109fe64324b3a1ea4199`, compared with the working tree, including new files.
- Package and test evidence: [release-review-20261028](C:/Users/steve/.codex/visualizations/2026/10/02/01a0fa70-8ca5-7321-ac1f-07fe824f5be2/release-review-20261028).
- [Verification summary](C:/Users/steve/.codex/visualizations/2026/10/02/01a0fa70-8ca5-7321-ac1f-07fe824f5be2/release-review-20261028/verification.json) records counts, scope and the exact package hash. The local build receipt and source-tree hash identify the package. Nothing was uploaded or installed into game profiles.
- The initial all-pages native run exceeded a 60-second watchdog. Subsequent broad runs use a 300-second allowance per test and retain terminal summaries.
