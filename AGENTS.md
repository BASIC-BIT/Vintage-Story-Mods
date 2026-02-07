# AGENTS.md

This file provides guidance to AI Coding Agents when working with code in this repository.

## Local Maintainer Context (Optional)

If present, read `AGENTS.local.md` at the start of a session.

- This file is gitignored and may contain personal workflow preferences (e.g., speech-to-text use).
- Use it to adapt communication style (ask targeted clarifying questions, summarize decisions), and to respect approval boundaries.
- Never copy secrets from it into commits, issues, PRs, or public channels.

## Operating Principles (Ship-Safe)

- This mod has tens of thousands of users. Prefer boring, reliable changes over clever ones; avoid risky refactors unless explicitly scoped.
- Research-first: before implementing anything, search the repo and `../vs_source` for existing hooks/systems so we do not duplicate functionality.
- Backwards compatibility: do not renumber or reuse existing `[ProtoMember(n)]` IDs in config/message contracts. Add new fields using the next available number; do not reuse reserved/removed IDs.
- Guard new behavior behind config flags when feasible; default to the least disruptive behavior.
- Client safety: a client crash is a release blocker. Favor null-safe code paths, defensive checks, and graceful fallback behavior.
- Networking: prefer the existing `thebasics` network channel and `SafeClientNetworkChannel` for client->server sends; send on state changes (not every frame/keystroke).
- Logging: keep logs useful and low-volume; avoid per-frame/per-tick logging.

## Agentic Workflow

- Discover: use subagents for broad repo searches and external research; capture findings briefly with file paths.
- Design: propose the smallest viable implementation and an incremental rollout plan.
- Implement: keep changes localized; reuse existing patterns (mod systems, network channel, config distribution).
- Verify: build/package locally and request human in-game verification for behavior changes.
- Release hygiene: ensure CI builds still pass and config migrations are safe.

## When To Ask For Human Help

Ask the maintainer to step in when you need any of the following:

- In-game validation (launching client/server, reproducing a bug, confirming UX).
- Access/setup changes (new MCP activation requiring restart, credentials, PATs, server access).
- Updating Vintage Story DLLs (new game version, or pulling fresh DLLs for decompilation).
- ModDB verification (checking comments/reports, confirming behavioral expectations).



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

### Source Reference (Decompiled)
This workspace commonly includes `../vs_source` (see `Vintage-Story-Mods.code-workspace`) which contains decompiled Vintage Story sources (`VintagestoryAPI`, `VintagestoryLib`, etc.). Prefer referencing `../vs_source` for API behavior questions.

### Build Output
All builds output to standard MSBuild locations:
- `mods-dll/thebasics/bin/Release/net7.0/thebasics.dll`
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

Notes:
- Do not commit proprietary DLLs to this repo. Use `vs-build-dependencies` (CI) or local installs (`VINTAGE_STORY`) instead.
- CI workflows pin `VS_VERSION`/`.NET` versions; update those deliberately and together.

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

## Current Work Status

The working tree may be dirty. Use `git status` to understand what is currently in progress and avoid reverting unrelated local changes.

## Testing

No automated test framework is currently configured. Testing is done manually in-game.

## Linting/Type Checking

The project uses standard C# compilation for type checking. No additional linting tools are configured.
Run `dotnet build` to check for compilation errors.
```

## Release & Versioning

- **Branching by game version:** keep incompatible VS/.NET targets on separate long-lived branches.
  - Example in this repo: `compat/vs1.20-dotnet7` targets VS 1.20.x + .NET 7; `master` targets newer VS (1.21+) + newer .NET.
  - Prefer cherry-picking fixes between branches over cross-target hacks.
- **Version numbers:** releases are driven by `mods-dll/thebasics/modinfo.json` (and `mods-dll/thebasics/Properties/AssemblyInfo.cs`), tags use `Vx.y.z`.
- **Release workflow:** use the GitHub Actions `Create Release` workflow when cutting a version; it updates versions, builds/packages, creates tag + GitHub release.
- **ModDB upload:** ModDB has no upload API; expect a manual step.
  - If automation is needed, use a browser automation tool (e.g., Playwright) with a human-authenticated session.
  - Never commit credentials; prefer interactive login or repo secrets.

## Playbooks (“Skills”) And How To Write Them

We can’t rely on memory alone; codify repeatable workflows as short playbooks.

- Put playbooks in a stable doc (e.g., `AGENTS.md` for high-level, or a separate `docs/agent-playbooks/` for step-by-step).
- Each playbook should include: purpose, when-to-use trigger, prerequisites, exact steps/commands, safety notes, verification checklist, and rollback plan.
- For UI automation playbooks (ModDB uploads, web logins): first run an **exploratory pass** to learn the UI, then write a deterministic “do X” playbook.

## Playwright Exploratory Protocol (For Future UI Automation)

- Explore with read-only intent first (navigate, snapshot, identify selectors/labels).
- Record stable anchors (role/name/placeholder text) rather than brittle CSS selectors.
- Convert exploration into a playbook with:
  - explicit navigation path
  - required inputs
  - expected page-state checks after each action
  - failure/lockout handling steps

## OpenCode Tooling Notes

- Prefer `Glob`/`Grep`/`Read`/`apply_patch` over shell file operations.
- Use subagents for wide discovery (repo scan, external mod research) before implementing.
- If a new MCP/tool requires a restart or human login, ask the maintainer early; don’t stall.

## Development Guidance

- **Configuration Management**
  - This mod is live - ProtoMember attributes for existing config values should not be changed where possible - new config values should recieve the next available sequentially increasing number.
- **Vintage Story Asset Domains**
  - Vanilla content that resides under `assets/survival` in the game install is still addressed through the `game:` domain when loading or patching assets. Prefer `game:` over `survival:` unless a mod explicitly registers its own domain.

## Known Candidate Feature Port

### Typing Indicator (Port From `FatigueDev/typing_indicator`)

Goal: fold the unmaintained Typing Indicator mod into `thebasics` with production-grade practices and RP-friendly defaults.

- Existing related surface area:
  - Client: `mods-dll/thebasics/src/ModSystems/ChatUiSystem/ChatUiSystem.cs` already patches `HudDialogChat` and has a commented breadcrumb for a typing message.
  - Server: `mods-dll/thebasics/src/ModSystems/ProximityChat/RPProximityChatSystem.cs` already owns the `thebasics` network channel and config distribution.
- Recommended approach:
  - Detect local typing state via existing `HudDialogChat` hooks (poll input text + focus with debouncing).
  - Send state changes via `SafeClientNetworkChannel` on the existing `thebasics` channel.
  - Render indicator client-side above player heads (avoid API-fragile mount hooks like `MountedOn`).
  - Scope rendering by range (config) and keep it behind a config flag for safe rollout.
