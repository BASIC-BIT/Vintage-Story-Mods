# Chat formatting isolation, October 5, 2026

Objective: prevent a malformed message from styling subsequent chat history entries.
Branch: codex/chat-message-format-isolation, based on the available origin/main.

The client's existing HudDialogChat.UpdateText prefix builds rich-text components separately for every saved message. This path is unconditional: it does not inspect content to choose a rendering strategy. Each parse starts with a fresh tag stack and cloned base font. Native multiline markup, message separators, and scroll behavior are retained. Previous rich-text components are disposed before replacement.

The reported restart announcement has not been captured. A synthetic unclosed strong/font/i message reproduces the same class of formatting leak. This change contains that mechanism regardless of the sender; it does not identify the reporting server's scheduler or mod.

Validation: 132 relevant checks passed, including the native Harmony patch installation check. Build and package completed successfully. Test cases exercise later message weight, color, slant, legitimate bold names, multiline formatting, empty entries, spacing, escaped literal brackets, font sizes, native link components, and custom VTML tag converters. Existing VTML utility and chat occlusion tests also pass. Twenty-eight native Cairo comparisons cover ordinary histories at two widths and two UI scales, including wrapping, mixed fonts, empty histories, literal brackets, and leading/trailing spaces. Those pixels and heights match vanilla. Two additional comparisons explicitly record the known multiline/empty-entry rounding difference; passing these checks does not claim the difference is fixed. The package was copied only inside this worktree, with no server or game-profile deployment.

Runnable checks (from this worktree, VINTAGE_STORY set):

    $env:PATH = "$env:VINTAGE_STORY\Lib;" + $env:PATH
    & D:\bench\vs\.dotnet\dotnet.exe test mods-dll/thebasics.Tests/thebasics.Tests.csproj --configuration Release /p:SkipPostBuildPackage=true --filter 'FullyQualifiedName~ChatHistoryFormattingTests|FullyQualifiedName~ChatUiSystemTests|FullyQualifiedName~VtmlUtilsTests|FullyQualifiedName~ChatOcclusionTests'

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
Measured limitation: independent parsing creates extra native text components. In a synthetic multiline history containing empty entries, this introduced a one-pixel vertical rounding difference at UI scale 1 (the scale 1.5 comparison matched). The temporary content-dependent guard was removed at the owner request. All messages use independent parsing, and the one-pixel difference remains a measured limitation in this multiline/empty-entry case. Manual QA must assess whether it is acceptable.

Local package SHA256 after restoring unconditional isolation: 4901D9DB9391CCEB6B463ADD0B85532DA974D3C6A6F6BF72BE446524061AFF69. Build and package passed with deployment restricted to this worktree's artifacts/local-mods.
Retire this packet once the fix is merged with explicit human QA observations, or the change is abandoned.
