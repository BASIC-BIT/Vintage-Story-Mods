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

6. **Scene Markers** (P1)
   - Setup: default production-like config with `EnableSceneMarkers=true`. Player A (creator), player B with claim build access, player C with `controlserver` and claim access. Have one marker location inside a claim B cannot build in.

   1. **Craft And Item Glyph** (P1)
      - Do: Craft a marker from two loose stones. Look at it in inventory, in hand, and dropped on the ground.
      - Expect: Shapeless two-stone recipe yields one marker. All three views show the exclamation glyph, not a stone base. An unwritten marker's tooltip tells you to place it and Shift-right-click.
      - Watch for: recipe missing, stone base still rendered in any of the three views, or a wrong held-item transform.

   2. **Ground And Wall Placement** (P1)
      - Do: Place one marker flat on the ground and one against a wall, trying each of the four facings.
      - Expect: Both attachments place and orient like a sign, with no collision box and no light blocking.
      - Watch for: refused placement, wrong rotation, a marker you cannot target, or the wall variant floating.

   3. **Symbol, Color, Size, Height, Bobbing** (P1)
      - Do: Shift-right-click to edit. Step through all six symbols (exclamation, question, information, dot, ring, diamond) and all five colors (Yellow, White, Blue, Green, Red). Open `Other icons...`, pick a catalog icon, change color, save and reopen, then pick a symbol tile again. Set indicator size to 25, 100, and 300 percent, height offset to -0.5, 0, and 4, then toggle Idle bobbing off and on. Save & Close and reopen each time. Target the marker and look away.
      - Expect: Selector tiles draw real artwork matching the preview and the world. A catalog icon replaces the billboard symbol in the preview and the world, takes the chosen color, shows in the tile beside the button, and leaves every symbol tile unlit until you pick one, which clears it. Every indicator uses the same steady translucent style; there is no effect or hologram picker. Size and height apply independently. Bobbing moves only the symbol, about 0.05 blocks on a four-second cycle, and the symbol grows slightly while targeted. Out-of-range size or height values are refused with a localized error.
      - Watch for: a blank or font-glyph tile, an empty billboard where a catalog icon failed to draw instead of the fallback symbol, a symbol picker left open after the editor closes, colors not repainting, the bubble drifting with the bob or the target growth, or a saved value snapping back on reopen.

   4. **Display Modes And Distance Fade** (P1)
      - Do: Compare When targeted, Always nearby, and On interaction. Set indicator distance to a short finite value, then Unlimited; set a separate nearby text distance. Walk in and out of both radii, aim at and away from each marker, and check occlusion behind a wall.
      - Expect: When targeted shows the bubble only while aimed at it. Always nearby shows it throughout its own text distance and fades independently of the indicator. On interaction shows no bubble at all. Indicators fade through the outer quarter of their distance, Unlimited stays visible within loaded terrain, and walls obscure both indicator and bubble.
      - Watch for: the text distance field enabled in the wrong mode, a bubble in On interaction mode, indicators visible through terrain, or flicker at the fade boundary.

   5. **Bubble Size, Title Icon, Show Description** (P1)
      - Do: With Show description in bubble off, then on, compare a short and a long multiline description at bubble size 40, 100, and 200 percent. Open the Icon picker, search a name, page through the catalog, choose an icon, then choose None. View from screen edges and steep camera angles.
      - Expect: Off shows the title only; on adds the description, bounded to a preview while the reader keeps the full text. Letters stay the same size at a given percentage and the panel grows with the text. The chosen icon draws to the left of the title without overlapping wrapped text, appears in the editor tile and in the world, and None clears it for good.
      - Watch for: shrinking letters in long bubbles, an icon jumping above the title, a cleared icon reappearing after save or pickup, unclickable catalog tiles, or a picker left open after the editor closes.

   6. **Read Versus Edit** (P0)
      - Do: Plain right-click a written marker in every display mode, including as a claim visitor. Shift-right-click it. Shift-right-click a written marker while holding it. Try editing from more than 8 blocks away and from outside the claim. Open the editor, change the title, and try Cancel, the title-bar X, and Escape.
      - Expect: Plain right-click always opens the full text in the book-style reader, headed by the marker's title icon and its title in bold above the page; Shift-right-click opens the editor only with claim build access and within 8 blocks. The held item reads in the same book view. The inspector shows the title and a short preview. Closing the editor with unsaved changes asks for confirmation before discarding them, and a read-only locked marker never asks. Refusals are explicit localized errors.
      - Watch for: right-click opening the editor, an editor opening for an unauthorized player, a silent refusal, or claim bypass.

   7. **Read Marks** (P1)
      - Do: With players A and B near one Always nearby marker, have A right-click it and press Mark as read, then close and reopen the reader. Walk A away and back, and rejoin the server as A. Have B check the same marker. As A, reopen and press Mark as unread. Then as A edit and save the marker, and separately open the editor and use Clear read, confirming the prompt.
      - Expect: After A marks it read, A sees no bubble in any display mode and a visibly dimmer indicator bobbing at half height and half speed, while B still sees the bubble at full strength. The reader button reads Mark as unread on reopen and A's state survives relog. Mark as unread restores A's bubble. Any save, and Clear read after its confirmation, make the marker unread for both players again. Clear read is disabled while the marker is locked and refused without claim build access or from beyond 8 blocks.
      - Watch for: the bubble still drawn for a reader, the indicator hidden instead of dimmed, one player's mark affecting the other, a stale button label, a read state lost on relog or surviving a save, or a Clear read that anyone can press.

   8. **Creator Lock** (P0)
      - Do: As A, edit a marker, click the top-right lock glyph, and Save & Close. Repeat once clicking the glyph and then Cancel. As B, open, read, break, and try to lock the marker. Keep a stale editor open on B while A locks.
      - Expect: The armed glyph locks the marker on Save & Close; Cancel discards the pending lock with no change. No padlock item is involved. A locked marker opens read-only for everyone, A included, with every field and Save & Close disabled. B cannot lock, edit, break, or pick it up, and a stale editor's save is rejected.
      - Watch for: a Save & lock button still present, a non-creator arming the lock, a stale editor bypassing the lock, or the lock glyph showing the wrong state.

   9. **Unlock** (P0)
      - Do: As A, click the lock glyph on the locked marker. Repeat as C. Attempt it as B, from beyond 8 blocks, and without claim access.
      - Expect: A and C unlock and the editor reopens fully editable; nothing is consumed or returned. B, out-of-range, and no-claim attempts are refused with a localized error.
      - Watch for: an unauthorized unlock succeeding, the editor staying read-only after a successful unlock, or a lock state that does not reach other clients.

   10. **Pickup And Re-Place Persistence** (P0)
       - Do: Break and re-place a written, appearance-customized, locked marker. Restart the server and rejoin. Also place a fresh crafted marker after saving a distinctive appearance.
       - Expect: Title, body, display mode, every appearance setting, creator, and lock survive pickup, re-place, and restart. Exactly one item drops. The fresh marker inherits your last saved appearance with empty content; the picked-up marker keeps its own.
       - Watch for: duplicate or missing drops, reset appearance, transferred ownership, a lost lock, or a fresh marker inheriting someone else's content.

   11. **Markup Safety** (P0)
       - Do: Save a title and body containing `<strong>`, an anchor tag, an icon tag, an unbalanced `<`, and a very long single word. Check the bubble, reader, inspector, item name, and item tooltip.
       - Expect: Markup appears as literal text everywhere, wrapping stays inside the panel, the title truncates at 80 characters, and the body caps at 4096.
       - Watch for: rendered VTML, a broken or blank bubble texture, a link or icon tag taking effect, or a client exception on save.

   12. **EnableSceneMarkers=false** (P0)
       - Do: Set `EnableSceneMarkers=false`, restart the server, and rejoin near existing markers. Try to craft, place, edit, and read. Set it back to `true` and restart.
       - Expect: Existing markers remain in the world as plain blocks. Placement, editing, floating bubbles, indicators, and the crafting recipe are all unavailable, with no client errors. Flipping it back restores every marker with its saved content and appearance intact.
       - Watch for: markers disappearing or losing data, the recipe still craftable, an indicator or bubble still rendering, an editor still opening, or the change taking effect without a restart.

   13. **Logs And Exceptions** (P0)
       - Do: Fetch server and client logs after the pass.
       - Expect: `SceneDescriptionSystem` loads once; audit entries record scene-marker edits and unlocks; no scene-marker exceptions or warnings.
       - Watch for: rejected-packet or malformed-edit warnings, appearance-preference read failures, renderer or texture exceptions, and ghost indicators after leaving and re-entering loaded terrain.

