# Fresh review of the configuration editor

Five fresh subagents reviewed PR #256 against main on October 30. Their scopes were server and save correctness, approved requirements, documented standards, shared rendering controls, and capture/deployment tooling. They did not use earlier review reports as their starting point.

## Finding and correction

The runtime review reproduced a language recovery defect. When two languages exchanged names and the save timed out without being applied, recovery incorrectly treated the unchanged server names as saved identities. Retrying lost the rename mapping that keeps players' language knowledge attached to the intended language.

A follow-up regression covered a successful save followed by another administrator's language edit before readback. That snapshot cannot establish whether the submitted rename was saved. Recovery now keeps the save unconfirmed, preserves the draft, and blocks retrying. The editor explains how to discard the draft and reopen current server settings. Exact original and exact saved snapshots still recover normally; newer local edits survive confirmed saves.

The fix is limited to wizard recovery. The analogous legacy language dialog behavior predates this PR and remains a follow-up for that separate editor.

## Review and validation

The standards, requirements, rendering and tooling reviews found no additional actionable issues in their inspected scopes. A second review of the recovery correction found no remaining blocker. Regression coverage includes unapplied swaps, applied swaps with newer local edits, ambiguous concurrent edits, baseline preservation, repeated reopen attempts, blocked retries, and compact recovery-message placement.

The fresh tooling review ran 21 screenshot-report tests, two workflow trust-boundary tests, and 29 relay tests successfully. The final focused native recovery run passed 102 tests with no failures or skips. The canonical local-only package build passed with zero errors and 386 warnings, and formatting verification passed. Final CI results are recorded in the PR and its local evidence folder. These counts overlap earlier validation and are not a combined total.

## Remaining limits

Human QA remains pending. No game launches, server changes, Profile2 refresh, merge or release were performed. Profile2's package and agent configuration hashes were rechecked unchanged.

Initial regression builds omitted the packaging suppression flag and ran the existing auto-copy step into the default Roaming Vintage Story data folder. This was disclosed to the owner. Subsequent builds and tests disable that step. No claim is made that the default profile's previous package was preserved.

Relay revision 11 deployment, default command routing and legacy-menu retirement remain separate gates.
