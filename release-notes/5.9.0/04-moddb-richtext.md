[h2]The BASICs v5.9.0[/h2]

[h3]Sticky chat types[/h3]

[b]/me[/b], [b]/ooc[/b], and [b]/gooc[/b] now work the way [b]/whisper[/b], [b]/say[/b], and [b]/yell[/b] always have. Send one with a message and it sends that single line, as before. Send one with no message and you stay in that chat type until you leave it.

[list]
[*][b]/ooc[/b] with no message parks you in local out of character chat. Run it again to return to in character speech.
[*][b]/gooc[/b] with no message parks you in global out of character chat.
[*][b]/me[/b] with no message parks you in emote.
[*]Any prefix still overrides for one line, so a player parked in OOC can emote with [b]*waves*[/b] without leaving OOC.
[/list]

Range and chat type are two separate things, and they combine. You can whisper in character, whisper out of character, or yell an emote, by setting each one independently. Running a range command with a message, such as [b]/say hello[/b], sends that line in character and returns you to in character speech.

[h3]Questions read as questions[/h3]

A message ending in a question mark now uses a question verb, so [b]Where are you?[/b] renders as [b]Alice asks, "Where are you?"[/b] instead of [b]Alice says[/b]. This applies to whisper, say, and yell, and the verbs are configurable per mode with [b]ProximityChatModeQuestionVerbs[/b].

[h3]Server wide proximity chat range[/h3]

A proximity range of [b]-1[/b] now means unlimited, delivering to every online player. Set it per mode with [b]ProximityChatModeDistances[/b], so a server can make normal speech server wide while whisper and yell keep their limits. At unlimited range there is no far edge, so distance obfuscation and distance font scaling do not apply.

[h3]Two experimental occlusion settings, both off by default[/h3]

These model geometry between a speaker and a listener. Both are off unless a server owner turns them on, and both take effect without a restart.

[list]
[*][b]SpeechOcclusionWallPenaltyBlocks[/b] adds effective distance for each sound blocking block between two players, so walls muffle speech toward unintelligible and then out of range, rather than cutting it off at a hard boundary. [b]0[/b] disables it.
[*][b]RequireClearSoundPathForSpeech[/b] gates delivery per chat mode on an unobstructed sound path.
[/list]

Sound and sight deliberately behave differently. Glass and water stop speech but not sight. Foliage stops neither. An unlimited range cannot be combined with [b]RequireClearSoundPathForSpeech[/b], because the check needs a bounded range to work against, and config validation reports that combination on startup.

[h3]Overhead speech bubbles no longer clip long words[/h3]

A single long token, such as a URL or an unbroken string, is now wrapped instead of being cut off at the edge of the bubble.

[h3]Sign language and overhead text carry through foliage[/h3]

Signing works through a tree canopy, and the speech bubble, nametag, typing indicator, and placed environmental bubbles above a player under a canopy stay visible. These previously disagreed with each other, so a signed message could arrive in chat while nothing rendered above the signer.

Reading another player's character sheet with [b]/look[/b] still requires a clear view, where foliage does block.

[h3]Distant chat is readable[/h3]

The smallest distance font size is now 9 instead of 6. Text at the far edge of a chat range is still small, but it is legible.

Existing configurations that use the previous default font sizes are updated to the new floor the first time the server loads. Custom values are left alone.

[h3]RP character inventory ownership[/h3]

Switching RP characters now carries inventories added by other mods, as long as they belong to the switching player. External inventories and other players' inventories are still left alone.

[h3]Optional temporal gear cost for /top[/h3]

[b]Teleportation.TopRequireTemporalGear[/b] makes [b]/top[/b] consume one temporal gear. It defaults to [b]false[/b], and the gear is only consumed when the teleport actually completes.

[h3]Vintage Story 1.22.6[/h3]

This release builds and is tested against Vintage Story 1.22.6.
