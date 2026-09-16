# Dice rolling competitive analysis

Research date: 2026-09-07. Scope: The BASICs feature exploration, not implementation or deployment. Sources are publisher listings, publisher documentation, and linked upstream repositories. Mod behavior was not tested in game. Search/open responses include cached snapshots; versions below are the latest **advertised in the retrieved page**, not verified compatibility with today's client. Relative publication dates are deliberately not converted into calendar dates.

## Assessment

Dice rolling already has both standalone and The BASICs-specific implementations. The strongest opportunity is a small integrated RP utility: a server-produced result, intelligible character attribution, predictable nearby recipients, and a concise expression/result breakdown. This is a product inference from the alternatives below, not a claim that all competitors lack these properties.

Proximity alone is not a differentiator: EternalDice explicitly advertises it. One operator's reported Roll command experience nevertheless demonstrates why integration matters: writing to a tab named Proximity does not necessarily apply that chat system's recipient rules. Physical dice are a separate tavern/tabletop experience already served by Tabletop Games. [Roll command](https://mods.vintagestory.at/roll), [EternalDice](https://mods.vintagestory.at/eternaldice), [Tabletop Games](https://mods.vintagestory.at/tabletopgames)

## Vintage Story alternatives

| Alternative | Published interaction | Audience and RP fit | Retrieved maintenance evidence / limits |
| --- | --- | --- | --- |
| **Roll the Dice - A TheBasics Addon**, Tisma / LesNaufragesVS | Client command `.roll 2d6` or `.roll 3d20`; results sent through The BASICs `/it`; multilingual. | Direct evidence of demand for an integrated RP roll. Its advertised transport publishes result text through the environmental narration command. Actual generation and validation implementation is unverified here. | Latest listing: **0.3.1**, VS **1.20.3**, February 12, 2025. In a September 19, 2025 reply, the author said they had not touched mods in a long time and believed the dependency declaration might be missing. Current compatibility and license unknown. [Publisher listing and author replies](https://mods.vintagestory.at/show/mod/14788) |
| **Roll command**, Ginox | `/roll` defaults to 1-100 in examples; also `/roll MAX`, `/roll MIN MAX`, `/roll d20`, `/roll 2d20`. Dice rolls highlight extrema; configurable maximum dice count **10**, sides **100**, global-channel protection and colors. | Server-side. On April 22 the author explicitly said proximity was unsupported and recommended private groups. A server operator reported all Proximity members receiving rolls, including distant players. This is a reported case, not our reproduction. | Latest listing: **1.0.1**, advertised VS **1.21.0-pre.1 through 1.22.0**. Single listed release and a proposed proximity improvement do not establish that improvement shipped. [Publisher listing and discussion](https://mods.vintagestory.at/roll) |
| **EternalDice**, DeanBro | `/dice` and `/d`; bare command rolls d6, `/dice 2` rolls two d6, `/dice 3d12` rolls three d12. | Server-side; advertises **6-block radius**, **3-second cooldown**, configurable settings. Built for Eternal Seraphim. | Latest retrieved listing: **1.0.1**, VS **1.22.6**, localization added. Page supplied relative dates; no absolute release date inferred. Source code, RP-name handling, precise distance semantics, server RNG path and license unverified. [Publisher listing](https://mods.vintagestory.at/eternaldice) |
| **slimes simple rp dice**, cannibalgirl666 | Dedicated dice tab, roll reasons, coin flips, advantage/disadvantage and sounds in 1.0.1. Exact command grammar not documented in retrieved text. | Both sides; made for Lunaria RP. Audience/radius and attribution are unknown. | Latest listing: **1.0.1**, VS **1.21.6**, April 1 (year omitted in page). This already covers several plausible second-phase features. Source and license unverified. [Publisher listing](https://mods.vintagestory.at/slimesrpdice) |
| **Tabletop Games**, DanaCraluminum / Moby_ | Craftable in-world dice, dice trays, boards and pieces. Lists d4/d6/d8/d10/percentile/d12/d20. Release history describes randomization when placed/dropped and optional animation. | Both sides; physical table play, with Attribute Rendering Library required. Publisher recommends Click To Pick to avoid accidental dice pickup. Chat audience, authenticated roll history and command syntax not established. | Latest listing: **4.0.0**, VS **1.22.0 through 1.22.3**, July 15 (year omitted). Repeated release history is evidence of maintained releases, not proof of current compatibility. [Publisher listing](https://mods.vintagestory.at/tabletopgames) |

License observations only: Roll command's linked repository identifies **GPL-3.0**; Tabletop Games' linked repository displays an **MIT** license. Neither fact requires reusing their code, and this brief makes no license-compatibility judgment. [Roll command repository](https://github.com/GinoxXP/vintage-story-roll-command), [Tabletop Games repository](https://github.com/Craluminum-Mods/TabletopGames)

## Adjacent interaction patterns worth borrowing

**Roll20:** Standard `/roll NdS+modifier` syntax, individual die values and a total, optional purpose text, and `/gmroll` for the roller plus GM. Its larger grammar supports extensive tabletop mechanics, but that breadth is not evidence The BASICs needs them. The useful baseline is a readable arithmetic receipt that other players can inspect. [Roll20 Dice Reference](https://help.roll20.net/hc/en-us/articles/360037773133-Dice-Reference)

**Foundry:** Explicit public, GM, blind and self roll modes make the audience a visible decision. It also supports purpose labels and expandable per-die results. Its basic dice page and chat page disagree about whether bare `/roll` follows selected mode or is always public; avoid relying on that detail without version-specific verification. The pattern to borrow is clear recipient semantics, not necessarily all four modes. [Foundry Basic Dice](https://foundryvtt.com/article/dice/), [Foundry Chat Messages](https://foundryvtt.com/article/chat/)

**Trust:** Roll20 describes server generation and signed results verified by clients. This is a benchmark for separating a generated result from ordinary user-authored chat, not a recommendation to build quantum randomness or cryptographic signing into a hobby-server mod. No comparable verified trust property was established for the VS competitors in this pass. [Roll20 Quantum Roll](https://help.roll20.net/hc/en-us/articles/360037256594-Quantum-Roll)

## Proposed product boundary

These are recommendations to evaluate against The BASICs code and owner preferences, not agreed requirements:

1. Start with one server command, a bounded `NdS` expression and an optional signed integer modifier. Make help show a few examples. Decide the bare-command default explicitly; competitors differ between d6 and d100.
2. Publish one short result containing the RP identity appropriate to that recipient, canonical expression, individual dice and total. Example: `Mara rolls 2d6+3: [4, 2] + 3 = 9`. Display should distinguish a system roll from normal player narration.
3. Make nearby visibility the default and define the actual recipient filter, including dimension boundaries. Do not infer locality from a channel ID or silently broadcast to the entire proximity group. Ensure the roller receives their own result.
4. Generate results on the server; accept a request, not a client-supplied result. Bound dice count, sides, input size and rate. The addon transport motivates checking this boundary, but its exact implementation must be inspected before calling it exploitable.
5. Keep game-system meaning with players. A highest face can be visibly highlighted without declaring every maximum a universal critical success. Avoid automatically altering combat, skills, outcomes or consent.
6. Treat reasons, coin flips and advantage/disadvantage as validated competitive ideas for later scope, not prerequisites. Defer dice tabs, physical items, GM workflows, arbitrary expressions, macro engines and statistics unless actual use requires them.

## Build versus integrate

**Explore a native implementation** if consistency with The BASICs names, recipients and message presentation is the main goal. **Improve an existing addon** if its present architecture is sound and maintenance/reuse are viable. The direct addon means this decision should precede implementation, not be discovered after shipping a competing command.

Compatibility checks before a design is approved: inspect the addon's source link and current package metadata; identify command registration conflicts with `/roll`, `/dice` and `.roll`; confirm whether a legacy `.roll` keeps sending `/it` alongside the native feature; inspect all local message paths before claiming native rolls are visibly trustworthy. This pass did not install packages, contact authors, evaluate source implementation, or establish interoperability.

Discovery was selective rather than exhaustive. We do not infer missing features from sparse documentation, use download counts as quality evidence, or equate stale compatibility declarations with broken mods.
