---
name: rp-culture
description: Roleplay community culture and player intent behind The BASICs chat features. Read before designing, defaulting, gating, or writing copy for any RP-facing surface. Captures how RP players actually use these features, which is often not inferable from the code.
compatibility: opencode
metadata:
  audience: maintainers
  domain: product-design
---

## Why this file exists

The BASICs is a roleplay mod. Most of its features look, from the code, like generic chat plumbing: ranges, channels, prefixes, toggles. The reasons players and server owners actually want them come from roleplay community culture, which is not derivable from the source and is easy to get subtly wrong while writing something that compiles, passes review, and ships.

An agent working here will understand roleplay conceptually. That is not the same as knowing the culture. This file records the specific cultural facts as they come up, so design decisions stop being re-derived (or re-guessed) each time.

**Read this before:**

- adding or changing an RP-facing chat surface
- choosing a default for anything player-visible
- deciding whether a setting should exist, and at what granularity
- writing player-facing copy or naming a channel, mode, or command
- deciding whether two features that look similar should share a switch

**When you learn something new about how players use this, add it here.** That is the point of the file. The entries below are a growing list, not a complete one.

---

## Entry 1: Global OOC and local OOC are different features

They look like the same feature at two ranges. They are not, and treating them as one is how you get a wrong config surface.

### What each is actually for

**Local OOC** (`(...)`, `/ooc`) is **meta-commentary inside a scene**. A player is mid-roleplay and needs to say something as themselves, to the people physically present: reacting to something that happened in-game, "brb", clarifying a confusing action, sorting out whose turn it is. It is tightly coupled to the scene and to the people in it. Range matters, because the audience is "the people I am currently roleplaying with".

**Global OOC** (`((...))`, `/gooc`) is **roleplay coordination across the server**. Announcing an adventure and looking for people to come along, asking if anyone is around to run a scene, organising. It works as a substitute for general chat, and it exists mainly because everyone is already looking at the roleplay tab. Range is irrelevant by definition.

### Global OOC versus vanilla general chat

A player being deliberate about it will usually split these:

- **General chat**: casual conversation, unrelated to the game world
- **Global OOC**: out-of-character but roleplay-adjacent, coordination and logistics
- **Local OOC**: meta-commentary inside a scene

The distinction between general chat and global OOC is soft and plenty of players do not observe it. But the split is real for the players who care, and it is why global OOC is not simply redundant with a channel the game already has.

### Design consequences

- **Gate them independently.** A server owner's reasons for disabling each are unrelated. Some owners consider global OOC too immersion-breaking and want that traffic in general chat instead. That reasoning says nothing about local OOC, which serves scene-level needs those same owners usually want to keep.
- **Do not let one setting gate both.** During QA of #207 a guard was written that put global OOC behind the local OOC toggle. It compiled, it was defensible from the code, and it silently removed a working feature on servers that had disabled an unrelated setting.
- **Naming is genuinely ambiguous.** "OOC" is used to mean local OOC on some servers and global OOC on others, and communities differ. Do not assume a bare "OOC" in a request, issue, or user report means local. Ask.

---

## Entry 2: Server owners are a distinct persona with distinct motives

Config decisions on this mod are made by roleplay server administrators, not by the players using the feature. Their reasoning is frequently about **protecting the fiction**, not about performance, safety, or preference.

Examples of real motives behind toggles:

- Disabling global OOC because it pulls players out of the world mid-scene
- Requiring nicknames so account names never appear in character
- Line-of-sight and range settings so private conversations stay private, which is a **fiction-integrity** concern before it is a privacy one

When choosing a default, ask what a roleplay server owner would want out of the box, not what is most permissive or most featureful. The default config for this mod deliberately leans feature-forward for RP servers (see `docs/FEATURES.md`, "Default Configuration Philosophy").

### The granularity question

This recurs constantly and should be decided deliberately each time rather than by habit. For any given behavior there are up to three levels:

1. **No config.** The behavior just is what it is, or players self-select per message via a prefix.
2. **Player preference.** Each player chooses; the server does not care.
3. **Server control**, which is really two settings: the server default, *and* whether players may change it.

Default to the lowest level that solves the actual need. Adding config later is easy; removing config from a live mod is not. Level 3 is what RP servers with a house style eventually ask for, but "eventually asks for" is not the same as "needs now".

---

## Entry 3: Two chat axes, and what they are called

As of #207 the mod has two independent axes, and the terminology was chosen carefully. Keep it consistent in code, copy, and docs.

