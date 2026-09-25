# Persistent RP scene markers: recovered checkpoint

Date: 2026-09-07

## Latest local continuation: creator padlocks

BASIC approved keeping padlocks and enforcing creator/admin unlock and pickup protection. This supersedes the earlier suggestion to remove `Lockable`.

- Implementation is on local branch `codex/scene-marker-locking` in this worktree, based on PR #243 head `7beb88b`. The existing PR and both earlier worktrees were left untouched.
- Creator attaches a vanilla padlock with sneak-right-click; reinforcement is not required. This consumes one padlock. Creator/admin unlock with an empty-hand sneak-right-click and recover one padlock, falling back to a world drop when inventory is full.
- Locked descriptions remain readable. All editing, including stale editor save packets, is rejected until unlock. Only the creator or an admin with `controlserver` can break/pick up or unlock a locked marker, with land-claim build access still required.
- Creator identity no longer changes on edits. Existing experimental markers use their recorded author as owner; the earlier implementation did not preserve original creator history, so it cannot be reconstructed. New blank items receive their creator on first placement.
- Creator and lock item code persist through world save and item drops/replacement. Locked markers resist explosions. Vanilla padlock handling for other blocks is unchanged by the marker-scoped Harmony prefix.
- Automated verification: 739 tests passed, 0 failed, 0 skipped. This includes 28 new tests for permission matrices, server handlers, stale editor rejection, no duplicate padlock returns, persistence, and Harmony installation/removal against the installed 1.22.7 assembly. Independent code review found no actionable issue, with actual game interaction still requiring QA.
- Standard build-and-package script succeeded, 0 warnings and 0 errors in its reported release build. The DLL in the test output matches the newly packaged build. Packaged English and handbook JSON parsed successfully.
- New local QA ZIP: `mods-dll/thebasics/thebasics_5_9_1.zip`, SHA-256 `1DD0636DEF52E23FB0A21690E8665498933633AE5E591FD71086B589DDE5B9C5`. A second copy is staged only under `.tmp/scene-marker-lock-qa/Mods/` inside this worktree. No test profiles, test server, or public release were changed. The historical PR package hash below is not this locking candidate.
- Updated `FEATURES.md`, handbook, localization, and `RELEASE_SMOKE_TEST.md` include the new behavior and a concrete padlock QA follow-up. Manual QA remains unperformed.

## Objective and scope

Resume the previously requested roleplay indicator feature without duplicating existing work. The recovered feature is persistent RP scene markers inside The BASICs, associated with the Lunaria RP Description bounty. It is contextual narration anchored to a small world block, with environmental and OOC presentation. It is separate from typing indicators and character switching.

Requirements recovered from the existing implementation and its September 3 handoff: two-loose-stone recipe; ground and wall placement; shift-right-click title/body/kind editor; targeted-only overlay; server-enforced claim access; metadata-preserving break/drop/replacement; held-item read-only book view. No new chat toggle or standalone mod is required.

## Current authoritative engineering state

- PR: https://github.com/BASIC-BIT/Vintage-Story-Mods/pull/243
- Live head: `7beb88b6e0d6efd82fcc606d0b13ddd6d4d05f68`, branch `codex/scene-markers`.
- Live base: `58416856f10cf0729548f6bf46c8e086e95ffe1b`, branch `main`.
- PR is open, unmerged, with clean mergeability. Two commits, 18 changed files.
- Matching clean local implementation: `D:\bench\vs\work\s2-scene-markers`, local branch `wt/scene-markers`.
- Older preserved worktree `D:\bench\vs\work\thebasics-scene-markers` remains at `ececcd9`. Do not restart implementation from that older checkpoint.
- Existing authoring lane was preserved. No other agent session was resumed, no branch was changed, and no runtime state was mutated during recovery. The currently active human/agent owner was not established.

The September 3 basic-life handoff and its owning task still say no PR and rebase required. Those statements are superseded by the live PR. Do not repeat that rebase or create another PR.

## Evidence refreshed during recovery

