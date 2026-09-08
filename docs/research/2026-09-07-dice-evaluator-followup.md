# Exact-version dice evaluator follow-up

Date: 2026-09-07. Scope now includes exploding dice, automatic success pools, general arithmetic, keep-high/low, and roll reasons. This supersedes the earlier small-parser recommendation for that broader scope. Research only: public source and NuGet metadata were read; no library installed, built, or executed.

## Recommendation

Use **DiceRoller 4.2.0 as the first adoption candidate**, behind a small The BASICs adapter with explicit input/output budgets. It covers the accepted mechanics and exposes deterministic RNG injection and a shared generated-dice limit. Final source validation below found zero-draw reroll and once-per-pool caveats, so it is not a drop-in adoption recommendation. RollCraft 0.1.6 requires more application machinery and has no native success-counting grammar in its published source.

This is a source-backed candidate selection, not a claim of verified runtime compatibility. Before implementation is approved, the design can rely on the verified API below. Before shipping, verify the pinned binary, dependency packaging, custom formatting, pathological-input rejection and game-server loading.

## What was pinned

| Artifact | Verified identity | Material distinction |
|---|---|---|
| DiceRoller NuGet 4.2.0 | GitHub release tag `4.2.0` resolves to `4f9e55e08fb9b8d922e50ad3da62f61c8b793571` | NuGet metadata does not embed a source commit. Tag-source analysis is not proof of binary/source reproducibility. |
| RollCraft NuGet 0.1.6 | Package nuspec embeds commit `684b1cd30d22a406ca46b2bb6be02f2583d4d33a` | Nuspec repository URL still says `hquinn/Dicer`; that commit resolves in hquinn/RollCraft. |
| DiceRoller current master | `3e74ba2fcc1919468561cf366fcca2162b620189` | Project declares 5.0.0 and netstandard2.0/net6.0. Do not substitute it for 4.2.0. |
| RollCraft current main | `126288a876e31078b50d410d60db4bf84e3c1290` | Project declares 1.0.0.0, net10/net9 and MonadCraft 0.15.0. It is materially newer than 0.1.6. |