| Axis | Term | Values | Commands |
|---|---|---|---|
| How far it carries | **chat mode** | whisper, normal, yell | `/whisper` `/say` `/yell` |
| What kind of message | **chat type** | in character, emote, OOC, global OOC | `/me` `/ooc` `/gooc` |

**Rejected names, and why** (do not revisit without new information):

- **"Volume"** for the mode axis. Accurate today, but the modes are configurable and a server may repurpose them for something non-acoustic. The label would then assert something the mod does not guarantee.
- **"Channel"** for the type axis. Collides with real chat channels, General and Proximity, which already exist and mean something else to players.

"Chat type" is deliberately bland. Its job is to stay out of the way while the values (`in character`, `OOC`) carry the meaning, because RP players already think fluently in IC/OOC.

---

## Entry 4: Prefix syntax is muscle memory, and breaking it is expensive

RP players type `*`, `(`, `((` thousands of times. These are not discoverable UI, they are motor habit built over years, often carried between servers and between games.

Consequences:

- **Changing what an existing prefix does is a breaking change**, even when the new behavior is better. Weigh it as such.
- **Making a previously-erroring input do something is also a change.** #207 made a bare `/me` park the player in emote mode where it used to be a harmless "missing argument" error. That is the intended feature, but on a server with years of muscle memory it is the most likely source of "the update broke chat" reports.
- Prefer additive syntax (a new prefix like `**`) over redefining existing syntax.

---

## Entry 5: Spectator invisibility does not mean spectator silence

A spectator is usually an administrator observing the world with their in-world body intentionally hidden. They still need to communicate, and server policy should treat communication and embodied presence as separate concerns.

- Spectators may communicate through local OOC, global OOC, environmental narration, and placed casting. Local OOC still serves scene-level coordination, and global OOC still serves server-wide roleplay coordination.
- A default-on server protection should reject ordinary or explicit speech, sign language, and name-led emotes. Do not silently reinterpret an untyped line as OOC; require the administrator to deliberately choose an allowed chat type. Frame this as protection against accidentally publishing embodied roleplay while invisible, not as a punishment or player-permission restriction. Server owners must be able to disable the protection when their staff workflow needs normal RP chat behavior.
- Environmental narration (`!` or `/it`) is the existing nameless alternative to an emote. Placed casting (`!!` or `/envhere`) anchors that narration to a chosen world position. These are the appropriate fiction-facing tools for spectators.
- Passive entity-attached cues must not reveal a spectator. This includes above-head speech bubbles, typing indicators, nametags, map markers, and positional chatter sounds.
- Local spectator OOC identity is its own server choice. Account names are the unambiguous default, while servers may opt into RP nicknames. Global OOC continues to follow the existing global OOC nickname setting.
- A deliberately placed environmental message is attached to the chosen world position, not to the spectator's hidden body. Servers may allow it independently. If a server disables it, reject the placement explicitly instead of falling back to an above-head bubble.

Do not use a spectator visibility rule as a broad chat permission. A rule intended to prevent accidental location or presence leaks should not silently remove the communication tools spectators deliberately invoke.

---

## Open questions worth resolving with players, not from first principles

Recorded so they are not silently answered by whoever touches the code next.

- **Should players be able to hide or mute OOC and global OOC locally?** No such setting exists today. Argument against: players hiding active conversation is a recipe for confusion, where someone is talked to and never sees it.
- **Should a player-facing chat history exist showing what that player would have seen?** Chat history today is permission-gated (`ChatHistoryPermission`, default `commandplayer`), so it is effectively an admin/staff surface rather than a player one.
- **What should the channel labels be?** The `[OOC]` / `[GOOC]` prefixes are currently fixed strings. Server owners may want custom labels, shorter markers (a bare `G`), or no brackets. See the issue tracker.

---

## Measurement

Product decisions here are currently made from intuition and a handful of Discord reports. The mod has a large installed base and an enormous amount of accumulated player typing time, and almost none of it is being measured.

An analytics system exists (`src/ModSystems/Analytics/`) with root-admin consent, three levels (off, server, personalized), and a repeating prompt. Treat verifying and using it as a first-class task, not a chore: the questions in this file (which chat types get used, which toggles get flipped, which commands get attempted while disabled) are answerable with data rather than argument.

Signals worth having, noted as they come up:

- Attempts to use a command that a server has disabled. This is a specific argument for keeping such commands registered and refusing at execution rather than not registering them: an unregistered command produces no signal, no acknowledgement that the player typed the right thing, and no custom copy.