7. **Typing Indicator** (P1)
   - Config: `EnableTypingIndicator=true`.
   - Do: Open chat on one client, type and pause, then close chat.
   - Expect: Other client sees chat-open/composing/typing states and timeout/clear behavior.
   - Watch for: stale indicator after close/disconnect, indicator above self, or indicators through walls.

8. **Nametag Range, LOS, And Self View** (P0)
   - Config: `NametagRequiresLineOfSight=true`; use a known `NametagRenderRange`.
   - Do: Approach the other player, cross the configured range boundary, target them when target-only mode is enabled, then test visibility through stone, glass, foliage, a slab/door/fence, and partial eye/torso/feet exposure. Switch the observing client to F5 third-person view and check its own nametag. Repeat the long-range movement for at least one minute.
   - Expect: The remote nametag uses the configured strict range boundary, targeting behavior, transparent-block rules, and multi-point LOS. The local player's own nametag remains visible in F5 third-person view even when target-only mode is enabled, while vanilla still hides it in first person. Movement and rendering remain responsive throughout the long-range pass.
   - Watch for: a remote nametag beyond range, different results around partial block selection boxes, target-only mode hiding the local player's own F5 nametag, or any client hitch/freeze while LOS refreshes.

9. **Save Notification** (P1)
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

1. **Admin Config Panel Access And Save** (P0)
   - Config: admin/root client and non-admin client available.
   - Do: As non-admin, try `/basic config`. As admin, run `/basic config`, toggle `DebugMode`, save, close/reopen, then restore the original value.
   - Expect: Non-admin is denied; admin sees the panel; save persists to `ModConfig/the_basics.json`; clients receive updated config without restart.
   - Watch for: panel opening for non-admins, duplicate network channel errors, save packet errors, or stale UI values after save.

