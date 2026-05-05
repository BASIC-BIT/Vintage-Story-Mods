# The BASICs Release Smoke Test

Use this checklist for compatibility releases and before publishing a new ModDB/GitHub release. It is intentionally broader than a single PR QA plan, but still small enough to run as a smoke pass.

## Setup

- Build and deploy with `mods-dll\thebasics\scripts\build-and-package.ps1`.
- Restart the test server.
- Fetch logs with `mods-dll\thebasics\scripts\fetch-logs.ps1 -LogType all`.
- Confirm `server-main.log` shows Vintage Story version, `.NET` runtime, The BASICs loaded once, all six server mod systems, no duplicate mod warning, and no The BASICs startup exceptions.
- Relaunch both test clients after every server restart.

## Post-Publish ModDB Download Check

Run this after the ModDB release is published. It verifies the player-facing install path, not just the local build artifact.

1. **Put The ModDB Zip On The Test Server** (P0)
   - Do: Download the published ModDB file and place that exact zip in `/data/Mods/` on the test server.
   - Do: Remove any other `thebasics*.zip` files from `/data/Mods/`.
   - Expect: `/data/Mods/` contains only the published release zip, for example `thebasics_5_5_0.zip`.
   - Watch for: accidentally testing a freshly built local zip instead of the public ModDB artifact.

2. **Force A Client-Side ModDB Fetch** (P0)
   - Do: Close all Vintage Story clients.
   - Do: Delete local client copies from `Mods` and the server-specific `ModsByServer` folder for the test profile, for example:
     - `D:\Games\VSProfiles\Profile2\Mods\thebasics*.zip`
     - `D:\Games\VSProfiles\Profile2\ModsByServer\15.235.75.126-30000\thebasics*.zip`
   - Do: Restart the server, then launch the test client and connect to the server.
   - Expect: The first connection reports `lacking mods` for `thebasics@<version>`, requests `v2/mods/install-information`, downloads the ModDB release, reconnects, and loads from `ModsByServer\<host-port>\thebasics_<version>.zip`.
   - Watch for: no ModDB install-information request, wrong version downloaded, manual local `Mods` copy being used, or client-side The BASICs load errors.

3. **Confirm Client Join** (P0)
   - Do: Check the active `client-main.log` after reconnect.
   - Expect: The client log shows `Mod 'thebasics_<version>.zip' (thebasics)`, receives server assets, and reaches level finalize.
   - Watch for: client crash log updates, The BASICs assembly load errors, or disconnect loops.

## Batch 1: Default/Production-Like Config

1. **Clean Boot** (P0)
   - Config: production-like defaults.
   - Do: Restart server, fetch logs, inspect startup.
   - Expect: The BASICs loads once; no startup exceptions; no duplicate mod warning.
   - Watch for: `Multiple mods share the mod ID`, missing mod systems, or The BASICs exceptions.

2. **Basic Proximity Speech** (P0)
   - Config: RP chat enabled.
   - Do: Put two clients near each other. Type `hello there` in proximity chat.
   - Expect: Other client receives formatted IC speech and sees an overhead bubble.
   - Watch for: message swallowed, wrong channel, no bubble, or client exception.

3. **Whisper/Yell Ranges** (P1)
   - Config: RP chat enabled.
   - Do: Test `/whisper quiet test` close together, then at normal distance. Test `/yell help` farther away.
   - Expect: Whisper is short range; yell reaches farther and formats as yell.
   - Watch for: mode not changing, wrong range, wrong punctuation/verb.

4. **Emote And Environment Messages** (P1)
   - Config: RP chat enabled.
   - Do: Run `/me waves` and `/it The wind shifts.`.
   - Expect: Emote and environment formatting appear correctly, with distinct bubble styling.
   - Watch for: escaped VTML visible, missing italics/styling, or wrong name formatting.

5. **Placed Environmental Messages** (P1)
   - Config: `MaxEnvironmentPlacementDistance` nonzero.
   - Do: Aim at a nearby block and type `!!A note is pinned here.`.
   - Expect: Text appears as a placed environmental bubble at the targeted world position.
   - Watch for: fallback to normal env when target is valid, wrong placement, no bubble.

6. **Typing Indicator** (P1)
   - Config: `EnableTypingIndicator=true`.
   - Do: Open chat on one client, type and pause, then close chat.
   - Expect: Other client sees chat-open/composing/typing states and timeout/clear behavior.
   - Watch for: stale indicator after close/disconnect, indicator above self, or indicators through walls.

7. **Nametag Behavior** (P2)
   - Config: current server nametag settings.
   - Do: Approach, target, and move away from the other player.
   - Expect: Nickname/account name and range behavior match config.
   - Watch for: nametag visible beyond configured range or hidden when targeted.

8. **Save Notification** (P1)
   - Config: save announcement enabled.
   - Do: Trigger or wait for a server save.
   - Expect: Start message appears as configured; finish message appears only if enabled.
   - Watch for: duplicate spam, wrong delivery mode, or missing configured text.

## Batch 2: Language And Visibility Config

