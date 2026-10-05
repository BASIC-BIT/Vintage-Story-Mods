# Chat formatting isolation, October 5, 2026

Objective: prevent a malformed message from styling subsequent chat history entries.
Branch: codex/chat-message-format-isolation, based on the available origin/main.

The client's existing HudDialogChat.UpdateText prefix now builds rich-text components separately for each saved message. Each parse starts with a fresh tag stack and cloned base font. Native multiline markup, message separators, and scroll behavior are retained. Previous rich-text components are disposed before replacement.

The reported restart announcement has not been captured. A synthetic unclosed strong/font/i message reproduces the same class of formatting leak. This change contains that mechanism regardless of the sender; it does not identify the reporting server's scheduler or mod.

Validation: 31 focused checks passed, including the native Harmony patch installation check. Build and package completed successfully. Test cases exercise later message weight, color, slant, legitimate bold names, multiline formatting, empty entries, and spacing. The package was copied only inside this worktree, with no server or game-profile deployment.

Runnable checks (from this worktree, VINTAGE_STORY set):

    $env:PATH = "$env:VINTAGE_STORY\Lib;" + $env:PATH
    & D:\bench\vs\.dotnet\dotnet.exe test mods-dll/thebasics.Tests/thebasics.Tests.csproj --configuration Release /p:SkipPostBuildPackage=true --filter 'FullyQualifiedName~ChatHistoryFormattingTests|FullyQualifiedName~ChatUiSystemTests'

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

Retire this packet once the fix is merged with explicit human QA observations, or the change is abandoned.
