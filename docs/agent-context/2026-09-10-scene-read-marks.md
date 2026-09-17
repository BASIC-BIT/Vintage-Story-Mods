# Scene read marks

BASIC asked for per-player "read" marks on scene markers: a read marker shows no floating bubble in any display mode and draws its indicator at half opacity, while plain right-click still opens the full text.

- `SceneDescriptionData.ReadStamp` (unix ms, 0 = never stamped) is the content version. `WriteTo` takes an opt-in `includeReadStamp`, so the block entity's tree persists and syncs it while a picked-up item stack never carries it. `Stamp()` is monotonic per marker, so a same-millisecond edit cannot collide with a stale mark.
- Fresh stamps are issued on every placement (`InitializeFromItem`), on every accepted save, and on the Clear-read packet. Legacy markers with stamp 0 get one on the first mark-read.
- Marks live in per-player mod data (`BASIC_SCENE_READ_MARKS`, `GetSceneReadMarks`/`SetSceneReadMarks`) as `Dictionary<string,long>` keyed `x/y/z/dimension`, capped at 4096 by dropping the smallest (oldest) stamps. `SceneReadMarks` holds the pure cap/compare helpers plus the client cache.
- Packets: block-entity 1004 mark read, 1005 mark unread (no edit permission, server replies to that player with the authoritative stamp as 8 raw bytes), 1006 clear read (same `CanEdit` + 8-block checks as Save, then a fresh stamp and MarkDirty). `SceneReadMarksMessage` is registered last on the shared "thebasics" channel and sent right after the config on client ready.
- UI: `SceneReadonlyBookDialog` subclasses the vanilla reader and adds one toggling button; the editor gets a Clear read button between Cancel and Save, disabled while locked, behind `GuiDialogConfirm`.

Validation: 815 automated tests pass (10 new). Manual QA pending; smoke-test card 6.8 covers two players, relog, save, and clear.

Retire once PR #243 merges and smoke-test card 6.8 has passed once on the test server.

QA package 2026-09-10: commits 101f0f2..d0d8881, thebasics_5_9_1.zip SHA256 fa3160c3b0660368..., deployed to the test server (restart 01:53Z, RunGame reached, no mod errors) and to Profile2/Profile3. Includes the symbol catalog slice and the review fixes.

QA package 2026-09-10 (second): commits eb050f3..80beee3 on top, thebasics_5_9_1.zip SHA256 f876286e0b5a5b01..., deployed to the test server (restart 02:20Z, RunGame reached, no mod errors) and to Profile2/Profile3. Adds the unsaved-changes prompt, preview zoom, Save & Close bottom-right, symbol-row spacing, dampened bobbing when read, and the reader header.

QA package 2026-09-10 (third): commit c24ee38, thebasics_5_9_1.zip SHA256 899aaf5ac87bd6b7..., deployed to the test server (restart 03:22Z, RunGame reached, no mod errors) and to Profile2/Profile3. Bubble now rendered screen-space in the Ortho pass with a line-of-sight gate; inspector no longer repeats the title.

QA package 2026-09-10 (fourth, CI-green head): commit 54b51f7, thebasics_5_9_1.zip SHA256 57f1bae4cae31ec9..., deployed to the test server (restart 05:36Z, RunGame reached, no mod errors) and to Profile2/Profile3. This is the build under manual QA.

QA package 2026-09-10 (fifth): commit 20d1378, thebasics_5_9_1.zip SHA256 fa99b524e0ac2bd8..., deployed to the test server (restart 06:20Z, RunGame reached, no mod errors) and to Profile2/Profile3. Addresses the first manual QA pass: reader closes on Mark as read, bubble density 200/block with size 40-350 percent and height -2..4, no reopen after a locking save, locked markers unbreakable by anyone, symbol-box outline and break decal, no Material tooltip line.

QA package 2026-09-10 (sixth): commit ca3566e, thebasics_5_9_1.zip SHA256 8e0ff9dc0c975dc2..., deployed to the test server (restart 06:33Z, RunGame reached, no mod errors) and to Profile2/Profile3. Locked-break warning on the client and the rebalanced preview. BASIC reported the rest of the smoke test looking good.

QA package 2026-09-10 (seventh): commit a4410c3, thebasics_5_9_1.zip SHA256 10567faa896c3edd..., deployed to the test server (restart 06:38Z, RunGame reached, no mod errors) and to Profile2/Profile3. Other icons button clearance from the preview.

QA package 2026-09-10 (eighth): commit 318010d, thebasics_5_9_1.zip SHA256 7855ed231f113dea..., deployed to the test server (restart 17:03Z, RunGame reached, no mod errors) and to Profile2/Profile3. Shared ScrollableTextArea: caret fix across line wraps in the scene editor, character sheet and player notes, and a scrolling scene description box.

QA package 2026-09-11: commit 7311459, thebasics_5_9_1.zip SHA256 1c0970c81a98f39e..., deployed to the test server (restart 03:39Z, RunGame reached, no mod errors) and to Profile2/Profile3. Preview height offset now moves the whole stack.

QA package 2026-09-11 (second): commit 817c21c, thebasics_5_9_1.zip SHA256 ce295d0507675df8..., deployed to the test server (restart 03:50Z, RunGame reached, no mod errors) and to Profile2/Profile3. Indicator base size 0.64 blocks (was 0.8); preview symbol a fifth larger.

QA fix 2026-09-11: Two identical stones failed because the game merges supplied stacks before matching each repeated wildcard ingredient. The recipe now uses one wildcard ingredient with quantity 2. This accepts two stones of the same type, split across slots or stacked. A regression test loads the recipe asset and exercises the game's shapeless matcher and consumption logic. Original recipe: 3 expected failures; corrected recipe: full suite 838 passed. Release package SHA256 D09A51795C809EE13118AE864DEEEB6145A27752A7AB50D8B4FC0F50F617AC76 uploaded to QA and both client profiles. Human chert retest pending.
