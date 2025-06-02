# Active Context: Current Project State

## Current Work Focus
**Development Infrastructure Enhancement**: Successfully implemented server log fetching capabilities and modernized repository structure with improved .gitignore and new development workflow processes.

## Recent Analysis Findings

### Project Scope Discovery
- **Primary Mod**: "The BASICs" is the main focus - a comprehensive roleplay proximity chat system used extensively in production
- **Supporting Mods**: Repository contains several legacy/toy projects (autorun, DummyTranslocator, forensicstory, makersmark, thaumstory, litchimneys)
- **Production Status**: Only "The BASICs" is used in production environments and is actively maintained (version 5.1.0-rc.1)
- **Legacy Projects**: All other mods are old experimental projects or toys, not used in production

### Architecture Insights
- **Sophisticated Design**: Uses transformer pattern for message processing with two-phase pipeline
- **Modular Structure**: Each feature (chat, languages, stats, TPA) is independently configurable
- **Network Synchronization**: Complex client-server communication with protobuf serialization
- **Performance Optimized**: Efficient distance calculations and message processing

### Key Technical Patterns Identified
1. **Extension Methods**: Elegant player data management through `IServerPlayerExtensions`
2. **Configuration-Driven**: Extensive `ModConfig` class controls all behavior
3. **Message Context Flow**: Chat messages flow through transformer pipeline with metadata
4. **Harmony Patches**: Client-side UI modifications for chat system integration

## Recent Issue Resolution

### GitHub Issue #22: Crash on Connection (RESOLVED)
- **Problem**: Players crashing when connecting after updating to 5.1.0-rc.1
- **Root Cause**: `OnPlayerJoin()` sending network packets before channel connection established
- **Solution Implemented**: Comprehensive safe packet sending system with connection checking and retry mechanism
- **Files Modified**: [`ChatUiSystem.cs`](mods-dll/thebasics/src/ModSystems/ChatUiSystem/ChatUiSystem.cs)
- **Status**: Issue fully resolved with robust network communication system

### Technical Solution Details
1. **VS API Analysis**: Discovered `IClientNetworkChannel.Connected` property for connection status checking
2. **Safe Packet Sending**: Implemented `SendPacketSafely<T>()` method that checks connection before sending
3. **Retry Mechanism**: Added automatic retry system with 2-second delays and maximum 10 attempts
4. **Queue System**: Packets are queued when channel not connected and sent when connection established
5. **Memory Management**: Proper cleanup and queue clearing to prevent memory buildup

### Key Learnings from Issue #22
1. **VS API Deep Dive**: Understanding network channel interfaces and connection properties
2. **Root Cause vs Symptoms**: Replaced try-catch masking with proper connection handling
3. **Retry Pattern**: Implemented robust retry mechanism for network timing issues
4. **Queue-Based Architecture**: Used action queuing for deferred packet operations
5. **Connection State Management**: Proper tracking of connection retry state and counts

### Important Insights About GitHub Interaction
- **Read-Only Approach**: GitHub should be treated as primarily read-only for information gathering
- **No Automatic Comments**: Never add issue comments without explicit user permission
- **Avoid AI Detection**: Any GitHub interactions must appear natural and human-authored
- **Focus on Code Solutions**: Prioritize fixing code over GitHub repository management

### Network Architecture Understanding
- **Channel Registration**: Happens in `RegisterForServerSideConfig()` during `StartClientSide()`
- **Timing Issue**: Channel exists but may not be "connected" when `OnPlayerJoin()` fires
- **Handshake Protocol**: Client ready message → Server config response → Client processes config
- **Error Pattern**: "Attempting to send data to a not connected channel" indicates timing, not null reference

### Critical Meta-Learning: Problem-Solving Discipline
- **Avoid Premature Victory Claims**: Never declare a problem "SOLVED" without thorough verification
- **Distinguish Symptoms vs Root Causes**: Preventing crashes ≠ fixing underlying issues
- **Require Manual Confirmation**: Always wait for user validation before claiming success
- **Honest Assessment**: Be transparent about what was actually accomplished vs what remains unresolved
- **Resist Overconfidence**: Initial solutions often address symptoms, not root causes
- **Deep Investigation Required**: Complex issues require understanding the full system context
- **User Feedback Essential**: The user's domain knowledge often reveals gaps in understanding

## Recent Build System Resolution (December 2025)

### Complete Solution Build Success
Successfully resolved all compilation errors across the entire Vintage Story Mods solution, achieving **100% build success rate** for all 7 projects.

### Issues Resolved
1. **makersmark**: Auto property initializer syntax incompatible with C# 5.0
   - Converted `public bool Property { get; set; } = value;` to constructor-based initialization
   - Fixed empty return statement in event handler method

