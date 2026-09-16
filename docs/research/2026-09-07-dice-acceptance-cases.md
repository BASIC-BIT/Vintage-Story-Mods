# Dice evaluator: deterministic acceptance cases and open decisions

Research/design artifact. No tests in this document have been executed, and no dependency has been adopted. These are concrete candidate assertions for DiceRoller 4.2.0 at source tag commit `4f9e55e08fb9b8d922e50ad3da62f61c8b793571`. See [exact-version evaluation](2026-09-07-dice-evaluator-followup.md).

## Test method

Use a fresh per-call RollerConfig with RollDie backed by a fixed face queue. Assert the requested inclusive face range on every callback, reject queue underflow, and assert draw count and any expected unused entries. Supply faces directly, not RNG byte values: the upstream test helper's byte sequences use a different encoding.

Compare numeric result, result kind, generated faces and kept/dropped/success/failure flags. Assert the formatter preserves relevant arithmetic and flags without relying on upstream forum formatting or on summing RollResult.Values (which includes structural markers). Injecting a fixed face queue removes statistical uncertainty from these tests.

Do not modify global DefaultConfig. Reasons belong outside the evaluator. Game, history and Discord rendering must consume the same completed result without making further RNG calls.

## Arithmetic and ordinary dice

These cases express candidate product semantics; the source's ordinary precedence, decimal arithmetic and public RNG seam are documented in the [pinned evaluation](2026-09-07-dice-evaluator-followup.md). They remain unexecuted.

| Expression | Faces supplied | Expected assertion |
| --- | --- | --- |
| `2d6+3` | 4, 5 | Total 12; two draws; faces 4 and 5 remain visible. |
| `2d20kh1+3` | 7, 16 | Total 19; 7 dropped, 16 retained. |
| `2d20kl1+3` | 7, 16 | Total 10; 16 dropped, 7 retained. |
| `4d6dl1` | 1, 4, 5, 6 | Total 15; retain the dropped 1 in the breakdown. |
| `2d20kh1` | 16, 16 | Total 16; exactly one die retained. Which identical die is marked dropped need not be a public promise. |
| `2d6+3*2` | 4, 5 | Total 15, ordinary multiplication precedence. |
| `(2d6+3)*2` | 4, 5 | Total 24. |
| `5/2` | none | Total 2.5; no random draws. Decimal precision/display policy remains a decision. |
| `-(1d6+2)` | 4 | Total -6. Do not silently clamp negative totals. |

## Success-pool cases

The pinned [SuccessRollShould tests](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/TestDiceRoller/Grammar/SuccessRollShould.cs) explicitly cover inclusive thresholds, dropped dice, constants, critical-success additions and mixed result kinds. Strict-threshold behavior follows the explicit comparison syntax and needs its own regression case.

| Expression | Faces supplied | Expected assertion |
| --- | --- | --- |
| `4d6>=5` | 4, 5, 1, 6 | 2 successes, not sum 16. |
| `4d6>5` | 4, 5, 1, 6 | 1 success; distinguish strict > from >=. |
| `4d6>=5f1` | 4, 5, 1, 6 | Net 1 success: two successes minus one failure. |
| `4d6dl1>=5f1` | 4, 5, 1, 6 | 2 successes; dropped 1 does not count as a failure. |
| `4d6>=5 + 2` | 4, 5, 1, 6 | 4 successes. Constant is added to the success count, not to each face. |
| `2d6>=5 + 2d6` | 4, 5, 1, 6 | Numeric total 8: first pool contributes 1, second roll contributes 7; label as total, not 8 successes. |
| `4d6>=5cs=6` | 4, 5, 1, 6 | 3 successes under the library's explicit critical-success modifier. Do not confuse this with cosmetic highlighting. |

An explicit success/critical expression can encode a player's rule. Merely rolling the maximum on an ordinary die must not produce invented narrative consequences. Exact exposed critical/fumble syntax is not yet accepted.

## Failure and delivery cases

The following are proposed application acceptance criteria, not assertions that the unwrapped library already enforces them.

