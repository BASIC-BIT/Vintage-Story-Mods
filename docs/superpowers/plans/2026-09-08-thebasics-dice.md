# The BASICs dice implementation plan

Spec: ../specs/2026-09-08-thebasics-dice-design.md
Base: origin/main at 5841685, rebased before implementation.
Goal: implement full accepted design, PR, review/CI iteration and 30-minute exact-head readiness gate. Do not merge.

## Global constraints and rulings

- Implementation request accepts final proposed defaults. Test seams are the spec's evaluator with deterministic faces, command/delivery result, bubble summary/rendering and analytics event contract.
- Ruling: use a focused bounded C# evaluator rather than adapting DiceRoller 4.2.0. Its shared reroll counter, group work gaps and dependency packaging would require invasive fixes. Preserve ALL accepted mechanics; do not substitute a basic-only grammar.
- Pure evaluator public boundary: DiceEvaluator.EvaluateInput(string input, Func<int,int> rollDie = null) returns DiceRollResult, throws DiceRollException with safe Code. rollDie receives positive side count and returns 1..sides.
- DiceRollResult fields: Expression, Reason, Value (decimal), IsSuccessPool, Breakdown, SimpleSides (nullable int), Mechanics (IReadOnlyCollection<string>). Immutable accepted result reused for all destinations.
- Public helpers/rounding, suffix order, guards and reason ambiguity must match accepted cases.
- No change to live settings, game profiles or server until explicit QA approval. Build packaging into worktree staging only.
- Private command routing must bypass history, events, bubbles, Discord and content-bearing audit paths, while counting command usage.
- Parent owns integration/config/docs. Evaluator task owns only DiceEvaluator/DiceRollResult/DiceRollException source/tests. No overlapping edits.
- Current branch is isolated; no user secrets copied.

## Task 1: Evaluator and reason boundary

Create src/ModSystems/DiceRolling/DiceEvaluator.cs and result/error types, tests in thebasics.Tests/ModSystems/DiceRolling.
Start with deterministic 2d6+3 faces 4,5 ->12 failing test, implement, then expand vertical slices.
Required cases: +,-,*,/, unary, parentheses, floor/ceil/round away from zero; kh/kl/dh/dl; ! explosion; r/ro once per eligible die and rr recursion with comparisons; >= success pools; strict comparison; modifiers in fixed categories; mixtures/pool values; helper recursion; invalid suffix and optional # or // / natural reasons.
Reject fractional/zero/negative counts/sides, overflow, division by zero, unknown functions, impossible unbounded recursion via whole-request budget. No partial output. Finite input/token/depth/draw/output bounds.
SimpleSides only for exact plain dN; all other expressions use generic icon. Breakdown preserves kept/dropped/replaced/generated faces.
Test command:
D:/bench/vs/.dotnet/dotnet.exe test mods-dll/thebasics.Tests/thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true --filter FullyQualifiedName~DiceEvaluatorTests
No package dependency is required.

## Task 2: Commands, delivery, configuration

Create DiceRollCommands.cs, DicePresentation.cs and delivery tests in same module.
Register roll/r, proll/privateroll without overwriting existing handlers, plus thebasics roll/proll. Bare input shows help.
Example boundary:
```csharp
var result = DiceEvaluator.EvaluateInput(input);
var text = DiceRollFormatter.Format(result, displayName, mode, isPrivate);
```
Default-on EnableDiceRolling with next unused ProtoMember and admin setting. Resolve once: current mode, recipients in same dimension/config range, names, and result. Preserve sender.
Private: requester message only, command telemetry. No public events/history.
Public: same result per recipient, explicit Roll event/history kind, canonical log+relay once, bubble payload only if eligible. Spectator guard by game mode independent of unmerged spectator changes.
Use a five-attempts-per-ten-seconds per-account burst guard and bounded cleanup via player disconnect lifecycle. Emit safe error messages.
Test private non-disclosure, aliases, mode and dimensions, disabled/help/errors, one evaluation and event, marker/escaping.

## Task 3: Bubbles and analytics

Extend SpeechBubbleVtmlPatches with an explicit dice kind and compact single-line dice motif+result rendering using Cairo, no formula layout or animations.
Use data separated from full chat text, preserve bubble modes, LOS, lifecycle and spectator suppression.
Plain dN shows sides inside die, otherwise generic die motif. W/Y marker and total/success count remain readable.
Add public mechanic usage and canonical command telemetry with no contents; matching relay enums/allowed properties and node tests. Private only roll/proll command fields.
Update API kind/history filters and analytics docs as needed.
Test format and privacy/allowlist exact contracts; manual bubble visuals remain owner QA.

## Task 4: Full validation and PR delivery

Run focused tests during slices, full suite once implementation integrates. Run package script with THEBASICS_LOCAL_MOD_DIRS pointing to resolved worktree staging folder and no root/mod .env, clearing inherited SFTP variables.
Inspect zip integrity and DLL hash. Prepare numbered human QA cards for commands/advanced mechanics/bubbles/private/Discord/config.
Run two-axis code-review on git diff origin/main...HEAD; fix every valid spec/standards finding. Commit/push branch, create PR against main.
Poll exact head terminal CI and every review/comment/thread/sticky summary. Respond/resolve addressed feedback before recycle push. Every new commit resets 30 minutes. Final post-window reread required.
Never claim merge-ready without required manual QA and acceptable checks. Request only concrete remaining human QA after automatic work is ready.