2. **forensicstory**: Multiple compilation errors
   - Fixed incomplete `Log()` method implementation in `ChunkLogger.cs`
   - Added missing `GetPrettyString()` extension method for `EntityPos` type
   - Corrected type mismatches in data handling

3. **DummyTranslocator**: Missing assembly references and files
   - Created missing `Properties/AssemblyInfo.cs` file
   - Updated project to use `$(VINTAGE_STORY)` environment variable instead of hardcoded paths

4. **thaumstory**: .NET Framework version conflicts
   - **Complete modernization**: Converted from old .NET Framework 4.8 to modern SDK-style .NET 7.0 project
   - Created `Properties/AssemblyInfo.cs` for modernized project structure
   - Resolved System.Runtime version conflicts by targeting .NET 7.0

### Repository Structure Understanding
- **Production Mod**: `mods-dll/thebasics/` - The only mod used in production, extensively deployed
- **Legacy Projects**: All other mods (`mods/` directory) are old experimental/toy projects
- **Build Patterns**: Modern projects use SDK-style .csproj with .NET 7.0, legacy projects varied

### Key Technical Insights
- **Project Modernization**: Converting old .NET Framework projects to modern SDK-style resolves version conflicts
- **Environment Variables**: Using `$(VINTAGE_STORY)` for assembly references ensures portability
- **C# Language Versions**: Legacy projects constrained to C# 5.0, modern projects use default language version
- **Assembly References**: Consistent pattern across working projects for Vintage Story API dependencies

## Recent Infrastructure Improvements (June 2025)

### Server Log Fetching System
Successfully implemented comprehensive server log fetching capabilities to complement the existing deployment infrastructure.

#### New [`fetch-logs.ps1`](mods-dll/thebasics/scripts/fetch-logs.ps1) Script
- **SFTP Integration**: Leverages existing `.env` configuration and WinSCP infrastructure
- **Flexible Parameters**: `-LogType` (server/debug/crash/mod/all), `-Days`, `-OutputDir`
- **Verified Functionality**: Successfully downloads 6+ server log files including:
  - `server-main.log` (144KB+ of operational data)
  - `server-debug.log` (16KB+ of debug information)
  - `server-audit.log`, `server-chat.log`, `server-build.log`, `server-worldgen.log`
  - Historical archived logs from `/data/Logs/Archive/`
- **Organized Storage**: Creates timestamped subdirectories in `logs/` folder
- **Error Handling**: Graceful handling of missing directories (e.g., CrashReports)
- **Comprehensive Logging**: Detailed operation logging with full SFTP transaction records

#### Repository Structure Modernization
- **Updated [`.gitignore`](.gitignore)**: Converted from whitelist to blacklist approach
  - Now includes `.roo/` rules in version control
  - Cleaner structure with proper exclusions for build artifacts and logs
  - Easier to maintain and understand
- **New Rule [`05-memory-bank-updates.md`](.roo/rules/05-memory-bank-updates.md)**: Established structured workflow
  - Implementation → Validation → Log Analysis → Memory Bank Update phases
  - User-driven process with explicit memory bank update triggers
  - Clear workflow prompts to guide development process

#### Enhanced Development Workflow
- **Log Analysis Integration**: Server logs now easily accessible for debugging and monitoring
- **Consistent Tooling**: Both deployment (`package.ps1`) and log fetching (`fetch-logs.ps1`) use same infrastructure
- **Improved Documentation**: Updated [`04-logs.md`](.roo/rules/04-logs.md) with verified working examples

### Key Technical Insights from Implementation
1. **WinSCP Integration**: Proper destination path formatting (`"$fetchDir\"`) crucial for successful file transfers
2. **SFTP Error Handling**: Different remote directories have varying permissions and existence
3. **Log File Patterns**: Server uses `.log` extensions, not `.txt` as initially assumed
4. **Archive Structure**: Historical logs stored in timestamped subdirectories under `/data/Logs/Archive/`

### Future Infrastructure Considerations
- **FTP MCP Server**: Potential development of reusable FTP/SFTP MCP server for broader file management
- **Variable-Based Rules**: Consideration of placeholder-based rule system to avoid hard-coded paths
- **Automated Log Analysis**: Potential integration of log parsing and analysis tools

## Next Steps for Development

### Immediate Priorities
1. **Commonize Network Solution**: Extract safe packet sending into reusable utility class
2. **Code Review**: Look for similar network timing issues in other parts of the codebase
3. **Testing Strategy**: Evaluate current test coverage and identify gaps
4. **Documentation Updates**: Ensure README and inline documentation are current

### Planned Refactoring: SafeNetworkChannel Utility

#### Design Goals
- **Reusable**: Create utility class that can be used by any client-side mod system
- **Client-Focused**: Server channels don't have connection issues, focus on client implementation
- **Drop-in Replacement**: Easy to integrate into existing code with minimal changes
- **Consistent API**: Unified interface for safe packet sending across the project

