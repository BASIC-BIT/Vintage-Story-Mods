# Dice rolling

Use /roll or /r followed by an expression. A public roll follows your current whisper, normal, or yell range in the same dimension, even while OOC or global OOC is selected. Whispered results start with (W), yelled results with (Y). Numbers are never scrambled by languages or speech effects.

Use /proll or /privateroll for a result visible only to you, labelled [Private Roll]. Private roll contents do not enter The BASICs shared history, chat/audit logs, bubbles, processed-message events, or Discord. Normal command-use analytics may count the attempt when analytics is enabled.

If another mod owns a short command, use /thebasics roll or /thebasics proll. A bare command shows help. Dice commands require the existing chat privilege.

## Expressions

| Example | Meaning |
| --- | --- |
| d20 | One twenty-sided die |
| 2d6+3 | Two six-sided dice plus three |
| 4d6kh3 | Keep the highest three |
| 2d20kl1 | Keep the lowest one |
| 4d6dh1 or 4d6dl1 | Drop the highest or lowest one |
| 3d6! | Explode each maximum face, adding another die |
| 4d6r=1 or 4d6ro=1 | Reroll each eligible die once |
| 4d6rr<3 | Reroll eligible dice repeatedly, within the roll budget |
| 5d10>=8 | Count retained faces that are at least eight |
| floor((2d6+3)/2) | Arithmetic and rounding |

Comparisons support >, >=, <, <= and =. Modifiers always run in this order: explode, reroll, keep/drop, then count successes. Rerolled maximum faces do not start exploding again.

Arithmetic supports +, -, *, /, parentheses, decimals, floor(), ceil(), and round(). Round sends exact halves away from zero. Success-pool arithmetic with constants remains labelled successes; mixing an ordinary dice total with a success pool produces a numeric total. No automatic narrative critical-success or failure labels are applied.

Add a reason naturally when unambiguous, such as /r d20 climbing, or separate it with # or //, such as /r d20+2 # forcing the gate. Incomplete arithmetic and ambiguous suffixes produce an error, not a partial roll. Dice counts and side counts must be positive whole numbers.

## Presentation and limits

Chat includes the expression, result, reason, and face breakdown. A plain dN expression gets a numbered die icon beside its result in the overhead bubble; other expressions get a wireframe die and total or success count. Private rolls and active spectators produce no entity-attached roll cue. Existing bubble mode, lifetime, visibility and line-of-sight rules still apply.

Limits are 512 input characters, 256 parser tokens, 32 recursive parsing levels, 100 drawn dice including replacements/explosions, 1,000,000 sides, 2,048 evaluation work steps, and 4,096 breakdown characters, and 1,900 characters in the complete rendered result including name and reason. This prevents truncation by the Discord bridge. Each account can attempt five rolls per ten seconds. An exceeded limit reports an error without publishing a partial result.

## Server settings and compatibility

EnableDiceRolling defaults to true and controls public and private commands independently of OOC settings.

Public results use the existing EnableTh3EssentialsDiscordRelay setting. A Discord relay makes a scene roll visible to that Discord destination regardless of its in-game range. There is one completed result and one relay event, with no reroll for Discord. Roll output neutralizes Discord mention syntax. Delivery still depends on the installed Th3Essentials bridge and Discord configuration.

Aggregate public telemetry reports only bounded mechanics, complexity and mode. Private rolls report normal command usage only. The updated analytics relay contract is revision 6; deploying that schema is a separate operational step.

This feature supplies no physical dice, GM recipients, character-stat lookup, macros or scripting.