| Scenario | Expected outcome |
| --- | --- |
| Invalid suffix such as `2d6garbage` | Reject whole request; no accepted partial expression or public result. |
| `1/(1-1)` | Error; no game/Discord success output. Exact friendly error wording can be chosen later. |
| Fractional count `2.5d6` or fractional sides `1d6.5` | Decision pending: library truncates dimensions; recommend explicit rejection rather than surprising truncation. |
| Count exceeds configured draw budget | Reject; no partial result published. |
| Nested parentheses exceed selected input nesting limit | Reject before parser entry; evaluation-depth protection alone is insufficient. |
| Output breakdown exceeds chosen output budget | Reject or use an explicitly agreed complete compact representation; do not silently omit mechanically relevant faces. |
| Callback returns an out-of-range face | Evaluator error; confirms inclusive min/max contract is respected. |
| Valid result with three game recipients and enabled Discord relay | One evaluation, unchanged draw count after all rendering, one canonical relay enqueue; external Discord receipt is a separate runtime check. |
| Relay disabled or unavailable | In-game roll remains valid; do not reroll or undo it. |
| Reason contains dice-looking text | Treat as reason text, never as another expression. |
| Reason contains markup/mentions | Preserve meaning through destination-specific formatting without creating unintended mentions. |

Audience was settled in the later user reply: see Accepted audience and range-marker cases below. Earlier evaluator cases remain independent of recipient selection.

## Open decisions for the next design frontier

Recommendations below are not approvals:

1. **Notation dialect:** use explicit `>=`/ `>` semantics and the pinned evaluator's documented notation, rather than promising exact Roll20 compatibility. Define the reason delimiter independently.
2. **Modifier order:** document explode -> reroll -> keep/drop -> success/failure, rather than pretending all typed modifier orders are equivalent to left-to-right evaluation.
3. **Arithmetic scope:** recommend parentheses and ordinary +, -, *, / with decimal results. Discuss rounding/formatting and any helpers separately; “general expressions” does not automatically mean arbitrary macros or every function.
4. **Pool arithmetic:** recommend retaining the library's documented result-kind behavior above and labelling total versus successes explicitly.
5. **Fractional dimensions:** recommend rejecting them; decimal final arithmetic remains useful.
6. **Advanced modifier domain and budgets:** establish which expressions modifiers may act on and how every terminating/error path is bounded. A generated-dice limit cannot count work that generates no dice. See the follow-up validation before choosing a cap policy.
7. **Dependency:** DiceRoller 4.2.0 is a recommendation pending acceptance and later package/runtime validation. Its Antlr dependency must be included deliberately by the mod packager.

No ADR is created because evaluator adoption and syntax remain proposed. No glossary term has newly been agreed by the user.



## Combined modifiers and cap counterexamples

