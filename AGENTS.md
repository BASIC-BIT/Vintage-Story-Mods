# AGENTS.md

This file provides guidance to coding agents when working with code in this repository.

## Autonomy and Preference Resolution

- Respect the autonomy level explicitly requested by the current user in the active conversation.
- Read `AGENTS.local.md` at the start of every session, if present.
- If autonomy is not specified for a category of action, ask once, then save that preference in `AGENTS.local.md`.
- Preference precedence is: current conversation instructions > `AGENTS.local.md` > `AGENTS.md`.
- `AGENTS.local.md` is intended for local/personal preferences and should not be committed.
- Keep `AGENTS.local.md` focused on operator-specific defaults (autonomy levels, approval gates, workflow preferences), not repository-wide policy.
- If `AGENTS.local.md` does not exist, create it only when a persistent local preference needs to be recorded.
- Record preferences by action category (for example: local repo work, contributor-facing GitHub actions, merges, manual QA, destructive operations).
- Do not include secrets in `AGENTS.local.md`; treat it as local instructions, not secure storage.
- Update `AGENTS.local.md` when the owner clarifies a lasting preference so it applies to future sessions.

## Sensitive and Contributor-Facing Actions

- Default to high autonomy for local repo work (investigation, code edits, builds, tests, and drafting plans).
- Require explicit owner approval in the current conversation before any contributor-facing GitHub action (reviews, approvals, merges, comments, replies, labels, assignments, or similar actions).
- Require explicit owner approval before merge operations.
- Require explicit owner approval before starting manual QA, and before marking manual QA complete.
- For contributor PRs, complete code review and local validation first, then present a QA plan and findings before requesting merge approval.
- For AI/bot review recycle loops on PRs, before pushing follow-up recycle commits: react to each addressed review comment, post a concise reply describing the fix, and resolve the corresponding review thread.

## Agent Toolbox

If the toolbox repo is present next to this repo, also read:
- ../basics-agentic-dogfooding/AGENTS.md
- ../basics-agentic-dogfooding/docs/agentic/README.md
- ../basics-agentic-dogfooding/docs/opencode/README.md
- ../basics-agentic-dogfooding/docs/opencode/toolbox.md

## Repository Skills

OpenCode skills under `.opencode/skills/` are first-class workflow assets for this repository. When a task matches a skill, follow it and update it in the same PR as workflow changes.


## Build Commands

### Local Development Build
```powershell
# Build and package main mod (thebasics) - ALWAYS USE THIS FOR TESTING
.\mods-dll\thebasics\scripts\build-and-package.ps1

# Full solution build (rarely needed)
dotnet restore Vintage-Story-Mods.sln
dotnet build Vintage-Story-Mods.sln --configuration Release

# Package only (after build)
Push-Location mods-dll\thebasics
.\scripts\package.ps1
Pop-Location
```

### Environment Setup
The `VINTAGE_STORY` environment variable must point to your Vintage Story installation directory containing the game DLLs.

### Build Output
All builds output to standard MSBuild locations:
- `mods-dll/thebasics/bin/Release/net8.0/thebasics.dll`
- Packaged mods: `mods-dll/thebasics/thebasics_VERSION.zip`

## High-Level Architecture

### Repository Structure
This repository contains multiple Vintage Story mods organized into two categories:

1. **Simple Mods** (`mods/` directory) - Asset-only or minimal code mods:
   - `autorun` - Auto-execution functionality
   - `DummyTranslocator` - Teleportation system
   - `forensicstory` - Logging/forensics features
   - `makersmark` - Item marking/labeling
   - `thaumstory` - Magic/wand system

2. **Complex Mods** (`mods-dll/` directory) - DLL-based mods with extensive features:
   - `litchimneys` - Chimney automation
   - `thebasics` - Main comprehensive mod (see below)

### The BASICs Mod Architecture

The main mod (`thebasics`) is a comprehensive roleplay and server management mod with these key systems:

1. **Chat System** (`src/ModSystems/Chat/`)
   - Proximity-based chat with configurable ranges
   - Chat UI enhancements with custom rendering
   - Role-play features (OOC, shouting, whispering)
   - Integration with player statistics

2. **Player Statistics** (`src/ModSystems/PlayerStats/`)
   - Tracks player activity, deaths, messages
   - Persistent storage system
   - Admin commands for viewing stats

3. **TPA System** (`src/ModSystems/TPA/`)
   - Teleport request functionality
   - Currently being refactored for gear system integration
   - Uses safe network communication utilities

4. **Save Notifications** (`src/ModSystems/SaveListener/`)
   - Notifies players when world is saving
   - Helps prevent data loss from disconnections

5. **Utilities** (`src/Utilities/`)
   - Network utilities for safe client-server communication
   - Extension methods for server/client APIs
   - Helper classes for common operations

### Networking Architecture

