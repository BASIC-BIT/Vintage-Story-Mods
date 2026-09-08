# Dice expression libraries for The BASICs

Research date: 2026-09-07. Scope: online primary-source comparison for server-generated chat/command rolls. No dependencies installed, code implemented, or runtime compatibility claimed. Library behavior below is documented behavior, not independently executed behavior. Web results include cached pages; release evidence is identified precisely rather than treated as proof of present maintenance.

## Recommendation

For a small first release, a deliberately bounded C# parser remains reasonable if the agreed grammar stays near `NdS`, signed modifiers, and perhaps keep-highest/lowest. If the desired feature set includes mixed expressions, repeated rerolls, exploding dice, success pools, or expression composition, evaluate **skizzerz/DiceRoller** before inventing that language ourselves. **RollCraft** is the second .NET candidate, with a material published-package versus main-branch documentation mismatch to resolve first.

Use the JavaScript candidates as grammar and result-display references. Adding a JavaScript runtime or separate Node process solely to roll dice is an integration cost that this feature does not currently justify. A Discord renderer is not a Discord transport: whichever evaluator we choose, the server should roll once and send the same established result to game chat and the existing relay.

These are design judgments based on the comparisons below, not a final dependency choice.

## Candidate comparison

| Candidate | Useful capabilities | Runtime, license and evidence | Fit |
|---|---|---|---|
| [skizzerz/DiceRoller](https://github.com/skizzerz/DiceRoller), NuGet `DiceRoller` | Expressions, detailed results, functions, rerolls, explosion variants, success/failure conditions | MIT; package 4.2.0 targets .NET Standard 2.0 and .NET Framework 4.5.2. Standard target depends on `Antlr4.Runtime.Standard >=4.9.2`. NuGet and GitHub show 4.2.0 dated 2021-06-15. | First full .NET evaluator to investigate; published release is old. |
| [hquinn/RollCraft](https://github.com/hquinn/RollCraft) | Arithmetic, keep, explosions, rerolls, clamping; current main README also describes variables, conditionals, custom rollers and detailed modifier flags | MIT; NuGet shows 0.1.6 dated 2025-08-27, targeting net8.0/net9.0 with MonadCraft >=0.13.0. | Promising modern C# API; confirm exact package functionality and bounded execution. |
| [RPG Dice Roller](https://github.com/dice-roller/rpg-dice-roller), `@dice-roller/rpg-dice-roller` | Rich modifiers, expressions, roll breakdowns and export; interchangeable RNG engines | MIT; inspected develop manifest says 5.5.0, Node >=18, runtime dependencies mathjs and random-js. | Strong reference for semantics, test cases and readable outputs. |
| [3d-dice/dice-roller-parser](https://github.com/3d-dice/dice-roller-parser), `@3d-dice/dice-roller-parser` | Roll20-style parsing, separate parse/evaluate methods, structured results, sample Discord renderer | MIT; inspected master manifest says 0.2.6, TypeScript declarations, no runtime dependencies declared. | Helpful reference for separating roll data from presentation. |

Package/manifest sources: [DiceRoller NuGet](https://www.nuget.org/packages/DiceRoller), [RollCraft NuGet](https://www.nuget.org/packages/RollCraft), [RPG Dice Roller manifest](https://raw.githubusercontent.com/dice-roller/rpg-dice-roller/develop/package.json), [3d-dice manifest](https://raw.githubusercontent.com/3d-dice/dice-roller-parser/master/package.json).

### DiceRoller: useful breadth, unresolved execution details

The official API exposes `Roller.Roll` and a result containing total and dice. Nested expressions can require deeper tree inspection to recover all intermediate rolls. Configuration is exposed through `Roller.DefaultConfig`, including dice limits. Exact limit defaults, an RNG injection seam, and whole-expression execution bounds were not verified because the detailed API/source pages did not load. These remain adoption questions, not evidence that the library lacks them. [API documentation](https://www.skizzerz.net/DiceRoller/API).

The 4.2.0 notes include bounded reroll counts, once-only explosion variants, removal/override of built-in functions, and warn that concrete AST subclasses are implementation details. A restricted public grammar should not depend on private AST types being stable. [Package release notes](https://www.nuget.org/packages/DiceRoller).

### RollCraft: distinguish main from the distributable package

The main README describes custom `IRoller` injection, reproducible seeded rolls and result flags recording dropped/rerolled/exploded dice. It also describes indefinitely repeating rerolls. No evaluation budget or cancellation facility was established in the inspected documentation. A bounded custom RNG would cap random draws, but would not by itself bound parsing, recursion or non-random expression work. [Main README](https://github.com/hquinn/RollCraft).

The NuGet 0.1.6 README lists only int/double support and a smaller syntax surface than main. Do not promise current-main features as available in that published package. The newest commit date could not be established from the web history page; the published version date is the maintenance evidence available here. [Published package](https://www.nuget.org/packages/RollCraft).

### RPG Dice Roller: explicit guards still need application budgets

Default randomness comes from Math.random; documented alternatives include Node/browser cryptographic engines and custom engines. Keep server ownership regardless of engine: clients submit notation, never authoritative results or chosen seeds. [RNG documentation](https://dice-roller.github.io/documentation/guide/customisation.html).

Exploding, compounding, penetrating, reroll and unique modifiers have a documented 1,000-iteration cap per initial die. This avoids specific infinite loops, but many initial dice or chained modifiers can still multiply work. A per-die guard is not a small whole-command budget. [Modifier documentation](https://dice-roller.github.io/documentation/guide/notation/modifiers.html).

The inspected develop history shows dependency/build fixes and a release-branch merge in February 2025, followed by a documentation merge on March 31, 2025. This establishes real maintenance activity at those dates, not a current responsiveness guarantee. [Commit history](https://github.com/dice-roller/rpg-dice-roller/commits/develop/).

### 3d-dice parser: useful separation and naming caveat

The README provides a custom random function and configurable `maxRollCount`, defaulting to 1,000 rolls per die. `parse` and `rollParsed` separate parsing from evaluation, and `DiscordRollRenderer` formats rolled data into Markdown. These are useful architectural examples; they do not send messages or establish compatibility with Th3Essentials. [README](https://github.com/3d-dice/dice-roller-parser).

The README still contains unscoped installation examples, while its manifest identifies the scoped package. Pin an exact package identity if using it. Latest commit/publication date remained unknown; the fetched GitHub release page was empty, which does not establish inactivity or absence of npm publications. [Manifest](https://raw.githubusercontent.com/3d-dice/dice-roller-parser/master/package.json), [release page](https://github.com/3d-dice/dice-roller-parser/releases).

## Packaging and robustness requirements for either route

Local source inspection confirms `mods-dll/thebasics/thebasics.csproj` targets net10.0. The current `scripts/package.ps1` explicitly packages modinfo, thebasics.dll, thebasics.pdb and assets. It does not collect third-party dependency DLLs. A NuGet reference alone would not produce a complete distributable mod; dependency packaging and game-loader verification are part of adoption cost.

Recommended invariants, irrespective of library choice:

1. Validate the entire expression before any authoritative result is published. Reject unsupported trailing syntax rather than accepting a prefix.
2. Bound input length, expression depth/terms, sides, initial dice and total generated dice. Repeating operations share one command budget. Reject overflow and non-finite arithmetic.
3. A budget failure produces an error, never a silently truncated roll represented as a valid result. Do not automatically retry a failed roll.
4. Keep original/canonical notation, individual dice, kept/dropped/rerolled status and total together. Render that same result for each destination.
5. Keep player labels/reasons outside the expression evaluator, and escape output for both game markup and Discord formatting/mentions.
6. Test representative successes plus malformed input, huge counts, nesting, impossible reroll/explosion conditions, and budget exhaustion before enabling broad grammar.

These are proposed engineering requirements. Numeric product limits should be chosen after the desired player features are agreed. The straightforward first-release parser should intentionally omit general math functions, macros, variable lookup, conditional expressions and recursive modifiers unless real demand makes their cost worthwhile.

## Naming correction

Rolisteam's well-known [DiceParser](https://github.com/Rolisteam/DiceParser) is C++/Qt, GPL-3.0, and powers its own chat/bot integrations. It is a useful feature reference, but is not a C#/.NET dependency. Generic searches for “DiceParser” or “Dice.NET” should not be treated as verified package identities.