Read live PR details, all three ordinary comments (including the mutable Claude summary), formal reviews, inline review threads, and all ten check runs. Eight check runs succeeded and two conditional review jobs were skipped. There are no formal reviews or inline threads. Claude's summary explicitly matches the current head and base and reports no blocking findings. Codex review did not run because of usage limits. The coverage comment reports 27% lines and 18.6% branches, decreases from base; the build check nevertheless succeeded.

The PR reports 711 passing tests, a release build with zero errors, clean whitespace/tooling checks, asset parsing, and cold review fixes. These were not rerun during this recovery; live CI success and the preserved artifact were verified instead.

Verified local package:

- `D:\bench\vs\work\s2-scene-markers\mods-dll\thebasics\thebasics_5_9_1.zip`
- 581,716 bytes.
- SHA-256 `86AD482B59C0234C30B5CF9115DBEB0917FA2B8B07C5EDE98B1DC6D220C0E2F7`, identical to the PR's QA candidate.

September 5 Agent Control evidence in basic-life records selection of `thebasics:scene-marker-ground-north` in a connected 1.22.7 client. This is limited runtime presence evidence, not completed scene-marker QA or current server package verification.

## Smallest useful next deliverable

Complete the existing scene-marker QA card against the identified candidate on Vintage Story 1.22.7, then fix observed failures. No additional feature design is necessary to reach that test.

One batch, default production-like config, two players and a claim the second player cannot build in:

1. Craft from two loose stones; place ground and wall markers. Expect small markers and correct orientation.
2. Save titled, multiline environmental narration. Look away and back; have the second player target it. Expect text only while targeted and synchronized contents.
3. Switch to OOC presentation. Expect visibly distinct presentation and readable text without raw markup.
4. Break, inspect, shift-right-click the held written item, and replace. Expect title, body, kind, and author metadata retained with one valid drop and readable book view.
5. Try editing without claim access. Expect refusal and no data change. Record concrete observations and check logs for client/server exceptions.

Cross-reference: `D:\bench\vs\work\s2-scene-markers\mods-dll\thebasics\docs\RELEASE_SMOKE_TEST.md`, Batch 1, card 6. The PR also discloses that padlocks do not gate editing, plain right-click does not invoke inherited sign interaction, and edits are not rate-limited. Preserve those disclosures when assessing QA; claim permissions are the implemented access control.

Repository `AGENTS.md` explicitly requires owner approval before starting manual QA and before marking it complete. The human-qa skill requires presenting the batch plan up front. Recovery authorization was not treated as permission to launch clients, replace test-server packages, or declare QA passed.

Remaining gates: BASIC's QA-start approval; concrete in-game observations and completion approval; any resulting fixes and refreshed checks; separate merge/release authorization. The original bounty poster, current acceptance criteria, and whether the bounty is still open remain unverified. Do not claim bounty delivery or payment eligibility.

## Source pointers and retirement

- `D:\bench\basic-life\research\2026-09-03-thebasics-scene-markers-handoff.md`: recovered requirements and older checkpoint.
- `D:\bench\basic-life\context\business-and-products.md`: owning unfinished bounty task and 1.22.7 contract.
- `D:\bench\basic-life\.cache\mono-thread\2026-09-05-resume\agentcontrol-manual-qa.md`: limited runtime selection evidence.
- Live PR #243: current engineering and review state.

Retire this recovery packet when the owner records current QA results and the authoritative continuation checkpoint. No release or external message was published during this recovery.

## Continuation plan

Planning requested by BASIC on September 7. This section plans work; it does not authorize launching manual QA, merging, or publishing. Execute inline in the existing lane after establishing its current owner and state. The goal is a verified scene-marker candidate that satisfies the recovered requirements on Vintage Story 1.22.7.

### 1. Prepare the exact candidate

