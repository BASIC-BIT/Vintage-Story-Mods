# Dice rolling: RP culture and gaming needs

Research date: 2026-09-07. Research and feature exploration only. No implementation or runtime verification. The owner confirmed chat/command rolls and wants Discord relay compatibility; physical dice are outside current scope. Companion documents: [VS competitive analysis](2026-09-07-dice-rolling-competitive-analysis.md) and [local integration investigation](2026-09-07-thebasics-dice-integration.md).

## Recommendation

Build the first experience around **a shared, contextual roll that people can interpret using their own rules**: dice notation, signed modifiers, an optional reason, per-die results, character attribution, and the same result in Discord when the server enables that relay. Treat reasons as first-version functionality, because they make simultaneous rolls and later Discord reading understandable with very little interaction cost.

The strongest additional mechanic is **keep highest / keep lowest**, including familiar advantage/disadvantage examples. It serves several styles of play without choosing a character system. Ask which actual house rules the requesting servers use before promising pools, Fate dice, exploding dice, or GM workflows. Popular tools supporting a mechanic demonstrates precedent, not demand among The BASICs players.

Example syntax throughout this document is proposed, not implemented or an approved command name.

```text
/dice 2d6+3 # forcing the cellar door
[Roll] Mira: forcing the cellar door | 2d6+3: [4, 5] + 3 = 12
```

## What established systems demonstrate

