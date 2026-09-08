# Dice implementation QA cards

Status: prepared, not started. Owner approval is required before deployment/client launch and before marking manual QA complete. Automated tests and static PNG previews do not substitute for these observations.

## Batch A: enabled, RpText, real relay

Setup after approval: use test server 8982de16 and profiles2/3, preserve the current config, enable dice and history with RpText bubbles, and use the existing Th3Essentials relay setup. Record actual whisper/normal/yell ranges. Capture package hashes. No analytics schema deployment is implied.

1. **Public roll and compact badge (P0)**
   - Do: Stand nearby with both clients. Player A enters /r d20 # qa-public. Repeat with /roll 4d6kh3.
   - Expect: Both clients show the same completed result and breakdown. Plain d20 has a numbered die beside the result; 4d6kh3 has a wireframe die beside its total. The bubble is one line.
   - Watch for: Different results, duplicate chat, clipped badge, expression text in bubble, or chatter audio.

2. **Speech range and selected chat type (P0)**
   - Do: Select whisper, then park global OOC and roll. Move B outside whisper but within normal range; repeat. Switch A to normal and then yell, repeating at each configured boundary.
   - Expect: Only clients inside that speech range receive the result, irrespective of OOC type. Whisper/yell lines and bubbles retain (W)/(Y); normal is unmarked.
   - Watch for: Global-OOC broadcasting the roll, language scrambling, or mismatched markers.

3. **Private output and logs (P0)**
   - Do: A uses /proll d20 # qa-private-sentinel, /privateroll d6, and /thebasics proll d6. Inspect B, staff history, server-chat/server-audit logs, and Discord. Also enter malformed /proll d6+ # qa-private-error.
   - Expect: Only A receives [Private Roll] or a safe error. No bubble, Discord line, or private contents on shared surfaces, including the malformed input.
   - Watch for: Raw command arguments in audit/error logs or public history.

4. **Single history/Discord publication (P0)**
   - Do: Find qa-public in staff history and the configured Discord channel.
   - Expect: One roll entry and one Discord line with the same result/reason and applicable range marker. Ordinary nearby speech still relays normally.
   - Watch for: Duplicate bridge publication, rerolled numbers, or an absent Discord line. Queue insertion alone is not delivery proof.

5. **Spectators and bubble lifecycle (P1)**
   - Do: Observe A roll while visible, behind a solid wall, then as an active spectator. Return to normal mode and roll again.
   - Expect: Visible bubble expires using the existing lifetime; wall blocks its render; spectator command still delivers chat but no entity-attached bubble. Normal bubbles resume afterward.
   - Watch for: Revealed spectator location or stuck textures.

6. **Command coexistence and regression (P1)**
   - Do: With any installed competing short dice command, use /thebasics roll d6 and /thebasics proll d6. Run bare /r, ordinary /help, /whisper, and /me hello.
   - Expect: Fallbacks work, bare owned /r shows useful help, another mod retains its short command, and ordinary commands behave normally.
   - Watch for: Replaced command handlers or broken command responses.

## Batch B: Vanilla and Off bubbles

7. **Existing bubble policies (P1)**
   - Config: First Vanilla, then Off, using normal config reload/restart workflow.
   - Do: A rolls d20 while B stands nearby in each mode.
   - Expect: Vanilla shows a plain readable summary; Off shows no bubble. Chat result remains complete in both.
   - Watch for: Custom icons leaking into Vanilla or bubbles persisting in Off.

## Batch C: dice disabled

8. **Feature toggle and privileges (P1)**
   - Config: EnableDiceRolling=false.
   - Do: Try public/private aliases and both namespaced fallbacks. Try ordinary speech. Restore dice enabled, then try from a player without chat privilege.
   - Expect: Disabled commands give useful feedback and publish no result; ordinary speech remains normal. Missing privilege prevents rolling.
   - Watch for: An alias bypassing the toggle or private/public errors appearing to other clients.

Record each observed result and any failures against the PR's exact tested commit. Restore the saved test configuration after the approved session. No card is checked off by this document.