These cases were traced through the same pinned source, not executed. See [final source validation](2026-09-07-dice-evaluator-followup.md#final-source-validation-adoption-remains-conditional) for the underlying source links and adoption implications.

| Expression/config | Faces supplied | Expected pinned-source behavior |
| --- | --- | --- |
| `3d6.explode().keepHighest(2).success(>=5)` | 6, 2, 5, 6, 1 | Initial faces 6/2/5; explosion adds 6 then 1; keep both sixes; 2 successes; 5 draws. |
| `3d6.success(>=5).keepHighest(2).explode()` | 6, 2, 5, 6, 1 | Same result as above because modifier categories have fixed timing. |
| `1d6.explode().reroll(=1)` | 1, 6 | Original 1 dropped, replacement 6 retained; total 6. Replacement does not restart the earlier explosion phase. |
| `2d6ro=1` | 1, 1, 4 | Total 5, 3 draws. Only one replacement across the pool. This behavior needs an explicit product decision. |
| `1d6!e`, MaxDice=3, MaxRerolls=4 | 6, 6, 6 | TooManyDice before fourth RNG callback; no completed result to publish. |
| `{1}rr=1`, same limits | none | Library silently returns 1 after four zero-draw rerolls. An application must not claim the dice-count guard prevents this. |
| `{0d6}rr=0`, same limits | none | Same zero-draw-cap risk with a dice-containing expression; a superficial “contains dice” guard is insufficient. |

The first four rows are behavior-characterization candidates, not a statement that every behavior should become The BASICs policy. The last two are counterexamples that adoption must address, not accepted successful product outcomes. The combination cases ensure category order is tested rather than only testing each modifier alone.



## Accepted audience and range-marker cases

The owner accepted following the current whisper/normal/yell chat mode, independent of IC/OOC/global-OOC chat type, and requested a clear indicator such as (W)/(Y) before whispered/yelled results.

- Whisper: use configured whisper reach and prefix the result with (W).
- Yell: use configured yell reach and prefix the result with (Y).
- Normal: use configured normal reach. Proposed presentation leaves normal unmarked.
- Markers appear in game and plain-text Discord/history output; they describe in-game reach, not a restriction on the existing Discord destination.
- Every admitted recipient receives complete readable numbers, without speech-language transformations.
- Capture mode once for the roll; selecting a different mode afterwards must not change its stored marker or result.
- Parked global OOC does not make a roll server-wide; retain the selected local chat mode.
- Tests should compare each selected mode against the corresponding configured reach, including existing unlimited-range semantics where applicable, rather than hard-coding distances.

Illustrative output:

```text
(W) [Roll] Mira | 2d6+3: [4, 5] + 3 = 12
[Roll] Mira | 2d6+3: [4, 5] + 3 = 12
(Y) [Roll] Mira | 2d6+3: [4, 5] + 3 = 12
```

Exact surrounding formatting remains a design choice. Range behavior and the requirement for a visible marker on whisper/yell are accepted.



## Accepted reason parsing

Q4 is accepted with optional delimiters and explicit errors for ambiguous or incomplete expressions:

- `/roll 2d6+3 forcing the gate`: unmarked trailing reason when unambiguous.
- `/roll 2d6+3 # forcing the gate`: explicit reason boundary.
- `/roll 2d6+3 // forcing the gate`: alternate explicit boundary.
- `/roll 2d6 +`: incomplete expression error; never silently accept 2d6.
- `/roll 2d6 + strength`: error for an unsupported expression operand; recommend an explicit reason boundary if the text was intended as a reason.
- `/roll 2d6 # + strength`: reason text, not an operand.
- Preserve spaces in valid arithmetic and supported function calls. A generic longest-valid-prefix fallback is insufficient because it can turn malformed expressions into successful rolls.
- On ambiguity, request a delimiter rather than guessing. Lexical details must preserve these accepted examples.

## Earlier private-roll design question, superseded below

The owner raised private rolls. Intended recipients and implementation scope are unresolved. Do not mark a roller-only, selected-player/GM, blind, or staff-history policy as accepted. Any future private mode needs a deliberate path that does not accidentally publish its result through the existing ordinary processed-chat relay. This does not change Q3 for ordinary rolls.



## Accepted private-roll audience

Q6 is settled: private rolls show the result only to the requesting player and do not enter the ordinary Discord relay. Named recipients, GM rolls and blind rolls are deferred; /gmroll is a possible future feature, not current scope.

Acceptance cases:
- A private roll from whisper, normal or yell reaches only its requester.
- Enabling the ordinary Discord relay does not enqueue a private result.
- Nearby players do not receive the result or a roll notification.
- No passive bubble/chatter cue reveals a private roll.
- The private result remains a single server-generated result.
- Private command spelling, display label and staff-history content policy remain decisions for the next round.



## Accepted aliases, analytics and bubble direction

- /r and /roll are aliases of the same public action, not separate evaluations.
- /proll and /privateroll are aliases of the same private action.
- Namespaced fallback commands remain available; short-command collisions must not overwrite another mod's handler.
- Private output uses [Private Roll] with no W/Y marker.
- Private command usage can be counted through existing analytics, but expressions, reasons, faces and result contents must not enter analytics or shared logs/history.
- Ordinary public rolls should have cutesy bubble presentation; this is a user requirement, with exact styling still undecided. Do not silently retain the earlier no-bubble scope.
- Private rolls must not produce a cue visible to anyone else. Spectator entity-attached cues remain subject to the existing spectator policy.
- Default enablement, existing chat privilege, one server feature toggle and independence from OOC settings are accepted.
- Further public analytics should answer feature-use and failure questions with structured, bounded properties rather than collecting chat content. Exact event/property choices remain proposed.



## Accepted single-line bubble shorthand

- d20 yielding 17 shows a die icon containing 20, with result 17 next to it on the same line.
- d6 yielding 4 shows a die icon containing 6, with result 4 next to it.
- Whisper/yell markers remain before the shorthand.
- Icon numeral describes die sides; it must not be replaced with the outcome.
- Ordinary dN terms use readable numeric labels within supported die limits.
- Plain-text destinations retain readable notation such as d20; do not require the icon asset to interpret Discord/history.
- The previous two-line result-card mockup is superseded.
- Q11 accepted: 2d6+3 yielding 12 shows a generic die icon plus 12; a success pool yielding 5 successes shows a generic icon plus 5 successes. Plain dN retains its numbered icon. No formula-to-icon conversion or adaptive layouts. Full details remain in chat.
