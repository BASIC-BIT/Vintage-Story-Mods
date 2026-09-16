# Dice bubbles and analytics: source findings

Design research only. No implementation, deployment or runtime test. Inspected the dedicated design worktree at c11dba7; the primary checkout's uncommitted spectator changes are not present here.

## Existing visual capabilities

[SpeechBubbleVtmlPatches](../../mods-dll/thebasics/src/ModSystems/ChatUiSystem/SpeechBubbleVtmlPatches.cs) supports rich VTML textures, plain-text fallback, padded rounded backgrounds and colored kind borders. Its text width limit is 350 px, standard font size 25 px, whisper scaling 0.75 and yell scaling 1.3. Lifetime follows text length with a configurable minimum, default 3500 ms.

[VtmlUtils](../../mods-dll/thebasics/src/Utilities/VtmlUtils.cs) supports font, strong, italic, line-break and icon markup. This does not establish that a dice icon or Unicode dice emoji renders correctly. Validate an existing icon or draw a small die motif.

Bubble delivery can carry a compact summary independently of the full chat text: [SpeechBubbleClientDataTransformer](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Transformers/SpeechBubbleClientDataTransformer.cs) builds entity/message clientData, and [TransformerSystem](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Transformers/TransformerSystem.cs) sends that alongside chat. Both should render the same completed roll, without reevaluation.

Existing visibility includes line-of-sight gating and a 100-block acquisition cap. Chat delivery range and visible overhead geometry are not identical. Preserve complete chat results even where a bubble is not visible.

## Proposed cute presentation, unapproved

A small rounded card above the roller, soft accent border, tiny die motif, prominent total or success count, compact expression and W/Y marker. Full dice breakdown and long reasons remain readable in chat. Use existing lifetime initially; a subtle pop/wobble is an optional later styling decision, not a verified current capability.

Honor existing bubble mode: Off means no overhead cue, Vanilla receives a plain summary, RpText receives styled presentation. Do not route dice through speech transformations or chatter to obtain a bubble.

Private rolls emit no shared bubble or chatter. The inspected base does not establish spectator-specific suppression in this path. Explicitly reconcile the primary checkout's spectator policy and ensure invisible staff do not acquire entity-attached dice cues.

## Analytics integration

[AnalyticsService](../../mods-dll/thebasics/src/ModSystems/Analytics/AnalyticsService.cs) supplies command/feature/failure events through the existing consent-controlled [AnalyticsSystem](../../mods-dll/thebasics/src/ModSystems/Analytics/AnalyticsSystem.cs).

Accepted: private rolls may have ordinary command-use analytics without expression, reason, faces or result contents.

Proposed instrumentation:
- One canonical roll/proll command-use event per attempt, regardless of alias. Include success/help/disabled/invalid-expression/limit-exceeded through bounded labels.
- Private: ordinary command-use metadata only; no mechanics, identity or contents.
- Public: feature adoption via bounded mechanic flags (exploding, reroll, keep/drop, success pool, arithmetic), selected chat mode and appropriately bucketed complexity counts if useful. Do not collect raw expressions or reasons.
- Genuine errors use bounded categories and exception type, not messages that may contain player input.
- Track disabled attempts as well as successful use, matching the RP culture skill's measurement guidance.

The [relay worker](../../infra/terraform/stacks/thebasics-analytics-relay/worker/analytics-relay.mjs) validates property names, enums and per-event properties. Dice labels are not yet accepted. Instrumentation needs matching schema changes and tests; a C# TrackCommandUsed call alone is not evidence of working analytics. [RelayAnalyticsSink](../../mods-dll/thebasics/src/ModSystems/Analytics/RelayAnalyticsSink.cs) drops rejected batches, making compatibility with the relay's deployed schema important.

Deploying a relay schema is a later separately authorized operation. No live analytics configuration was read or changed here. This is scoped to dice instrumentation, not an unrelated repository-wide analytics expansion.



## Accepted single-line dice shorthand

Q10 accepted with owner refinement: public roll bubbles use a single line. For a standard dN roll, show a small die icon with N (the number of sides) inside, followed by the rolled result. The icon's number is the die type, not the rolled face. Retain W/Y range markers.

Illustrative notation here uses [die:20] as a textual placeholder for an actual icon:

- d20 result 17: [die:20] -> 17
- whispered d6 result 4: (W) [die:6] -> 4

The single-line shorthand supersedes the earlier two-line card sketch. Keep the agreed cute rounded styling. Exact icon artwork remains implementation/visual QA work; do not depend on unsupported emoji.

Q11 accepted on 2026-09-08 after the owner rejected overengineering: only plain dN gets the numbered die icon. All other expressions use a generic die icon beside the total or success count. Full expression and breakdown stay in chat. No formula-to-icon conversion or adaptive layout. This supersedes the proposed width-dependent expression rendering.