The mod uses Vintage Story's network channel system with custom safety wrappers:
- `SafeClientNetworkChannel` handles connection timing issues
- Automatic retry mechanism for failed packet sends
- Queue system for packets sent before connection established

### Dependency Management

The project uses a separate private repository (`vs-build-dependencies`) to manage Vintage Story DLLs:
- Core DLLs: VintagestoryAPI.dll, VintagestoryLib.dll
- Mod DLLs: VSSurvivalMod.dll, VSEssentials.dll, VSCreativeMod.dll
- Library DLLs: 0Harmony.dll, protobuf-net.dll, cairo-sharp.dll

## Key Development Patterns

### Mod System Structure
All mod systems follow this pattern:
```csharp
public class MyModSystem : ModSystem
{
    public override void Start(ICoreAPI api) { }
    public override void StartServerSide(ICoreServerAPI api) { }
    public override void StartClientSide(ICoreClientAPI api) { }
}
```

### Network Communication
Always use `SafeClientNetworkChannel` for client-to-server communication:
```csharp
_safeChannel = new SafeClientNetworkChannel(_channel, api, config);
_safeChannel.SendPacketSafely(new MyMessage());
```

### Configuration
Mods use JSON configuration files stored in the game's ModConfig directory.

## Testing

No automated test framework is currently configured. Testing is done manually in-game.

### Test Server Operations

This repo is frequently validated against a disposable/test Vintage Story server hosted on Pterodactyl.
If you have just built + uploaded a new mod zip to that test server, it is acceptable to restart it to load the new version.

- Test server (Pterodactyl): `8982de16`
- Typical action after upload: restart the server to apply the new zip

#### Server File Paths (inside the Pterodactyl container)

| Path | Purpose |
|------|---------|
| `/data/Mods/` | Mod zip files (e.g. `thebasics_5_3_0.zip`) |
| `/data/ModConfig/the_basics.json` | Runtime config for the thebasics mod |
| `/data/Logs/server-main.log` | Boot/load log — mod loading, warnings, startup errors |
| `/data/Logs/server-debug.log` | Runtime debug log — detailed mod activity |
| `/data/Logs/server-chat.log` | Chat messages |
| `/data/Logs/server-audit.log` | Admin actions |

#### Build → Deploy → Verify Loop

```powershell
# 1. Build + package + SFTP upload (all-in-one)
.\mods-dll\thebasics\scripts\build-and-package.ps1

# 2. Restart the test server (via Pterodactyl API)
#    Use ptero tools if loaded, or curl:
curl -s -X POST `
  -H "Authorization: Bearer $env:PTERO_TOKEN" `
  -H "Content-Type: application/json" `
  "https://pt.basicbit.net/api/client/servers/8982de16/power" `
  -d '{"signal":"restart"}'

# 3. Wait ~25s for server to come back up, then fetch logs
powershell -NoProfile -ExecutionPolicy Bypass -File mods-dll/thebasics/scripts/fetch-logs.ps1 -LogType all

# 4. Verify clean boot (see "What to look for in logs" below)
```

#### Pterodactyl API Patterns (direct curl)

Credentials come from `.env` in repo root (`PTERO_BASE_URL`, `PTERO_TOKEN`, `PTERO_SERVER_ID`).
All endpoints use the Client API (`ptlc_...` token) under `/api/client/servers/{id}/...`.

```
# List files in a directory
GET /files/list?directory=/data/Mods

# Read a file's contents
GET /files/contents?file=/data/ModConfig/the_basics.json

# Delete files
POST /files/delete  body: {"root":"/data/Mods","files":["old_mod.zip"]}

# Write/overwrite a file (raw body = file content)
POST /files/write?file=/data/ModConfig/the_basics.json

# Power actions (start/stop/restart/kill)
POST /power  body: {"signal":"restart"}

# Server resource usage / current state
GET /resources  → .attributes.current_state ("running"/"stopped"/etc.)
```

**PowerShell gotcha**: When calling curl/Invoke-RestMethod inline from bash on Windows, PowerShell dollar signs (`$`) get eaten by the shell. Write a `.ps1` script file and execute it instead of using inline PowerShell one-liners with variables.

#### What to Look for in Server Logs

After a restart, check `server-main.log` for:

1. **Mod loaded correctly**: Look for `Mod 'thebasics_X_Y_Z.zip' (thebasics):` followed by all 6 mod systems:
   - `thebasics.ModSystems.TPA.TpaSystem`
   - `thebasics.ModSystems.SleepNotifier.SleepNotifierSystem`
   - `thebasics.ModSystems.SaveNotifications.SaveNotificationsSystem`
   - `thebasics.ModSystems.Repair.RepairModSystem`
   - `thebasics.ModSystems.ProximityChat.RPProximityChatSystem`
   - `thebasics.ModSystems.PlayerStats.PlayerStatSystem`

