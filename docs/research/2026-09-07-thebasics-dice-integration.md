# Dice rolling in The BASICs: integration investigation

Research only, 2026-09-07. Source snapshot: local HEAD `c11dba7` plus existing uncommitted chat/spectator work. No implementation, build, deployment, or manual QA performed. Findings about that work are checkout observations, not released behavior. Companion: [competitive analysis](2026-09-07-dice-rolling-competitive-analysis.md).

Follow-up owner direction: chat/command rolling is sufficient; physical dice are out of current scope. Discord output through Th3Essentials is desired. Further research covers [libraries](2026-09-07-dice-libraries.md), [RP feature needs](2026-09-07-dice-rp-feature-needs.md), and [Discord delivery](2026-09-07-dice-discord-relay.md). Earlier alternatives and questions below are historical hypotheses, not a request to reconsider physical dice.

## Product hypothesis

The strongest fit is a shared, system-neutral roll for people participating in a roleplay scene. Physical dice and tabletop simulation are a separate scope. Existing competitors must inform the decision to incorporate, maintain an addon, or build a small native feature; the comparison is not evidence that an existing addon is broken.

Proposed first experience, subject to owner discussion:

```text
/dice 2d6+3
[Roll] Mira rolled 2d6+3: [4, 5] + 3 = 12
```

Support a deliberately small grammar such as `d20`, `2d6`, and `2d6+3`/`2d6-1`. A bare-command default and optional reason remain design choices. Use a canonical namespaced fallback (`/thebasics dice`) and decide a short alias after checking coexistence with installed rollers. Do not assume ownership of `/roll`.

Recommended initial behavior: server computes once; local participants see the same expression, individual results, modifier, and total. Use an identifiable system result format with escaped names/text. This improves attribution but is not cryptographic proof or a guarantee that screenshots cannot be forged. Keep real account identity in staff history and use the existing RP identity resolver for scene presentation.

No automatic success/failure, character-stat interpretation, combat resolution, wagering, macros, or dice physics in the initial hypothesis. These introduce rules or assets beyond the shared-roll need. Advantage/disadvantage is a reasonable later addition if the owner's game systems need it.

## Verified local integration seams

- [RPProximityChatSystem](../../mods-dll/thebasics/src/ModSystems/ProximityChat/RPProximityChatSystem.cs) registers player commands, owns the proximity group, records history, and publishes processed-chat events. Existing commands use player/privilege requirements. No dice command was found in the inspected The BASICs source.
- [TransformerSystem](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Transformers/TransformerSystem.cs) has sender processing, recipient selection, per-recipient formatting, history/logging, speech dispatch, and chatter dispatch. Reusing the whole ordinary-speech path without explicit dice treatment could change the result's presentation or invoke unrelated language behavior. Generate randomness before recipient iteration.
- [RecipientDeterminationTransformer](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Transformers/RecipientDeterminationTransformer.cs) uses chat mode ranges, a Manhattan distance boundary, language-specific delivery, and audible-speech occlusion. Global OOC short-circuits to all online players. A scene roll should not silently inherit a parked global OOC type, signing language, or speaking mode. Proposed default: normal local range, with numerical results fully readable for admitted recipients. The exact audience rule remains unapproved.
- [NameTransformer](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Transformers/NameTransformer.cs) already resolves nickname, character-sheet full name, then account name, and escapes name markup. Reuse this identity behavior deliberately rather than duplicating nickname access.
- [ChatHistorySystem](../../mods-dll/thebasics/src/ModSystems/ChatHistory/ChatHistorySystem.cs) records UID, account name, nickname, formatted content, recipient counts, and a chat-kind string. Its kind classifier has no roll case. Preserve structured roll identity and recognizable kind if rolls are integrated; do not make staff distinguish rolls from emotes by parsing text.
- [ProximityChatMessageKind](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Models/ProximityChatMessageKind.cs) has six existing kinds and no roll. [Event-args classification](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Models/ProximityChatMessageEventArgs.cs) defaults to Speech. A new kind needs explicit treatment across classifiers and downstream consumers. The [public API](../thebasics-proximity-chat-api.md) is observational, not a supported injection API.
- [Th3EssentialsDiscordRelay](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Th3EssentialsDiscordRelay.cs) relays rendered processed messages when enabled. Local delivery does not imply local-only distribution when this bridge is enabled. Decide whether roll events participate and document the result, especially before adding private/GM rolls.
- [RP culture](../../.opencode/skills/rp-culture/SKILL.md) distinguishes local scene coordination from global OOC and separates spectator communication from embodied cues. A scene roll should have a deliberate spectator policy; above-head bubbles and chatter are unnecessary for the proposed first version. Keep dice enablement separate from OOC toggles.

## Small implementation shape if approved later

1. A bounded parser and evaluator with injectable randomness for deterministic tests. Reject malformed input, oversized count/sides/modifiers, overflow, and excessive input length. Set fixed conservative limits before considering configurable limits.
2. A player command handler with server-side validation and a short per-account cooldown. Generate one immutable result after acceptance; never accept a client-supplied total.
3. An explicit roll delivery/formatting path that reuses appropriate chat primitives and identity resolution. Avoid a general RPG engine or a new public extension API for one consumer. Choose between an explicit pipeline kind and a narrow internal delivery method after confirming history/event semantics.
4. Minimal operator controls: feature enablement and command coexistence. Further range, permissions, or player overrides should follow concrete server needs.

## Verification needed for implementation

Automated: grammar and arithmetic edge cases with deterministic dice; bounded workloads; cooldown; one evaluation for multiple recipients; identical totals across recipients; no speech/language rewriting; recipient boundary and dimension behavior; disabled-command response; name/reason escaping; history identity and kind; relay inclusion/exclusion; command collision behavior.

Owner-approved manual QA later: two nearby clients receive identical rolls, an out-of-range client does not, parked whisper/global-OOC/signing states do not unexpectedly change the audience, nickname and spectator behavior match the chosen policy, and another installed dice mod retains its commands. The source inspection here does not validate runtime compatibility.

## Decisions for the next discussion

1. Primary use: shared RP scene resolution, physical tavern/tabletop games, or both?
2. For scene rolls: fixed normal local range or explicitly selected audiences? Recommend fixed normal range first.
3. Which mechanics are actually used by the community? Recommend basic dice plus signed modifier before advantage, pools, or character-sheet automation.
4. Incorporate into The BASICs or improve an addon? Resolve after evaluating the existing TheBasics-specific addon and the benefit of native history/identity integration.
