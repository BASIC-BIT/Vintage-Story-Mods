# Project Brief: Vintage Story Mods Repository

## Core Purpose
This repository contains multiple Vintage Story mods, with the primary focus being **"The BASICs"** - a comprehensive roleplay proximity chat system that transforms how players communicate in Vintage Story servers.

## Primary Goals
1. **Enhanced Roleplay Communication**: Provide immersive proximity-based chat with realistic distance mechanics
2. **Language System**: Implement configurable languages that players can learn and use for roleplay
3. **Player Identity**: Enable nickname system with colors for character identity
4. **Server Quality of Life**: Add essential server management features (save notifications, sleep coordination, player stats)
5. **Teleportation System**: Provide balanced player teleportation with configurable restrictions

## Key Requirements
- **Modular Architecture**: Each feature should be independently configurable and toggleable
- **Performance**: Minimal impact on server performance with efficient message processing
- **Flexibility**: Extensive configuration options for server administrators
- **Compatibility**: Work seamlessly with existing Vintage Story mechanics
- **User Experience**: Intuitive commands and clear feedback for players

## Success Criteria
- Stable proximity chat system with configurable ranges (whisper, normal, yell, sign language)
- Working language system with scrambling for unknown languages
- Reliable nickname system with color support
- Functional player statistics tracking
- Balanced TPA system with cooldowns and restrictions
- Comprehensive configuration system for server customization

## Target Users
- **Server Administrators**: Need extensive configuration and management tools
- **Roleplay Communities**: Require immersive communication systems
- **General Players**: Want enhanced social features and quality of life improvements

## Technical Constraints
- Must work within Vintage Story's modding framework
- Client-server architecture with network synchronization
- Harmony patches for client-side modifications
- Protobuf serialization for network messages
- Backward compatibility with existing player data