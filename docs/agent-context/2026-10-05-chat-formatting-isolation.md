# Chat formatting isolation, October 5, 2026

Objective: prevent a malformed message from styling subsequent chat history entries.
Branch: codex/chat-message-format-isolation, based on the available origin/main.

The client's existing HudDialogChat.UpdateText prefix builds rich-text components separately for every saved message. When IsolateChatMessageFormatting is enabled (the default), this path applies to every message without inspecting content. Disabling the setting returns control to native UpdateText. Each parse starts with a fresh tag stack and cloned base font. Native multiline markup, message separators, and scroll behavior are retained. Previous rich-text components are disposed before replacement.

The reported restart announcement has not been captured. A synthetic unclosed strong/font/i message reproduces the same class of formatting leak. This change contains that mechanism regardless of the sender; it does not identify the reporting server's scheduler or mod.

Validation: 195 relevant checks passed, including the native Harmony patch installation check. Build and package completed successfully. Test cases exercise later message weight, color, slant, legitimate bold names, multiline formatting, empty entries, spacing, escaped literal brackets, font sizes, native link components, and custom VTML tag converters. Existing VTML utility and chat occlusion tests also pass. Twenty-eight native Cairo comparisons cover ordinary histories at two widths and two UI scales, including wrapping, mixed fonts, empty histories, literal brackets, and leading/trailing spaces. Those pixels and heights match vanilla. Two additional comparisons explicitly record the known multiline/empty-entry rounding difference; passing these checks does not claim the difference is fixed. The initial package was subsequently staged on disposable server 8982de16 and Profile2/Profile3 with matching hashes. The owner did not observe the client rendering result and deferred manual QA. That earlier deployment is not evidence for the current PR head.

Runnable checks (from this worktree, VINTAGE_STORY set):

    $env:PATH = "$env:VINTAGE_STORY\Lib;" + $env:PATH
    & D:\bench\vs\.dotnet\dotnet.exe test mods-dll/thebasics.Tests/thebasics.Tests.csproj --configuration Release /p:SkipPostBuildPackage=true

Manual QA is pending, and is required before merge because this changes a client Harmony/rendering path. Use the disposable server after staging the final package with matching client hashes. Do not run an actual restart to reproduce the input; console broadcasts are sufficient.

1. **Malformed announcement containment** (P1)
   - Config: existing chat settings.
   - Do: send `<strong>FORMAT PROBE<br>` from the server console. Have players send ordinary IC speech and `((plain OOC probe))`. Repeat with `<i>FORMAT PROBE<br>` and `<font weight="bold" color="#ff0000">FORMAT PROBE<br>`.
   - Expect: later message bodies retain their usual weight, slant, and colors; legitimate nickname styling remains.
   - Watch for: later messages adopting the probe's style, disappearing, or producing a client exception.
2. **History and scrolling regression** (P1)
   - Config: unchanged.
   - Do: switch between General and Proximity, reopen chat, and scroll through existing messages. Hold the scrollbar handle while another client sends a message, then release it and send another message.
   - Expect: history formatting stays isolated after recomposition, line spacing remains readable, held-handle scrolling is not forced to the bottom, and subsequent normal updates follow vanilla scrolling behavior.
   - Watch for: missing lines, doubled separators, overlapping text, broken tab focus, or a stuck scrollbar.

Historical review (October 5):
- PR #23 introduced player-markup injection protection. Its early descriptive bracket replacements were superseded by PR #28.
- PR #28 (https://github.com/BASIC-BIT/Vintage-Story-Mods/pull/28) established minimal angle-bracket escaping for command arguments and nicknames, matching vanilla player-chat escaping and retaining literal bracket display.
- The May 4 hotfix ec2381f added the current user-tag sanitizer and trusted-renderable-tag handling.
- PR #207 (https://github.com/BASIC-BIT/Vintage-Story-Mods/pull/207#discussion_r3682098127) fixed malformed tag handling during distance obfuscation. Bare/unclosed brackets must be garbled rather than opening a markup span. The existing ChatOcclusionTests verify this still holds.
- PR #199 concerned long-token wrapping on bubbles and related surfaces, rather than the chat history parser.
- These fixes remain intact. The HUD patch supplies containment for messages that bypass the player pipeline, including server/console announcements and other mods.

