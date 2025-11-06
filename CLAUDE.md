# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.



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

## Current Work Status

Based on git status, current work involves:
- Refactoring the TPA system for gear integration (`TpaSystem.cs`)
- Updates to server player extensions (`IServerPlayerExtensions.cs`)

## Testing

No automated test framework is currently configured. Testing is done manually in-game.

## Linting/Type Checking

The project uses standard C# compilation for type checking. No additional linting tools are configured.
Run `dotnet build` to check for compilation errors.
```

## Development Guidance

- **Configuration Management**
  - This mod is live - ProtoMember attributes for existing config values should not be changed where possible - new config values should recieve the next available sequentially increasing number.