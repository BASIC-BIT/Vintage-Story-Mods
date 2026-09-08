# The BASICs dice rolling design

Date: 2026-09-08.
Status: accepted design, implementation underway. Q1-Q13 are accepted. The owner subsequently authorized implementation, PR creation, and iteration until merge-ready. Merge, manual QA, server changes, and analytics deployment still require their separate approvals.

## Purpose

Provide server-generated dice rolls for roleplay scenes, with expressive mechanics, readable chat output, optional reasons, self-only private rolls, and compact cute bubbles. Ordinary scene rolls use the existing Th3Essentials Discord relay.

Physical dice, GM/blind rolls, selected private recipients, character-stat automation, macros and scripting are outside current scope. The owner specifically wants /gmroll considered eventually.

## Commands and reason parsing

- /roll and /r: ordinary scene roll.
- /proll and /privateroll: self-only private roll.
- /thebasics roll and /thebasics proll: namespaced fallbacks.
- Bare commands show help/examples; they do not silently choose a die.
- Preserve other mods' command ownership; do not overwrite a conflicting short command. The namespaced command remains usable.

Reasons are optional. Accept unambiguous trailing text and optional # or // delimiters:

```text
/r 2d6+3 forcing the gate
/r 2d6+3 # forcing the gate
/r 2d6+3 // forcing the gate
```

Incomplete arithmetic such as `2d6 +` is an error. Do not implement generic longest-valid-prefix fallback. When a suffix could be intended as an expression operand, ask the player to use an explicit reason boundary. Keep reason parsing outside the evaluator, preserving whitespace in valid expressions and helper calls.

## Mechanics

Accepted:
- Standard dN and NdN dice notation and signed modifiers.
- Keep highest/lowest and drop variants needed by that mechanic.
- Exploding dice, success-counting pools and general arithmetic.
- Player-facing ! explosion syntax.
- Reroll-once applies once per eligible die, not once across the entire pool.
- Explicit strict/inclusive comparisons, such as > versus >=.
- +, -, *, /, parentheses, decimal results, floor, ceil and round.
- Fractional dice counts/sides are errors, not silently truncated dimensions.
- No variables, scripts or user-defined functions.

Modifier categories execute in a fixed order:

```text
explode -> reroll -> keep/drop -> count successes
```

Written order does not change this category ordering. A maximum produced in the reroll phase does not restart explosion. Test compositions, not just isolated features.

Accepted numerical details:
- round means nearest integer, with exact halves rounded away from zero; floor/ceil retain their usual mathematical meaning.
- Pool comparisons count qualifying retained faces. A constant added to a pool adds to the success count; mixing a success pool with an ordinary total-producing roll produces a numeric total. Label successes versus totals explicitly.
- Do not add automatic narrative critical-success/failure labels. Any further critical/fumble modifier syntax would need a documented purpose; it is not necessary for ordinary exploding dice or success pools.
- Public dice dimensions are positive integers within finite limits. Group reroll syntax is not implied by supporting parenthesized arithmetic. Do not expose extra upstream grammar merely because the evaluator accepts it.

## Audience, identity and output

Ordinary scene rolls follow the player's selected whisper/normal/yell mode, independently of IC/OOC/global-OOC chat type. Capture that mode for the roll and use configured reach. Parked global OOC does not turn the roll server-wide.

Use (W) before whispered results and (Y) before yelled results. Normal results are unmarked. Markers survive plain-text Discord and history rendering. They describe in-game reach, not Discord visibility.

Show complete readable results to admitted recipients. Do not scramble or alter numbers through language, accent or speech effects. Reuse existing RP name resolution and escaping, retaining existing staff attribution for public history. Rolls do not decide another character's actions or narrative consequences.

One accepted request produces one completed result, reused for chat, history, bubble and relay. Never reroll per recipient or destination.

## Private rolls

Only the requesting player receives a private result. Use [Private Roll], without W/Y markers.

Private expressions, reasons, faces and results do not enter shared chat logs, staff chat history, ordinary processed-chat relays or Discord. Audit/error paths must not accidentally include the raw command. Normal command-use analytics remain allowed without roll contents.

Private rolls produce no shared entity bubble, roll notification or chatter. There is no selected-recipient or GM mode in this release.

## Bubble presentation

Use a cute, small rounded bubble with a soft accent border.

- Plain dN: small wireframe die icon with the side count centered inside, and result beside it on one line.
- Every other expression: generic wireframe die icon beside the numeric total or success count.
- Put (W)/(Y) before the die icon in bubbles and before the public chat message.
- Full expressions, reasons and per-die breakdowns remain in chat.
- No formula-to-icon conversion, adaptive expression layout or animated dice system.