Sources: [DiceRoller tag metadata](https://api.github.com/repos/skizzerz/DiceRoller/git/ref/tags/4.2.0), [DiceRoller package](https://api.nuget.org/v3-flatcontainer/diceroller/4.2.0/diceroller.4.2.0.nupkg), [RollCraft package](https://api.nuget.org/v3-flatcontainer/rollcraft/0.1.6/rollcraft.0.1.6.nupkg), [DiceRoller master project](https://github.com/skizzerz/DiceRoller/blob/3e74ba2fcc1919468561cf366fcca2162b620189/DiceRoller/Dice.csproj), [RollCraft main project](https://github.com/hquinn/RollCraft/blob/126288a876e31078b50d410d60db4bf84e3c1290/src/RollCraft/RollCraft.csproj).

The exact NuGet package metadata was inspected in memory, without loading assemblies. DiceRoller 4.2.0 is MIT and its netstandard2.0 asset requires Antlr4.Runtime.Standard >=4.9.2. RollCraft 0.1.6 is MIT, targets net8/net9 and requires MonadCraft >=0.13.0. The package files and dependency versions should be locked during adoption.

## DiceRoller 4.2.0

### Deterministic tests and server authority

`Roller.Roll(expression, config, data)` accepts per-call configuration. `RollerConfig.RollDie` is a public `Func<int,int,int>` receiving inclusive minimum/maximum values. It can return a predetermined sequence for precise tests; `RollNode` rejects out-of-range callback values. `GetRandomBytes` is a second injection seam. Default rolling uses cryptographic random bytes and rejection sampling. Prefer explicit per-call configuration, not mutation of the global `DefaultConfig`. The Min/Max helpers temporarily mutate the provided config's RollDie property, so shared config objects need care. [Roller](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/Roller.cs), [config](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/RollerConfig.cs), [roll implementation](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/AST/RollNode.cs).

### Limits, including the important silent-cap behavior

Defaults are MaxDice=1000, MaxSides=10000, MaxRecursionDepth=20 and MaxRerolls=100. `RollNode` checks the shared `AllRolls.Count` before generating another die, including extra rolls. Exceeding MaxDice throws. AST evaluation checks depth and cumulative roll counts. These are useful whole-roll guards, but the depth check occurs during evaluation, after ANTLR parsing/tree walking. Input length and pre-parser nesting/token limits are still needed. No cancellation/time-budget API was found in this release's public Roll entry point. [Config](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/RollerConfig.cs), [AST guard](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/AST/DiceAST.cs), [parser entry](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/Roller.cs).

MaxRerolls silently stops processing and returns a result. Moreover, despite config comments describing a per-die maximum, explosion and reroll implementations keep a counter across their processing of a roll expression. Do not describe that setting as a trustworthy per-die semantic limit. [Explosion loop](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/Builtins/ExplodeFunctions.cs), [reroll loop](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/Builtins/RerollFunctions.cs).

Initial adapter hypothesis, now restricted by the final validation below: for basic-die recursion where every iteration draws a die, set the silent safety ceiling above the shared MaxDice budget so exhausted recursive rolls throw before that ceiling can truncate them. This does not work for zero-draw group rerolls. Explicit once-only/player-requested bounded rerolls remain valid mechanics. Test this relation for each enabled modifier and combinations; do not claim it is proven solely by the config comments.

### Grammar and arithmetic semantics

The release grammar accepts an entire expression followed by EOF, with unary minus, parentheses, dice/groups, addition/subtraction and multiplication/division using ordinary precedence. Numeric results are decimal; division is not integer division. Mathematical helpers include floor, ceil, round, abs, min and max. General arithmetic here does not imply arbitrary code, modulo, exponentiation, or every calculator function. Dice counts and sides truncate decimal values in RollNode, so the product must either document that behavior or reject fractional counts/sides deliberately. [Grammar](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/DiceGrammarParser.g4), [math](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/AST/MathNode.cs), [math helpers](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/Builtins/MathFunctions.cs).

Comparisons attached to rolls count successes, not a comparison of the summed total. Release tests specify `4d6>=5` as count faces >=5, `4d6>=5f1` subtracts failures on 1, and dropped dice do not contribute. Adding an ordinary total-producing roll to a success pool changes the result type to Total. A modifier +2 added to a success pool adds two successes. The UI must label the result type correctly. [Success grammar tests](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/TestDiceRoller/Grammar/SuccessRollShould.cs).

Modifier execution follows registered timing: explode, reroll, keep/drop, success/failure, critical/fumble, sort. It is not simply left-to-right typed order across modifier categories. [FunctionTiming](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/FunctionTiming.cs).

### Result extraction and stable boundaries

Public `RollResult` exposes Value, ResultType, Expression, NumRolls and Values. Each DieResult includes face value, sides, kind, flags and optional data. Values also contains structural markers such as operators and parentheses, so it is not an array to blindly sum. Flags distinguish success/failure, dropped and extra rolls; Extra combines explosion/reroll provenance rather than distinguishing them. Reroll output retains dropped earlier faces. Arbitrary nested dice-count expressions are not fully represented in Values, so full nested evaluation narration would require AST traversal or a separately captured draw trace. [RollResult](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/RollResult.cs), [DieResult](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/DieResult.cs), [flags](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/DieFlags.cs).

Release notes explicitly exclude concrete AST subclasses from API stability and deprecate direct parser/AST access. Keep the adapter on public result/config types. Custom display should not reuse its forum-oriented binary persistence helpers. [4.2.0 notes](https://www.nuget.org/packages/DiceRoller/4.2.0).

## RollCraft 0.1.6

The package-pinned evaluator exposes `CreateCustom(IRoller)`, `CreateSeededRandom(int)` and separate Parse/Evaluate calls. Deterministic face-sequence tests are straightforward. Parsed numeric support is only int or double; generic type syntax does not mean decimal parsing is supported. Division uses the chosen numeric type. [Evaluator](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/DiceExpressionEvaluator.cs), [parser](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/DiceExpressionParser.cs), [divide](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/Nodes/Divide.cs).

The exact source has no native success-pool node/modifier. Its comparison handler rejects general prefix/infix comparisons; comparison support belongs to applicable dice modifiers. Implementing automated pools would require additional integration beyond this evaluator. Current-main README features such as variables, conditionals, broader math and newer parser-depth guards are not evidence for the 0.1.6 package. [Published source tree](https://github.com/hquinn/RollCraft/tree/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft), [comparison handler](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/TokenHandlers/ComparisonTokenHandler.cs).

There is no shared MaxDice/MaxSides or configurable expression-depth budget in the inspected package implementation. Explosion silently breaks after more than 1000 additional results; rerolls silently stop after 1000 iterations per die. A custom IRoller can stop excessive generated dice, but cannot alone bound parsing or non-random operations. Keep uses a variable-sized stackalloc based on keep count, reinforcing the need for application bounds. [Dice node](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/Nodes/Dice.cs), [exploding](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/Modifiers/Exploding.cs), [reroll](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/Modifiers/ReRoll.cs), [keep](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/Modifiers/Keep.cs).

Results expose a numeric result and DiceRoll list with sides, face and flags. Reroll mutates the same face value and sets Rerolled, losing previous face values from that result list. Thus a rich audit trail needs separate RNG tracing; flags alone are insufficient. AST node types are internal. [Result](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/DiceExpressionResult.cs), [DiceRoll](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/DiceRoll.cs), [reroll implementation](https://github.com/hquinn/RollCraft/blob/684b1cd30d22a406ca46b2bb6be02f2583d4d33a/src/RollCraft/Modifiers/ReRoll.cs).

## Adoption checks still outstanding

- Exact-package runtime loading on the target Vintage Story .NET runtime and distribution of DiceRoller plus Antlr DLLs. Current The BASICs packaging does not collect dependency DLLs automatically.
- Deterministic combined-feature tests: exploding then keep then success counting; ties; failure conditions; mixed totals/pools; malformed suffixes; arithmetic overflow/division by zero; invalid/fractional dimensions.
- Input length, nesting and output limits before/after evaluation. Reject capped/error rolls without publishing partial results, and preserve a single completed result for game chat, logs and Discord.
- Decide which built-in functions/macros remain exposed. Broad arithmetic does not require opening every library feature or custom callback facility to players.
- Keep reasons, character attribution, audience policy and Discord formatting outside the evaluator. Evaluator choice does not resolve audience behavior.


## Final source validation: adoption remains conditional

A targeted follow-up invalidated any broad claim that setting MaxRerolls above MaxDice prevents silent truncation for the entire grammar. This correction supersedes the earlier adapter-policy suggestion wherever group expressions are allowed.

`{1}rr=1` and `{0d6}rr=0` can repeatedly reevaluate a group without generating any dice. The shared generated-dice count does not advance; the reroll loop eventually reaches its silent ceiling and returns a result. Sources: [GroupNode](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/AST/GroupNode.cs), [FunctionContext](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/FunctionContext.cs), [RerollFunctions](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/Builtins/RerollFunctions.cs).

A second semantic caveat: `2d6ro=1` with face sequence [1,1,4] yields 5, replacing only the first eligible die. The once counter is shared across the pool. Do not document it as once per eligible die. Sources: [RerollData](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/Builtins/RerollData.cs), [reroll loop](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/DiceRoller/Builtins/RerollFunctions.cs).

The canonical explosion shorthand in this pinned release is `!e`. Do not assume bare `!` has the intended semantics. Explicit function syntax is useful for unambiguous combined-feature test cases. [Explosion grammar tests](https://github.com/skizzerz/DiceRoller/blob/4f9e55e08fb9b8d922e50ad3da62f61c8b793571/TestDiceRoller/Grammar/ExplodeRollShould.cs)

**Revised recommendation:** DiceRoller 4.2.0 remains the strongest evaluated off-the-shelf fit for the accepted mechanics, but it is not approved as a drop-in evaluator. Choose and test one of these approaches during design:

- Restrict recursive reroll modifiers to basic dice pools, retaining general arithmetic, explosion and automated success pools. Verify all equivalent shorthand/function paths before claiming enforcement. Merely checking that a group contains dice is insufficient because it may generate zero dice.
- If arbitrary group rerolls are wanted, evaluate a targeted library change or an adequate public hook that detects cap exhaustion and counts evaluation operations, including zero-draw work. No such complete hook was verified in this release.
- Choose explicit once-per-pool semantics, or adapt the implementation if once-per-die is desired. Do not silently promise conventional behavior the chosen package does not provide.

No narrowing has been accepted by the owner. This does not remove exploding dice, success pools or general arithmetic from the agreed feature scope. It exposes exact semantics for the next design decision.

See [deterministic acceptance cases](2026-09-07-dice-acceptance-cases.md). All new examples are source-derived and unexecuted; exact-package execution and Vintage Story loading remain future verification.