1. **Language Commands** (P1)
   - Config: `EnableLanguageSystem=true`.
   - Do: Run `/listlang`, add/remove a non-default language, and set a speaking language by prefix.
   - Expect: Descriptions are readable; add/remove/list output is localized and accurate.
   - Watch for: raw lang keys, stale command names, or max-language errors when under the limit.

2. **Unknown Language Scrambling** (P1)
   - Config: two clients with different known languages.
   - Do: Have one client speak a language the other does not know, including the listener's account name or nickname in the sentence.
   - Expect: Speaker sees intended text; listener sees deterministic scrambled text, with their own name word still readable.
   - Watch for: listener seeing raw text, blank text, unstable scrambling, or their own name getting scrambled.

3. **Sign Language LOS** (P1)
   - Config: sign language available.
   - Do: Use sign language while visible, partly visible, briefly hidden before stepping back into view, then fully behind a wall or outside range.
   - Expect: Visible/partly visible recipients receive sign output; briefly hidden recipients receive it if they regain line of sight quickly; blocked/out-of-range recipients do not.
   - Watch for: signs through opaque walls, missing signs with partial clear visibility, or late delivery after the retry window.

4. **Speech Bubble LOS** (P1)
   - Config: RP bubbles enabled through RP chat.
   - Do: Speak while visible, then move behind an opaque wall.
   - Expect: Bubbles respect line of sight.
   - Watch for: bubble rendering through walls or never rendering after returning to sight.

## Batch 3: Chatter And Audio

1. **Basic Chatter** (P0)
   - Config: default `EnableChatter=true`.
   - Do: Put two clients near each other. Type `hello there`.
   - Expect: Other client hears seraph/instrument chatter from the speaker's position.
   - Watch for: no sound, wrong position, endless chatter, or client exception.

2. **Chatter Mode Scaling** (P1)
   - Config: default `EnableChatter=true`.
   - Do: Test `/yell testing chatter` and `/whisper quiet test`.
   - Expect: Yell is louder/more noticeable; whisper is quieter/shorter.
   - Watch for: no distinction or unpleasantly loud defaults.

3. **Chatter Opt-Out** (P0)
   - Config: default `EnableChatter=true`.
   - Do: On listener, run `/chatter off`; speaker chats; then run `/chatter on` and repeat.
   - Expect: Off suppresses chatter heard by that player; on restores it. Other players should still hear that player's chatter when they speak.
   - Watch for: command errors, ignored toggle, or session persistence issues.

4. **Chatter Filters** (P1)
   - Config: default `EnableChatter=true`.
   - Do: Test `/me says "hello"`, `/me waves`, local OOC, global OOC, sign language, and `!!`.
   - Expect: Quoted emote speech chatters; pure emote, OOC, sign language, and environmental messages do not.
   - Watch for: non-speech producing chatter or quoted speech staying silent.

## Batch 4: TPA

1. **Basic TPA** (P0)
   - Config: `AllowPlayerTpa=true`.
   - Do: Run `/tpa <player>` and accept from the target.
   - Expect: Requester teleports to target.
   - Watch for: wrong direction, request not delivered, or crash.

2. **TPA Here** (P1)
   - Config: `AllowPlayerTpa=true`.
   - Do: Run `/tpahere <player>` and accept.
   - Expect: Target teleports to requester.
   - Watch for: reversed direction.

3. **TPA Deny/Cancel/List** (P1)
   - Config: `AllowPlayerTpa=true`.
   - Do: Create requests, run `/tpalist`, `/tpdeny`, `/tpacancel`, and `/cleartpa`.
   - Expect: Requests list and clear predictably.
   - Watch for: stale requests or ambiguous multi-request behavior.

4. **Temporal Gear Path** (P1)
   - Config: `TpaRequireTemporalGear=true`.
   - Do: Test request without gear, with gear accepted, and with gear denied/cancelled.
   - Expect: Missing gear blocks request; accepted request consumes gear; denied/cancelled request returns gear.
   - Watch for: gear dupes, gear loss, or inventory-full return bugs.

## Batch 5: Admin And Stats

1. **Player Stats Display** (P1)
   - Config: `PlayerStatSystem=true`.
   - Do: Run `/playerstats` and `/pstats <player>`.
   - Expect: Current tracked stats display without raw keys.
   - Watch for: missing block-break/distance stats or formatting errors.

2. **Player Stats Clear Flow** (P1)
   - Config: admin permission available.
   - Do: Run `/clearstat <player> <statName>` without confirm, then with `confirm`.
   - Expect: Confirmation guard appears; confirmed clear resets only selected stat.
   - Watch for: accidental clear without confirm or wrong stat cleared.

3. **Set Durability** (P2)
   - Config: root/admin client.
   - Do: Hold a durability item and run `/setdurability 1` and `/setdurability 100%`, then test negative input, empty hand, and non-durability item.
   - Expect: Valid item updates; invalid cases produce readable errors.
   - Watch for: crashes or block/non-item mutation.

## Post-Test Log Check

- Fetch logs after testing.
- Search server logs for `Exception`, `Error`, `WARNING`, `thebasics`, `Chatter`, and `chatter`.
- Check local client logs if a visual/audio feature behaved unexpectedly.
- Record failed cards with observed behavior, not just pass/fail.