The die icon must render reliably; do not assume an emoji glyph exists. Honor the existing Off/Vanilla/RpText bubble policy and existing visual lifetime. Preserve spectator suppression of entity-attached cues; implementation is based on main 5841685, which includes the spectator policy. Spectators can still use commands.

The existing renderer's line-of-sight and distance limits are independent of chat recipients. A result remains readable in chat even when the overhead bubble is not visible.

## Discord and analytics

Ordinary scene rolls follow the existing The BASICs/Th3Essentials relay setting. No independent dice relay switch or new bot. Send one rendering of the completed result through one relay path. Queue insertion is not a Discord delivery receipt. Discord outages do not invalidate an in-game roll.

Use existing consent-controlled analytics:
- Canonical command usage for public/private aliases, success/help/disabled/error categories.
- Private command usage only, without mechanics or contents.
- Public mechanic adoption and bounded complexity/mode/failure indicators, without raw expressions/reasons.
- No player-input text in exception messages sent to analytics.

New event labels/properties need matching analytics-relay allowlist changes and tests. A future schema deployment is a separate authorized operation. Do not claim working analytics based only on C# instrumentation.

## Enablement and engineering defaults

Accepted: enabled by default, existing chat privilege, one server toggle covering public/private rolls, independent of OOC toggles. Disabled attempts should receive useful feedback and normal analytics when enabled by consent.

Accepted engineering approach: fixed conservative limits for input length, nesting, generated dice, evaluation work and output size, plus a small per-account burst guard. Keep these as straightforward implementation constants initially rather than a new configuration subsystem. Errors publish no partial result. Document actual chosen limits in command help and test their boundaries.

Evaluation work must count zero-draw operations as well as generated dice. Bound parsing before evaluation. Reject divide-by-zero, overflow and unsupported trailing expression syntax explicitly.

## Evaluator choice and integration boundaries

Research favors DiceRoller 4.2.0 as an adoption candidate, not a drop-in contract. Its published behavior differs from the accepted design: !e explosion shorthand, once-per-pool reroll counters, possible silent zero-draw group reroll caps, and fractional dimension truncation.

Choose a narrow adapter or focused evaluator implementation based on the deterministic cases. The accepted semantics take precedence over preserving a dependency's quirks. Avoid implementing a general-purpose scripting language or relying on unstable concrete AST internals. Implementation ruling: use a focused native C# evaluator, avoiding the documented dependency mismatches and packaging dependency. Keep the complete accepted mechanics.

If adopting the package, pin it and its dependencies and verify the distributable zip includes required DLLs. The current mod packaging does not automatically collect third-party dependencies. Use an explicit roll message kind/delivery path for history and public relay; private results must bypass those shared surfaces.

## Verification and remaining work

The [acceptance cases](../../research/2026-09-07-dice-acceptance-cases.md) provide fixed-face examples, source counterexamples and delivery cases. They have not been executed. Some describe upstream quirks for characterization; accepted product behavior in this document wins.

Required coverage:
- Grammar, optional reasons, arithmetic/helpers and deterministic modifier composition.
- Per-die reroll-once and zero-draw cap errors.
- Result-kind labelling and preserved full breakdown.
- Alias equivalence, command collisions and disabled behavior.
- Mode range selection, W/Y markers and no speech transforms.
- Private non-disclosure across all shared output/error paths.
- Single evaluation and single ordinary relay enqueue.
- Analytics schema compatibility.
- Packaging/loading if a library is adopted.

Owner-approved manual QA later: nearby clients, out-of-range client, all chat modes, private rolls, visible and spectator states, bubble modes, and a real Th3Essentials Discord round trip. No live QA, server change, merge, publication or analytics deployment is authorized by this design document.

## Evidence and precedence

Worktree: D:/bench/vs/work/thebasics-dice-design, branch codex/thebasics-dice-design, rebased onto main 5841685 for implementation.

This consolidated document supersedes earlier research recommendations where the owner chose otherwise. The [decision record](../../agent-context/2026-09-07-thebasics-dice-design.md) preserves accepted rounds. The [glossary](../../../CONTEXT.md) contains domain terms only.

Supporting investigations: [libraries](../../research/2026-09-07-dice-libraries.md), [pinned evaluator](../../research/2026-09-07-dice-evaluator-followup.md), [RP needs](../../research/2026-09-07-dice-rp-feature-needs.md), [Discord](../../research/2026-09-07-dice-discord-relay.md), [bubbles and analytics](../../research/2026-09-07-dice-bubble-analytics.md).
