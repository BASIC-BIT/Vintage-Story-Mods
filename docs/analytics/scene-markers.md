# Scene marker usage

The reporting unit is a consenting server installation, including single-player installations. The relay sets `distinct_id` and `server_install_id` to the same installation identity. These are not unique players, downloads, worlds, or communities. Reinstalling or resetting analytics identity may create another installation.

Use PostHog project 438227, The BASICs Analytics. [scene-marker-queries.json](scene-marker-queries.json) contains query definitions for the query API or saved insights. They are prepared definitions, not published dashboard objects. Validate them in that project after the first instrumented release sends events. Historic versions cannot supply these measurements.

## Measurements

| Chart | Definition |
| --- | --- |
| Weekly usage by installation | Distinct installation identities per UTC week for each action. An installation may appear in multiple series. |
| Action counts | Total accepted events, split by action. Repeated opens and impressions are throttled. |
| Saved display modes | Save events split by `scene_display_mode`. This measures save choices, not the current number of markers using each mode. |
| Consecutive-week explicit usage | Among installations with an explicit scene action in the previous complete UTC week, how many also have one in the next complete week. Excludes passive bubbles and the redundant `moved` subset. The first and current partial weeks are excluded. |
| Config snapshot coverage | Distinct installations sending a snapshot with `enable_scene_markers=true` or `false`. This is observed snapshot coverage, not a complete active-installation denominator. Long-running servers may not send a config snapshot every week. |

Do not divide weekly usage by same-week config snapshot counts to calculate adoption. A long-running installation can use markers without emitting another snapshot that week. A future adoption percentage needs each active installation's latest known config as of the measurement time and instrumented-version coverage. Show raw installation counts first.

`moved` is already included in `placed`. It means a written item was reused, including creative copies. There is no marker identifier to establish a source and destination or measure marker retention. `removed` means a successful player break, not an explosion, chunk unload, or permanent deletion of its dropped item.

`reader_opened` means the client reported a successful reader opening and passed server validation. `bubble_viewed` means a qualifying bubble was visible for one second. Neither proves a person read it. Keep explicit reading and passive exposure separate. Bubble visibility requires its center in the viewport and opacity at least 0.5, with no visibility observation gap over 250 ms. Server cooldowns apply per viewer and marker, 30 seconds for reader opens and 60 seconds for bubbles. Held-item readers are reported separately through `scene_read_source=held`. All held items share one 30-second cooldown per viewer, so quickly opening different held notes undercounts those opens. This avoids assigning identities or content fingerprints to held items.

All charts are limited to consenting installations with the new build. Telemetry outages, queues, cooldowns and mixed client versions can undercount. Exclude the known QA installation through a private dashboard filter before drawing public conclusions; do not hardcode its identity in this repository. Always include reporting-installation counts beside event totals so one busy server is visible as such. The current week is partial on trend charts.

## Simple QA cards

Run after owner approval on the QA server, with the matching client build and relay contract 8 active. Inspect emitted events through the QA installation's private PostHog filter and check the relay's aggregate rejection count in Workers Logs. Allow the configured flush interval and ingestion delay.

1. **Fresh ceiling placement and saves (P1).** Place a fresh marker against the underside of an attachable block, give it a title/body, save, then edit and save again. Expect a ceiling plate and `placed` with `scene_placement=fresh` and `scene_mount=ceiling`, then `saved` with `first_content`, then `edit`. Change display mode, lock and body visibility and verify only bounded values appear. Watch for a missing mount, a rejected event, or title/body, author, coordinates or pseudonym in the payload.
2. **Open and toggle.** Open its reader twice within 30 seconds. Expect one `reader_opened`. Mark read, then unread. Expect one of each state change. Repeating an unchanged read-state request must not create another event. Have player two open the same marker within that window and expect their own open to count.
3. **Move a written marker to a ceiling (P1).** Unlock and break the written marker, then place the dropped item on a wall. Break it again and place it against the underside of an attachable block. Expect `removed` at each successful break, and one `placed` with `scene_placement=reused` plus one `moved` for each new placement. Both wall events must have `scene_mount=wall`; both ceiling events must have `scene_mount=ceiling`. The text should survive each move exactly as before. Watch for either ceiling event missing its mount or being rejected by the relay. A locked or claim-denied break must produce no removal event.
4. **View a bubble.** Set Always Nearby and show text. Look for less than one second, then away; expect no impression. Look continuously for over one second; expect one `bubble_viewed`. Staying in view must not emit every frame. Within 60 seconds, looking away/back must not emit another. Player two may receive their own impression. A bubble behind terrain, off screen, or suppressed by a read mark must not count.
5. **Turn analytics off.** Disable analytics through the existing consent control. Repeat placement/save/open/view. Expect no remote scene events; marker gameplay still works. Re-enable and check that new actions count without replaying observations from the disabled period. Save Scene Markers as disabled in `/basic`: gameplay and its recipe should remain unchanged until restart. Restart the server and reconnect both clients, then verify crafting/placement/overlays and scene reporting stop while retained markers remain visible as physical plates and can still be read.

## Rollout order

1. Finish automated tests and review both producer and relay changes.
2. After approval, deploy the `thebasics-analytics-relay` Worker and verify its health endpoint reports `contract_revision >= 8`. The existing release workflow refuses a lower revision.
3. After QA deployment approval, upload the new mod build to the test server, restart it and use matching local client builds. Run the cards above. Manual QA is complete only when the owner confirms the observed results.
4. Merge and publish only with owner approval. Save the prepared insights/dashboard in the analytics project during rollout, with the private QA exclusion and an annotation marking the first instrumented release.
5. Confirm the first live `scene_markers` events have the released mod version, inspect rejection counts in Worker logs, and then begin the usage readout. A zero before deployment means uninstrumented, not unused.