#### Implementation Plan
1. **Create Utility Class**: `SafeClientNetworkChannel` in `Utilities/Network/` folder
2. **Extract Logic**: Move connection checking, retry mechanism, and queue system from ChatUiSystem
3. **Generic Interface**: Support any `IClientNetworkChannel` and message type
4. **Configuration**: Configurable retry delays and maximum attempts
5. **Logging Integration**: Consistent logging patterns with existing mod systems

#### Benefits
- **Code Reuse**: Other mod systems can use the same reliable network communication
- **Maintainability**: Single place to update network handling logic
- **Consistency**: Unified approach to network timing issues across the project
- **Testing**: Easier to unit test network logic in isolation

### Development Opportunities
1. **Network Robustness**: Apply SafeNetworkChannel to other mod systems
2. **Feature Enhancements**: Evaluate community requests and feature gaps
3. **Code Quality**: Refactor any complex methods or improve error handling
4. **Testing Expansion**: Add more comprehensive unit and integration tests

## Active Decisions and Considerations

### Design Philosophy
- **Modularity First**: Every feature should be independently toggleable
- **Performance Conscious**: Minimize server impact for large player counts
- **User Experience**: Prioritize intuitive commands and clear feedback
- **Flexibility**: Extensive configuration options for server administrators

### Current Technical Debt
- **Complex Configuration**: The `ModConfig` class is very large and could benefit from decomposition
- **Message Processing**: The transformer pipeline, while powerful, has high complexity
- **Client-Side Patches**: Harmony patches create maintenance burden with game updates
- **Error Handling**: Some areas could benefit from more robust error recovery

### Important Patterns and Preferences

#### Code Organization
- **Namespace Structure**: Clear separation by feature area (`ModSystems`, `Extensions`, `Utilities`)
- **File Naming**: Descriptive names that indicate purpose and scope
- **Class Responsibilities**: Single responsibility principle with focused classes

#### Network Communication
- **Protobuf Serialization**: Efficient binary serialization for all network messages
- **Channel-Based**: Separate channels for different message types
- **Client Readiness**: Clients signal readiness before receiving configuration

#### Data Management
- **Extension Methods**: Player data accessed through extension methods on `IServerPlayer`
- **Mod Data Keys**: Consistent naming convention with `BASIC_` prefix
- **Serialization**: Use `SerializerUtil` for consistent data serialization

## Learnings and Project Insights

### What Works Well
1. **Transformer Pattern**: Provides excellent flexibility for message processing
2. **Configuration System**: Comprehensive options allow extensive customization
3. **Extension Methods**: Clean API for player data management
4. **Modular Design**: Features can be independently enabled/disabled

### Areas for Improvement
1. **Code Complexity**: Some classes (especially `ModConfig`) are very large
2. **Documentation**: Inline code documentation could be more comprehensive
3. **Testing**: Limited test coverage for complex interaction scenarios
4. **Error Messages**: Could provide more helpful guidance for configuration errors

### Community Integration
- **ModDB Distribution**: Primary distribution through official Vintage Story ModDB
- **Discord Support**: Active community support and feature discussions
- **Server Adoption**: Used by multiple roleplay servers (Saltpoint RP, Fair Travels, etc.)

## GitHub Repository Information
- **Repository**: `BASIC-BIT/Vintage-Story-Mods`
- **Owner**: BASIC-BIT (basic@basicbit.net)
- **Open Issues**: 15 total (as of last check)
- **Primary Language**: C#
- **License**: GPL-2.0

### GitHub Interaction Guidelines
- **Read-Only Approach**: Treat GitHub as primarily read-only
- **Issue Comments**: Only add comments with explicit user permission and compelling reason
- **Avoid AI Slop**: Any GitHub interactions must appear natural and human-authored
- **Focus on Code**: Prioritize code fixes over GitHub management

## Current State Assessment

### Stability
- **Production Ready**: Mod is stable and used on live servers
- **Version Control**: Proper semantic versioning with release candidates
- **Backward Compatibility**: Maintains player data across updates
- **Recent Fix**: Issue #22 crash resolved, improving connection stability

### Feature Completeness
- **Core Features**: All major features implemented and functional
- **Configuration**: Extensive customization options available
- **Commands**: Comprehensive command set for all features
- **UI Integration**: Client-side modifications work seamlessly

### Technical Health
- **Build System**: Proper MSBuild configuration with packaging scripts
- **Dependencies**: Minimal external dependencies, well-managed
- **Performance**: Efficient implementation suitable for production use
- **Maintainability**: Clear code structure with good separation of concerns
- **Network Robustness**: Improved with connection checks for packet sending

## Development Environment Status
- **Build Configuration**: Debug and Release configurations properly set up
- **Dependencies**: All Vintage Story references correctly configured
- **Packaging**: Automated packaging script for distribution
- **Testing**: Basic test framework in place, could be expanded