Scope: all messages rendered by HudDialogChat on clients loading The BASICs, regardless of sender or chat tab. Other rendering surfaces, server message delivery, stored history strings, and player escaping/obfuscation are unaffected. The same native richtext parser and registered custom tag converters are used for each entry. The UpdateText replacement applies consistently to every history and retains the installed native scrolling behavior. There is no markup detector or content-based fallback. Interactions with other patches and visual layout still need in-game verification. This is containment, not repair of malformed content within the offending message.

3. **Native and other-source messages** (P1)
   - Do: view server-info, damage/info logs, vanilla command feedback, and any installed mod's chat output. Send a balanced formatted console announcement with an embedded link, then ordinary player text. Send escaped literal bracket text through normal chat and /ooc.
   - Expect: valid styles, literal brackets, multiline text, link clicks, and message visibility retain their usual behavior. Formatting does not span separate messages.
   - Watch for: broken links, lost icons/custom tags, changed spacing, client exceptions, or conflicting chat patches.
Measured limitation: independent parsing creates extra native text components. In a synthetic multiline history containing empty entries, this introduced a one-pixel vertical rounding difference at UI scale 1 (the scale 1.5 comparison matched). The temporary content-dependent guard was removed at the owner request. All messages use independent parsing when the setting is enabled, and the one-pixel difference remains a measured limitation in this multiline/empty-entry case. Manual QA must assess whether it is acceptable.

Local package SHA256 with the server-configurable isolation toggle: E7A26188EA47221C685FBE189D206FE1A37898E9328D8E20FB9516688A613B75. Build and package passed with deployment restricted to this worktree's artifacts/local-mods.
Retire this packet once the fix is merged with explicit human QA observations, or the change is abandoned.

Configurable rollback: IsolateChatMessageFormatting is server-owned, defaults to true for fresh and legacy configs, and uses ProtoMember 159 with DefaultValue(true) so explicit false survives wire serialization. The existing Client UX registry exposes it as Prevent chat formatting leaks with live application on the next chat update. JSON, protobuf, registry, and disabled/missing-config native fallback checks passed. No content-based choice was added.

Admin flow: /thebasics config -> Client UX -> Prevent chat formatting leaks -> Save -> next chat update uses selected rendering path. Editing IsolateChatMessageFormatting in the JSON file and running /thebasics reloadconfig provides the alternate path.

4. **Live vanilla fallback and restoration** (P1)
   - Config: start enabled; after cards 1-3, disable via Client UX and Save, then re-enable and Save.
   - Do: with a malformed strong probe still in history, toggle off and send a fresh plain message. Then toggle on and send another. Close and reopen the editor to check the saved value. Send one message from each connected client.
   - Expect: off restores vanilla parsing (the intentionally malformed probe may restyle later text); on restores independent message formatting on both clients after a new chat update. The saved checkbox matches the selected value.
   - Watch for: the switch failing to sync, requiring a restart, a false value reverting to true, missing history, or client exceptions.
   - Reset: leave isolation enabled. QA remains pending until the owner supplies observations and explicitly approves completion.

Manual QA deferred at the owner request. No manual QA card is marked passed, and QA completion has not been approved. Resume only when the owner requests it; revalidate the staged server and client packages before resuming.

PR preparation refresh: fetched origin/main at 5324f195890f3b87979e6d49011f232639c10038. Full The BASICs suite passed 1175 tests with 8 pre-existing skips. Changed-file whitespace verification and required build/package passed. Corrected pre-existing pragma comment indentation in ModConfig.cs that failed whitespace verification. Current local package SHA256: 8943EC1B2C37E0904DE8A2D8853ADF1A136B32FE2F5A763433DAA594353E33DD. This package remains local. No merge-ready claim can be made until hosted feedback/checks, the post-commit 30-minute gate, and manual QA requirements are satisfied.
