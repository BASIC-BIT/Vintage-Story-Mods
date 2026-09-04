# The BASICs v5.9.0

## Sticky chat types

`/me`, `/ooc`, and `/gooc` now work the way `/whisper`, `/say`, and `/yell` always have. Send one with a message and it sends that single line, as before. Send one with no message and you stay in that chat type until you leave it.

- `/ooc` with no message parks you in local out of character chat. Run it again to return to in character speech.
- `/gooc` with no message parks you in global out of character chat.
- `/me` with no message parks you in emote.
- Any prefix still overrides for one line, so a player parked in OOC can emote with `*waves*` without leaving OOC.

Range and chat type are separate, and they combine. A player parked in local OOC who runs `/whisper` is whispering out of character and stays in OOC. Global OOC is the exception, because it has no range of its own: an explicit range command sends that one line as ranged speech, and the player remains in global OOC afterward.

Entering a sticky type is gated. All three require RP chat and the player's own RP text to be enabled. `/ooc` also requires `AllowOOCToggle` and the `OOCTogglePermission` privilege, and `/gooc` requires `EnableGlobalOOC`. A player who cannot enter a type is told why.

## Questions read as questions

In the default `StandardRoleplay` presentation, a message ending in a question mark now uses a question verb, so `Where are you?` renders as `Alice asks, "Where are you?"` instead of `Alice says`. This applies to whisper, say, and yell, and the verbs are configurable per mode with `ProximityChatModeQuestionVerbs`.

The other presentation modes are unaffected, because `SimpleSpeech` and `PlainProximity` render `Alice: ...` with no verb and `Prose` renders no speech verb at all. Sign and Babble keep their own verbs regardless of punctuation.

## Server wide proximity chat range

A proximity range of `-1` now means unlimited, delivering to every online player. Set it per mode with `ProximityChatModeDistances`, so a server can make normal speech server wide while whisper and yell keep their limits. At unlimited range there is no far edge, so distance obfuscation and distance font scaling do not apply.

## Two experimental occlusion settings, both off by default

These model geometry between a speaker and a listener. Both are off unless a server owner turns them on, and both take effect without a restart.

- `SpeechOcclusionWallPenaltyBlocks` adds effective distance for each sound blocking block between two players, so walls muffle speech toward unintelligible and then out of range, rather than cutting it off at a hard boundary. `0` disables it.
- `RequireClearSoundPathForSpeech` gates delivery per chat mode on an unobstructed sound path.

Sound and sight deliberately behave differently. Glass and water stop speech but not sight. Foliage stops neither. An unlimited range cannot be combined with `RequireClearSoundPathForSpeech`, because the check needs a bounded range to work against, and config validation reports that combination on startup.

## Overhead speech bubbles no longer clip long words

In RpText bubble mode, a single long token such as a URL is now wrapped instead of being cut off at the edge of the bubble. `OverheadChatBubbleMode=Vanilla` still clips, which is what that mode is for.

## Sign language and overhead text carry through foliage

Signing works through a tree canopy, and the speech bubble, nametag, typing indicator, and placed environmental bubbles above a player under a canopy stay visible. These previously disagreed with each other, so a signed message could arrive in chat while nothing rendered above the signer.

Reading another player's character sheet with `/look` still requires a clear view, where foliage does block.

## Distant chat is readable

The smallest distance font size is now 9 instead of 6. Text at the far edge of a chat range is still small, but it is legible.

Existing configurations that use the previous default font sizes are updated to the new floor the first time the server loads. Custom values are left alone.

## RP character switching and open inventories

The safety check that decides whether an RP character switch can proceed now uses Vintage Story's ownership boundary instead of a fixed list of inventory classes. An open inventory belonging to the switching player, including one added by another mod, no longer blocks the switch. An open external container or another player's inventory still does.

The inventories carried across a switch are unchanged: hotbar, backpack, and character.

## Optional temporal gear cost for /top

`Teleportation.TopRequireTemporalGear` makes `/top` consume one temporal gear. It defaults to `false`, and the gear is only consumed when the teleport actually completes.

## Vintage Story 1.22.6

This release builds and is tested against Vintage Story 1.22.6.