2. **Admin Config Live Toggle** (P1)
   - Config: admin/root client.
   - Do: Toggle `EnableTypingIndicator` off, save, type on another client, then toggle it back on and save.
   - Expect: Indicators clear when disabled and resume when re-enabled without a server restart.
   - Watch for: stale overhead indicators, client exceptions, or config file not matching the saved value.

3. **Admin Config Restart-Required Warning** (P1)
   - Config: admin/root client.
   - Do: Change a restart-required setting such as `EnableLanguageSystem`, save, then restore the original value.
   - Expect: Save succeeds and reports that restart is required for that setting.
   - Watch for: setting silently applying as if live, no warning, or command/UI desync.

4. **Admin Config Reviewed Settings** (P2)
   - Config: existing config with missing or empty `ReviewedConfigSettingKeys`.
   - Do: Open `/basic config`, note `NEW:` labels, click `Mark Reviewed`, close and reopen.
   - Expect: New-setting labels disappear and `ReviewedConfigSettingKeys` is persisted.
   - Watch for: reviewed state not saving or unrelated config values changing.

5. **Player Stats Display** (P1)
   - Config: `PlayerStatSystem=true`.
   - Do: Run `/playerstats` and `/pstats <player>`.
   - Expect: Current tracked stats display without raw keys.
   - Watch for: missing block-break/distance stats or formatting errors.

6. **Admin Config Live Permission Refresh** (P1)
   - Config: admin/root client and a non-admin test client without a temporary test privilege.
   - Do: Change a live permission setting such as `TpaRequestPrivilege` to a temporary privilege, save, verify the non-admin cannot use `/tpa`, then restore the original privilege and save.
   - Expect: The permission change applies to existing command instances without restart and restores cleanly.
   - Watch for: commands remaining usable after privilege tightening, commands staying locked after restore, or restart-required warnings for permission-only changes.

7. **Admin Config Complex Row Validation** (P1)
   - Config: admin/root client.
   - Do: Open `/basic config`, change one flattened complex row such as `ProximityChatClampFontSizes`, a `ChatDelimiters.*` value, or a `PlayerStatToggles.*` value, save, close/reopen, then restore the original value.
   - Expect: Valid edits persist and reload in the panel; invalid ranges or malformed comma-separated integers are rejected without changing the config.
   - Watch for: save exceptions, partial writes after validation failure, unreadable field labels, or stale values after reopen.

8. **Player Stats Clear Flow** (P1)
   - Config: admin permission available.
   - Do: Run `/clearstat <player> <statName>` without confirm, then with `confirm`.
   - Expect: Confirmation guard appears; confirmed clear resets only selected stat.
   - Watch for: accidental clear without confirm or wrong stat cleared.

9. **Set Durability** (P2)
   - Config: root/admin client.
   - Do: Hold a durability item and run `/setdurability 1` and `/setdurability 100%`, then test negative input, empty hand, and non-durability item.
   - Expect: Valid item updates; invalid cases produce readable errors.
   - Watch for: crashes or block/non-item mutation.

## Post-Test Log Check

- Fetch logs after testing.
- Search server logs for `Exception`, `Error`, `WARNING`, `thebasics`, `Chatter`, and `chatter`.
- Check local client logs if a visual/audio feature behaved unexpectedly.
- Record failed cards with observed behavior, not just pass/fail.