- [ ] Refresh PR #243 head, base, checks, and review feedback. Verify the owning worktree is still clean and no other worker is modifying it before making changes. Reuse completed work; do not repeat a rebase when the live base already matches.
- [ ] Recheck the package hash above. Inspect current server/client package identity and coordinate with any active test session before proposing replacement or restart. The September 5 Agent Control session may have shared this environment.
- [x] Resolve the padlock presentation decision. BASIC chose creator-owned padlocks with creator/admin unlock and pickup protection, preserving claim restrictions. Implemented and automatically verified locally; see latest continuation above. The earlier removal proposal is withdrawn.
- [ ] If code/assets change, run `dotnet test .\mods-dll\thebasics.Tests\thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true`, inspect the build/package script's deployment side effects before invoking it, and create an identified replacement QA artifact through the repository build workflow. Run `scripts/check-agent-tooling.ps1` only if its triggering files change. Record the new head and SHA-256; earlier QA cannot silently transfer to a changed candidate.

### 2. Run one focused QA batch after approval

Use two players, default production-like config, ground and wall placement space, and a claim the second player cannot build in. BASIC observes the game; the agent handles authorized setup and captures results. Estimate 15-25 minutes after setup, with a short persistence restart at the end.

| Card | Steps | Expected result / blocking failure |
|---|---|---|
| 1. Craft and place | Craft with two loose stones; place one marker on ground and one on a wall. | Recipe works; marker is small, selectable, and correctly oriented. Missing assets or unusable selection blocks delivery. |
| 2. Author and view | Shift-right-click; save title `Old campsite` and two body lines. Have both players target it, then look away. Reopen the editor. | Matching saved text on both clients; overlay appears only while targeted; editing restores saved values. |
| 3. OOC and text handling | Switch to OOC. Include literal text `<font>test</font>` and an ampersand in the body; inspect overlay, tooltip, and held-item book. Try an empty title and clear the body. | OOC differs visibly from narration; literal markup cannot inject formatting; no parser errors or stale previous text. Empty-content behavior remains usable. |
| 4. Pickup and replacement | Break each placement, inspect the drop, read it in hand, and replace it. | One valid drop; title/body/kind/author retained; held reading works; ground and wall variants retain content. |
| 5. Claims and synchronization | Owner edits in a claim; second player reads and attempts an edit without build permission. Owner then changes the content again. | Unauthorized edit is refused without data changes; authorized edits synchronize. Read access is not incorrectly treated as build access. |
| 6. Durable world state | Leave a written ground and wall marker placed. Perform a coordinated save/restart and client reconnect, then inspect both and repeat pickup once. | Content survives world reload and reconnect; no duplicate markers, missing data, or scene-marker exceptions. |

- [ ] Record concrete observations for each card against the exact head/package, including logs when a failure occurs.
- [ ] If a card fails, fix the smallest responsible path and rerun that card plus affected neighbors. Persistence failures implicate `SceneDescriptionData.cs`, `SceneDescriptionBlockEntity.cs`, and `SceneDescriptionBlock.cs`; display failures implicate `SceneDescriptionRenderer.cs`, `SceneDescriptionFormatter.cs`, and `SceneDescriptionDialog.cs`; placement failures implicate block assets and `SceneDescriptionBlock.cs`. Add an automated regression only where it exercises the actual defect.
- [ ] Obtain BASIC's explicit QA-completion approval after the observations support it. Do not equate a successful build or block-selection receipt with this approval.

### 3. Prepare delivery, then use the existing approval gates

- [ ] Reconcile the original bounty's acceptance criteria and current availability with BASIC. No external outreach is authorized by this plan. Do not invent the poster or promise payment eligibility.
- [ ] Update the existing PR and smoke-test evidence only with authorized GitHub actions. If new commits are pushed, allow the required 30-minute window after the latest commit and refresh all exact-head review/check surfaces afterward.
- [ ] Prepare concise release copy describing only verified capabilities. State the claim-based editing behavior clearly. Keep dice rolling and unrelated chat work outside this change.
- [ ] Present the tested artifact, remaining limitations, and current PR state for separate merge/release approval. Reverify merge and publication only after those actions are authorized and performed.

Completion of this plan means verified feature behavior and an accurately documented delivery candidate. Bounty fulfillment additionally requires the actual recipient's acceptance; it is not inferred from merging code.
