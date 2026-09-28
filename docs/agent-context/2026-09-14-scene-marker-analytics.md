# Scene marker usage analytics

User approved implementing all proposed usage measurements together on September 14, 2026. This extends PR #243 without changing marker gameplay or publishing another release.

## Event contract

Use the existing consent-controlled `feature used` event with `feature_name=scene_markers`. Never supply an actor pseudonym. The relay contract revision becomes 7, and remains compatible with previous producers.

| Action | Meaning |
| --- | --- |
| `placed` | A marker was successfully placed. Includes fresh and reused items. |
| `saved` | The server accepted and applied an editor save. |
| `reader_opened` | A client successfully opened a reader, accepted by the server. Cooldown 30 seconds per viewer and placed marker; all held-item readers share a per-viewer cooldown. |
| `marked_read`, `marked_unread` | The server changed the viewer's effective read state. Repeated requests do not count. |
| `moved` | A subset of placements where the item already carried written content. Copies also qualify; this is a reuse measurement, not proof of relocation. |
| `removed` | An unlocked marker was successfully broken by a player. |
| `bubble_viewed` | A client rendered a bubble at opacity >= 0.5 with its center on screen for one second, with no observation gap over 250 ms. Cooldown 60 seconds per viewer and marker. This measures visibility, not reading comprehension. |

All scene events use bounded properties `scene_display_mode` (`when_targeted`, `always_nearby`, `on_interaction`), `scene_content` (`empty`, `written`), `scene_locked` and `scene_body_shown` (booleans). Placements add `scene_placement` (`fresh`, `reused`) and `scene_mount` (`ground`, `wall`). Saves add `scene_save_kind` (`empty`, `first_content`, `edit`). Reader opens add `scene_read_source` (`placed`, `held`). Config snapshots add boolean `enable_scene_markers` so reporting can distinguish enabled installations from eligible installations.

Never export marker title/body, author names, player IDs or pseudonyms, positions, persistent marker IDs, item codes, or freeform error strings. Position and viewer references may exist only in bounded, temporary game-side validation and deduplication state. Clients report observations through a safe channel, without queuing disconnected observations. The server validates the loaded marker, dimension, reach or configured bubble distance, consent, and rate limits; it derives all exported properties itself. Opt-out and feature disablement stop tracking. Telemetry failure must not interrupt interaction or rendering.

## Reporting and rollout

Report weekly reporting installations with placement, save, explicit read, and bubble visibility separately; total actions; week-over-week return usage; display-mode preference on saved markers. Installation identity is the unit, not unique players. `moved` is already included in `placed` and must never be added to placement totals. No historic backfill is possible.

Ship the Worker contract before distributing the mod. Health must advertise revision 7 before mod upload. Deploy, manual QA, merge and publication remain separate owner-approved steps. QA covers accepted/denied edits, opt-out, two viewers, bubble dwell/cooldown, read toggles, and pickup/replacement. Retire this kickoff after the update is published and its first live scene events are verified.
