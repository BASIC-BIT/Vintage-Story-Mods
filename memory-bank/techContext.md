# Technical Context: Vintage Story Mods Repository

## Repository Structure

### Production vs Legacy Projects
- **Production**: `mods-dll/thebasics/` - Actively maintained, extensively used in production
- **Legacy**: `mods/` directory - Historical/experimental projects, not used in production

### Build System Architecture
- **Modern Projects**: SDK-style .csproj with .NET 7.0 (thebasics, modernized thaumstory)
- **Legacy Projects**: Traditional .csproj with .NET Framework 4.8 or mixed formats
- **Build Success**: 100% success rate across all 7 projects after modernization efforts

### Project Patterns
```
Repository Root
├── mods-dll/
│   ├── thebasics/          # Primary production mod (.NET 7.0, SDK-style)
│   └── litchimneys/        # Working legacy mod (.NET 7.0, SDK-style)
└── mods/
    ├── makersmark/         # Legacy mod (.NET Framework 4.8)
    ├── forensicstory/      # Legacy mod (.NET Framework 4.8)
    ├── DummyTranslocator/  # Legacy mod (.NET Framework 4.8)
    ├── thaumstory/         # Modernized to .NET 7.0 SDK-style
    └── autorun/            # Legacy mod (.NET Framework 4.8)
```

## Technology Stack

### Primary Production Stack (The BASICs)

### Core Framework
- **Vintage Story Modding API**: Primary framework for game integration
- **C# .NET 7.0**: Programming language and runtime
- **Protobuf-net**: Network serialization for client-server communication
- **Harmony**: Runtime patching for client-side modifications

### Dependencies
```xml
<PackageReference Include="ApacheTech.Common.Extensions" Version="1.2.0" />
<PackageReference Include="ApacheTech.Common.Extensions.Harmony" Version="1.2.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="System.ComponentModel.Composition" Version="8.0.0" />
```

### Vintage Story References
- **VintagestoryAPI.dll**: Core game API
- **VSSurvivalMod.dll**: Survival mode integration
- **VintagestoryLib.dll**: Game engine access
- **VSEssentials.dll**: Essential game systems
- **VSCreativeMod.dll**: Creative mode support

## Development Environment

### Build Configuration
- **Debug**: Outputs to `../../output/` for development testing
- **Release**: Outputs to current directory for distribution
- **Post-build**: PowerShell packaging script (`scripts/package.ps1`)

### Project Structure
```
mods-dll/thebasics/
├── src/                          # Source code
│   ├── Configs/                  # Configuration classes
│   ├── Extensions/               # Extension methods
│   ├── Models/                   # Data models and DTOs
│   ├── ModSystems/               # Feature implementations
│   ├── Utilities/                # Helper classes
│   └── Properties/               # Assembly metadata
├── assets/                       # Game assets (currently empty)
├── scripts/                      # Build and deployment scripts
├── modinfo.json                  # Mod metadata
├── thebasics.csproj             # Project configuration
└── README.md                     # Documentation
```

## Technical Constraints

### Vintage Story Limitations
- **Server-side Only**: Most functionality requires server-side implementation
- **Network Protocol**: Must use Vintage Story's networking system
- **Mod Loading**: Subject to game's mod loading order and lifecycle
- **API Boundaries**: Limited to exposed game APIs and reflection where necessary

### Performance Requirements
- **Minimal Server Impact**: Chat processing must be efficient for large player counts
- **Network Efficiency**: Minimize bandwidth usage for frequent chat messages
- **Memory Management**: Avoid memory leaks in long-running server environments

### Compatibility Constraints
- **Backward Compatibility**: Player data must survive mod updates
- **Cross-Platform**: Must work on Windows and Linux servers
- **Version Support**: Target current stable Vintage Story version

## Development Setup

### Prerequisites
```
- Vintage Story installed at %APPDATA%\Vintagestory\
- .NET 7.0 SDK
- Visual Studio or VS Code with C# extension
- PowerShell (for build scripts)
```

### Environment Variables
```
VINTAGE_STORY=%APPDATA%\Vintagestory\
```

### Build Process
1. **Compilation**: MSBuild compiles C# source to DLL
2. **Reference Resolution**: Links against Vintage Story assemblies
3. **Post-build**: PowerShell script packages mod for distribution
4. **Output**: Creates mod package in appropriate directory

## Network Architecture

### Client-Server Communication
```csharp
// Server-side channel registration
_serverConfigChannel = API.Network.RegisterChannel("thebasics")
    .RegisterMessageType<TheBasicsConfigMessage>()
    .RegisterMessageType<TheBasicsClientReadyMessage>()
    .SetMessageHandler<TheBasicsClientReadyMessage>(OnClientReady);

// Client-side channel registration
_clientConfigChannel = _api.Network.RegisterChannel("thebasics")
    .RegisterMessageType<TheBasicsConfigMessage>()
    .SetMessageHandler<TheBasicsConfigMessage>(OnServerConfigMessage);
```

### Network Connection Safety Patterns
Based on analysis of VS source code at `D:\bench\vs_source\VintagestoryAPI`:

#### Key VS API Insights
- **IClientNetworkChannel.Connected**: Property indicates if server is listening on channel
- **IServerNetworkChannel**: No connection property needed (always "connected" from server perspective)
- **Connection Timing**: Channels exist immediately but may not be "connected" until handshake complete

#### Safe Packet Sending Implementation
```csharp
// Check connection before sending
if (_clientConfigChannel != null && _clientConfigChannel.Connected)
{
    _clientConfigChannel.SendPacket(message);
}
else
{
    // Queue for retry when connection established
    QueuePacketAction(() => SendPacketWhenReady(message));
}
```

#### Retry Mechanism Configuration
- **Retry Delay**: 2000ms (2 seconds) between connection attempts
- **Max Retries**: 10 attempts before giving up
- **Queue Management**: Clear queue on max retries to prevent memory buildup
- **State Tracking**: Monitor retry progress and connection status

### Message Types
- **TheBasicsConfigMessage**: Server config synchronization to clients
- **TheBasicsClientReadyMessage**: Client ready notification to server
- **ChannelSelectedMessage**: Chat channel selection persistence
- **TheBasicsPlayerNicknameMessage**: Player nickname updates
- **TheBasicsRpChatMessage**: Roleplay chat messages

## Data Persistence

### Player Data Storage
Uses Vintage Story's built-in mod data system:
```csharp
// Storage pattern
player.SetModdata("BASIC_NICKNAME", SerializerUtil.Serialize(nickname));

// Retrieval pattern  
var nickname = SerializerUtil.Deserialize(player.GetModdata("BASIC_NICKNAME"), defaultValue);
```

### Configuration Management
- **Server Config**: Stored in `ModConfig/the_basics.json`
- **Auto-generation**: Creates default config if missing
- **Runtime Updates**: Config changes require server restart

## Testing Strategy

### Unit Testing
- None yet, we need this!

### Integration Testing
- **Server Testing**: Manual testing on development servers
- **Multi-player Testing**: Coordination testing with multiple clients
- **Performance Testing**: Load testing with simulated player counts

## Deployment Process

### Packaging
1. **Build**: Compile mod DLL and dependencies
2. **Asset Collection**: Gather mod assets and metadata
3. **Archive Creation**: Package into distributable format
4. **Validation**: Verify mod structure and dependencies

### Distribution
- **ModDB**: Primary distribution through Vintage Story ModDB
- **GitHub Releases**: Source code and development builds
- **Server Installation**: Direct deployment to server mod directories

## Infrastructure Tools

### Build and Packaging System
- **[`build-and-package.ps1`](mods-dll/thebasics/scripts/build-and-package.ps1)**: **RECOMMENDED** - Ensures fresh builds before packaging
- **[`package.ps1`](mods-dll/thebasics/scripts/package.ps1)**: **LEGACY** - Only packages existing DLLs without building
- **Critical Issue**: Original package script can deploy stale builds if DLLs aren't rebuilt
- **Best Practice**: Always use `build-and-package.ps1` to prevent stale build deployment
- **Deployment Pipeline**: Build → Package → Local Copy → SFTP Upload to production server

### Server Log Fetching ([`fetch-logs.ps1`](mods-dll/thebasics/scripts/fetch-logs.ps1))
- **SFTP Integration**: Uses same WinSCP .NET assembly and `.env` configuration as deployment
- **Flexible Parameters**: `-LogType`, `-Days`, `-OutputDir` for customized log retrieval
- **Verified Remote Paths**:
  - `/data/Logs/server-main.log`, `/data/Logs/server-debug.log`
  - `/data/Logs/server-audit.log`, `/data/Logs/server-chat.log`
  - `/data/Logs/server-build.log`, `/data/Logs/server-worldgen.log`
  - `/data/Logs/Archive/` (historical logs with timestamps)
- **Local Organization**: Timestamped subdirectories in `logs/` folder
- **Error Handling**: Graceful handling of missing directories and SFTP errors

### Repository Structure
- **Modernized [`.gitignore`](.gitignore)**: Blacklist approach with proper exclusions
- **Rule Integration**: `.roo/` directory now version controlled
- **Development Workflow**: Structured memory bank update process in [`05-memory-bank-updates.md`](.roo/rules/05-memory-bank-updates.md)

## Tool Usage Patterns

### Harmony Patching
```csharp
[HarmonyPrefix]
[HarmonyPatch(typeof(HudDialogChat), "HandleGotoGroupPacket")]
public static bool HandleGotoGroupPacket(HudDialogChat __instance, Packet_Server packet)
{
    // Prevent channel switching when in proximity chat
    if (_config.PreventProximityChannelSwitching && game.currentGroupid == _proximityGroupId)
    {
        return false; // Block original method
    }
    return true; // Allow original method
}
```

### Extension Method Pattern
```csharp
public static class IServerPlayerExtensions
{
    private const string ModDataNickname = "BASIC_NICKNAME";
    
    public static string GetNickname(this IServerPlayer player)
    {
        return GetModData(player, ModDataNickname, player.PlayerName);
    }
}
```

### Configuration Serialization
```csharp
[ProtoContract]
public class ModConfig
{
    [ProtoAfterDeserialization]
    private void OnDeserialized()
    {
        InitializeDefaultsIfNeeded();
    }
}