2. **No duplicate mod warning**: Search for `Multiple mods share the mod ID`. If present, an old zip is still in `/data/Mods/` and needs to be deleted.

3. **No exceptions on startup**: Search for `Exception`, `Error`, `WARNING` (excluding vanilla noise like `ErrorReporter`, worldgen, JSON asset warnings).

4. **Config loaded**: If the mod logs config values on boot, verify key settings match expectations.

#### Key Config Toggles for Testing

These are the most commonly toggled settings in `/data/ModConfig/the_basics.json`:

| Key | Default | Purpose |
|-----|---------|---------|
| `OverrideSpeechBubblesWithRpText` | `false` | When true, overhead bubbles show RP-processed VTML text with kind styling + LOS gating |
| `EnableTypingIndicator` | `true` | Show typing indicator above players' heads |
| `SendServerSaveAnnouncement` | `true` | Announce "save started" to players |
| `SendServerSaveFinishedAnnouncement` | `false` | Announce "save finished" to players |
| `ServerSaveAnnouncementAsNotification` | `true` | Use notification popup instead of chat line |
| `EnableGlobalOOC` | `false` | Allow `(( ... ))` global OOC chat |
| `EnableLanguageSystem` | `true` | Enable language commands and formatting |
| `DebugMode` | `false` | Enable `[THEBASICS][perf]` and diagnostic logging |
| `TypingIndicatorDisplayMode` | `2` (Both) | 0=Icon only, 1=Text only, 2=Both icon + text |

#### Launching Test Game Clients

Two VS profiles are set up for multi-client testing:

```powershell
# Profile 2 — auto-connects to test server on launch
Start-Process 'D:\Games\Vintagestory\Vintagestory.exe' `
  -ArgumentList '--dataPath "D:\Games\VSProfiles\Profile2" --fullscreen off -c 15.235.75.126:30000' `
  -WorkingDirectory 'D:\Games\Vintagestory'

# Profile 3 — launches to main menu (connect manually)
Start-Process 'D:\Games\Vintagestory\Vintagestory.exe' `
  -ArgumentList '--dataPath "D:\Games\VSProfiles\Profile3" --fullscreen off' `
  -WorkingDirectory 'D:\Games\Vintagestory'
```

Desktop shortcuts also exist at:
- `D:\Games\VSProfiles\Vintagestory Testing Profile 2.lnk`
- `D:\Games\VSProfiles\Vintagestory Testing Profile 3.lnk`

**After every server restart**, close existing game windows and relaunch both profiles:

```powershell
# Kill existing VS instances, then launch both profiles
Get-Process Vintagestory -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process 'D:\Games\Vintagestory\Vintagestory.exe' `
  -ArgumentList '--dataPath "D:\Games\VSProfiles\Profile2" --fullscreen off -c 15.235.75.126:30000' `
  -WorkingDirectory 'D:\Games\Vintagestory'
Start-Process 'D:\Games\Vintagestory\Vintagestory.exe' `
  -ArgumentList '--dataPath "D:\Games\VSProfiles\Profile3" --fullscreen off' `
  -WorkingDirectory 'D:\Games\Vintagestory'
```

## Linting/Type Checking

The project uses standard C# compilation for type checking. No additional linting tools are configured.
Run `dotnet build` to check for compilation errors.

## Pre-Release QA

Manual QA is required before merging PRs that touch client-side rendering, config behavior, Harmony patches, or client-server interactions. The full workflow is defined in `.opencode/skills/human-qa/SKILL.md`. Core principles:

- **Risk-based triggering.** Consider what's robot-testable vs. human-testable, and effort vs. risk. Things that are hard to verify are usually easy to break.
- **Cards, not vibes.** Every test is a numbered card with concrete steps, expected outcomes, and failure modes. No "does it look right?" — instead "type `hello`, observe a white-text bubble above Player2 that fades in ~4s."
- **Strict verification.** Never check off a PR checklist item without the human explicitly describing what they observed. Push back on vague answers.
- **Config-first batching.** Group cards by server config state to minimize restarts. Within a batch, group by feature area to minimize context-switching.
- **Smart failure recovery.** After fixing a failed card, roll the re-test into the next batch when configs don't conflict, rather than adding an extra restart cycle.
- **Discovery triage.** Pause after each batch to file GitHub issues for out-of-scope findings. Don't lose QA insights; don't let them block the PR either.

For contributor PRs specifically:

- Perform local code review and local validation (build/tests/log checks) before recommending merge.
- If manual QA is required, present a concrete QA plan and obtain explicit owner approval before starting it.
- Do not mark manual QA complete until the owner explicitly approves completion in the current conversation.



## Development Guidance

- **Configuration Management**
  - This mod is live - ProtoMember attributes for existing config values should not be changed where possible - new config values should receive the next available sequentially increasing number.
