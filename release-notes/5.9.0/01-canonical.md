# The BASICs v5.9.0

## Sticky chat types

`/me`, `/ooc`, and `/gooc` now work the way `/whisper`, `/say`, and `/yell` always have. Send one with a message and it sends that single line, as before. Send one with no message and you stay in that chat type until you leave it.

- `/ooc` with no message parks you in local out of character chat. Run it again to return to in character speech.
- `/gooc` with no message parks you in global out of character chat.
- `/me` with no message parks you in emote.
- Any prefix still overrides for one line, so a player parked in OOC can emote with `*waves*` without leaving OOC.

Range and chat type are two separate things, and they combine. You can whisper in character, whisper out of character, or yell an emote, by setting each one independently. Running a range command with a message, such as `/say hello`, sends that line in character and returns you to in character speech.

## Questions read as questions

A message ending in a question mark now uses a question verb, so `Where are you?` renders as `Alice asks, "Where are you?"` instead of `Alice says`. This applies to whisper, say, and yell, and the verbs are configurable per mode with `ProximityChatModeQuestionVerbs`.

## Server wide proximity chat range

A proximity range of `-1` now means unlimited, delivering to every online player. Set it per mode with `ProximityChatModeDistances`, so a server can make normal speech server wide while whisper and yell keep their limits. At unlimited range there is no far edge, so distance obfuscation and distance font scaling do not apply.

## Two experimental occlusion settings, both off by default

These model geometry between a speaker and a listener. Both are off unless a server owner turns them on, and both take effect without a restart.

- `SpeechOcclusionWallPenaltyBlocks` adds effective distance for each sound blocking block between two players, so walls muffle speech toward unintelligible and then out of range, rather than cutting it off at a hard boundary. `0` disables it.
- `RequireClearSoundPathForSpeech` gates delivery per chat mode on an unobstructed sound path.

Sound and sight deliberately behave differently. Glass and water stop speech but not sight. Foliage stops neither. An unlimited range cannot be combined with `RequireClearSoundPathForSpeech`, because the check needs a bounded range to work against, and config validation reports that combination on startup.

## Overhead speech bubbles no longer clip long words

A single long token, such as a URL or an unbroken string, is now wrapped instead of being cut off at the edge of the bubble.

## Sign language and overhead text carry through foliage

Signing works through a tree canopy, and the speech bubble, nametag, typing indicator, and placed environmental bubbles above a player under a canopy stay visible. These previously disagreed with each other, so a signed message could arrive in chat while nothing rendered above the signer.

Reading another player's character sheet with `/look` still requires a clear view, where foliage does block.

## Distant chat is readable

The smallest distance font size is now 9 instead of 6. Text at the far edge of a chat range is still small, but it is legible.

Existing configurations that use the previous default font sizes are updated to the new floor the first time the server loads. Custom values are left alone.

## RP character inventory ownership

Switching RP characters now carries inventories added by other mods, as long as they belong to the switching player. External inventories and other players' inventories are still left alone.

## Optional temporal gear cost for /top

`Teleportation.TopRequireTemporalGear` makes `/top` consume one temporal gear. It defaults to `false`, and the gear is only consumed when the teleport actually completes.

## Vintage Story 1.22.6

This release builds and is tested against Vintage Story 1.22.6.