| Primary source | Documented convention | Design implication for The BASICs |
| --- | --- | --- |
| [Roll20 Dice Reference](https://help.roll20.net/hc/en-us/articles/360037773133-Dice-Reference) | Basic notation, arithmetic, per-die output, descriptive text, keep/drop, exploding dice, and counting successes. Its `>` comparator means greater-than-or-equal, which differs from ordinary programming notation. | Familiar syntax matters, but copying a grammar silently imports semantics. Help and examples must state exact behavior. |
| [Foundry Basic Dice](https://foundryvtt.com/article/dice/) | Whole-roll descriptions after `#`; expandable individual results; public, GM, blind, and self visibility modes. | Reasons and readable results are established usability features. Distinct audiences are real use cases, but a server admin is not automatically the GM for every scene. |
| [D&D SRD 5.2.1](https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf) | Advantage rolls two d20s and keeps the higher; disadvantage keeps the lower. | `2d20kh1+3` and `2d20kl1+3` are more precise implementation primitives than an undefined generic "advantage" switch. |
| [Fate: Taking Action, Dice, and the Ladder](https://fate-srd.com/fate-core/taking-action-dice-ladder) | Four Fate dice have -1, 0, +1 outcomes; add a skill rating. Opposition can be another character's roll or a fixed rating. | Fate dice need a different face distribution. Opposed play can work with two ordinary attributed rolls before any challenge workflow exists. |
| [Blades in the Dark: Core System](https://bladesinthedark.com/core-system) | Roll d6s and read the highest; multiple sixes matter. With zero dice, roll two and take the lowest, with no critical. | A pool's sum is not always its meaningful result. Keep every original face visible, even when showing an aggregate. |
| [Blades: Action Roll](https://bladesinthedark.com/action-roll) | Establish the goal, rating, position and effect before rolling; outcomes can include success with consequences; narration is collaborative. | Purpose text helps situate a result. A general roller should not declare a universal victory, injury, or consequence. |

These are examples from distinct systems, not a proposal to implement those systems or their character sheets. Fate SRD is publisher-endorsed; Roll20 and Foundry are first-party tool documentation. Current pages were retrieved online; no gameplay or installed version behavior was tested.

## Scenarios and priorities

Priorities are product recommendations, not surveyed player requirements.

| Scenario | Smallest useful support | Priority |
| --- | --- | --- |
| Two characters contest an arm wrestle | Each rolls a die plus an agreed modifier, with a reason identifying the contest. Players compare results and settle ties. | First version through generic rolls. |
| A storyteller asks whether a risky climb works | Dice plus modifier and a purpose; participants decide what the result means. | First version. |
| A tavern plays a simple dice game | Multiple dice, every face visible, repeated commands. A coin can initially be `d2` with an agreed mapping. | First version through generic rolls. |
| An event runner chooses from a numbered encounter list | `dN`, including nonstandard side counts within bounds. The runner reads their own table. | First version; no table editor needed. |
| Players reading Discord need to follow a scene | Name, reason, formula, all faces, and total survive plain-text relay together. | First version because the owner requested it. |
| A house rule grants an extra chance | Keep-high/low, explicitly showing used and discarded faces. | Best next extension; plausible initial scope if parser support is already sound. |
| Character creation uses four d6, discard the lowest | Generic keep/drop can express it. | Same extension, without a character builder. |
| A pool counts dice meeting a threshold | Success counting with explicit comparison semantics and visible faces. | Add for a named server ruleset. |
| A group uses Fate/Fudge | `4dF+modifier`, showing signed faces. | Add for demonstrated use. |
| A house rule uses exploding dice or rerolls | Mark additional/replaced dice and distinguish those mechanics; bound evaluation. | Add for demonstrated use. |
| A GM checks something without players seeing the result | Explicit private/blind audience with defined GM membership and relay exclusion. | Defer until there is a concrete staff/scene workflow. |
| Large events run many opposed checks | A shared reason is enough initially; challenge IDs, acceptance, turn tracking and automatic winners are separate workflow scope. | Defer workflow automation. |

Simple dice plus visible individual faces already let players manually interpret many pools. This is useful fallback coverage, not a claim of automated system support. A highest/lowest result alone would lose information such as multiple sixes in Blades.

## RP culture, identity, and audience

The repository's [RP culture skill](../../.opencode/skills/rp-culture/SKILL.md) records that local OOC supports scene coordination, global OOC supports server-wide coordination, and server operators often configure chat to preserve the fiction. It also separates spectator communication from embodied cues. Applying those recorded principles to dice is an inference:

- Treat a roll as scene meta-information. It should stay legible regardless of the character's accent or language, and should not produce speech chatter or require the character to say the numbers aloud.
- Keep dice availability independent from OOC toggles. A server can want rolls while disallowing unrelated forms of chat.
- Reuse deliberate character naming rules for scene output; retain accountable operator identity in staff records. Duplicate RP names and NPC portrayal deserve concrete examples before adding new identity controls.
- Spectator staff may need to roll while running a scene. Do not attach passive above-head cues to their hidden body. Avoid inventing a full NPC identity picker for the initial roller.
- A public result does not confer authority over someone else's character. Participants or their chosen rules decide stakes and consequences. This needs neutral output, not consent dialogs, mandatory confirmations, or policing ordinary rolls.

For illustration, `[Roll] Mira: persuade Rowan | d20: [20] = 20` reports a roll. `Mira forces Rowan to agree!` decides another character's response. The latter is outside a system-neutral utility. Do not universally label maxima "critical success": rules differ by system and roll type.

## Discord implications

The separate integration investigation found an existing Th3Essentials relay seam, but this document does not verify its runtime behavior. Design acceptance criteria should include:

1. One in-game roll produces one Discord result, retaining its purpose and all mechanically relevant dice. No separate Discord reroll.
2. Plain text carries meaning without depending on color, hover expansion, or an image. For keep/drop, use explicit wording such as `rolled [7, 16], kept [16] + 3 = 19`.
3. Server documentation states that enabled relay extends the audience beyond nearby players. Calling a roll "local" must not imply that its Discord copy is local too.
4. Any future private/blind rolls must be deliberately excluded from a general relay. Do not introduce those modes until recipients and relay behavior are defined.

Readable Discord context is more valuable for this request than a dedicated dice tab or cosmetic roll effects. The needed runtime check is an actual command-to-local-clients-to-Discord path, not simply proving that a relay method was called.

## Questions worth taking back to requesting servers

These should sharpen a later design discussion, not block continued technical research:

- What are three roll commands they actually expect to use, including their modifiers or house rules?
- Are rolls mainly opposed player scenes, storyteller-run checks, tavern games, or a mixture?
- Should all ordinary nearby scene rolls appear in the existing Discord RP channel, or do they want a distinct destination?
- Do they have an actual secret-roll use case, and who is the intended GM for it?

Do not infer demand from D&D familiarity alone. The addon already demonstrates demand for a native-feeling scene roll, while reasons, keep/drop, and unusual dice need to be evaluated against the communities asking for this feature.
