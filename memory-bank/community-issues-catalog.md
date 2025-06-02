# Community Issues Catalog: ModDB Comments Analysis

## Overview
Analysis of community feedback from https://mods.vintagestory.at/thebasics comments section, cataloging reported issues, feature requests, and user concerns that need attention.

**Analysis Date**: June 2, 2025  
**Comments Analyzed**: 49 comments from May 2022 to May 2025  
**Active Issues Identified**: 16 distinct issues requiring attention

## Critical Bugs (Immediate Attention Required)

### 🔴 TPA Temporal Gear Not Consumed (May 2025)
- **Reporter**: Kasel (May 18th, 2025)
- **Issue**: TPA system requires temporal gear to teleport but doesn't consume it
- **Impact**: Game balance issue - players can teleport infinitely with single gear
- **Status**: Unresolved
- **Priority**: High - affects game balance
- **Config Setting**: `TpaRequireTemporalGear: true`

### 🔴 OnPlayerChat Null Reference Exception (January 2025)
- **Reporter**: CHR3S (January 22nd, 2025)
- **Error**: `Object reference not set to an instance of an object` in `RPProximityChatSystem.Event_PlayerChat`
- **Location**: Line 412 in `RPProximityChatSystem.cs`
- **Impact**: Server crashes, prevents chat functionality
- **Status**: Unresolved
- **Priority**: Critical - causes server instability
- **Suggested Fix**: Likely related to missing `UseGeneralChannelAsProximityChat` config option

### 🔴 Language Text Display Bug (March 2025)
- **Reporter**: Ragolution (March 2nd, 2025)
- **Issue**: When speaking in other languages, floating text bubbles show original language instead of spoken language
- **Example**: Player types in Tradeband, bubble shows English text
- **Impact**: Breaks language system immersion
- **Status**: Unresolved
- **Priority**: Medium-High - core feature malfunction

## Nickname System Issues

### 🟡 Nickname Display Format Problems (February 2025)
- **Reporter**: Ragolution (February 19th, 2025)
- **Issue**: Nicknames showing as "Ragolution (Ragolution)" format
- **Impact**: Interferes with other mods (CAN Markets) that use player names for ownership
- **Workaround**: Disable `ShowNicknameInNametag` config option
- **Status**: Workaround available, root cause unresolved
- **Priority**: Medium

### 🟡 Inconsistent Nametag Behavior (March 2025)
- **Reporter**: Amarillo (March 31st, 2025)
- **Issue**: "Hide name tag unless targeting" feature works inconsistently
- **Impact**: Unreliable nametag visibility behavior
- **Status**: Unresolved
- **Priority**: Medium

### 🟡 Nickname Range and Display Bugs (February 2025)
- **Reporter**: Royal_X5 (February 4th, 2025)
- **Issue**: Multiple nickname problems:
  - Can't set range correctly
  - Sometimes nicknames always show regardless of settings
- **Status**: Unresolved
- **Priority**: Medium

## Missing/Broken Commands

### 🟡 /pmessage Command Non-Functional (January 2025)
- **Reporter**: CHR3S (January 30th, 2025)
- **Issue**: `/pmessage` command doesn't work
- **Status**: Command removed from codebase but still documented
- **Priority**: Low - documentation cleanup needed
- **Action**: Remove from documentation or reimplement

### 🟡 Basic Commands Not Working (January 2025)
- **Reporter**: CHR3S (January 23rd, 2025)
- **Issue**: Can't use basic commands like `/say` or `/yell`
- **Likely Cause**: Mod compatibility issue or config regeneration needed
- **Status**: Unresolved
- **Priority**: Medium

## Feature Requests (High Community Interest)

### 🟢 Channel Order and Default Channel (March 2025)
- **Reporter**: PookieBunny (March 13th, 2025)
- **Request**: Swap general and proximity channels or make proximity the default
- **Rationale**: Easier access to proximity chat, reduce need for global OOC
- **Community Interest**: High
- **Status**: Feature request acknowledged by BASIC
- **Implementation**: Partially addressed with `UseGeneralChannelAsProximityChat` config

### 🟢 Admin Username Colors (April 2025)
- **Reporter**: GlooMeGlo (April 21st, 2025)
- **Request**: Admin username color functionality similar to NameTag tweaks mod
- **Use Case**: Distinguish admins and staff in-game
- **Status**: Feature request
- **Priority**: Medium

### 🟢 Client-Side Save Notification Disable (April 2025)
- **Reporter**: Hexedian (April 9th, 2025)
- **Request**: Option to disable server save notifications client-side
- **Rationale**: Notifications distracting when not in general chat tab
- **Status**: Feature request
- **Priority**: Low-Medium

### 🟢 General Chat Disable Option (December 2024)
- **Reporter**: Kara (December 26th, 2024)
- **Request**: Complete disable of general chat functionality
- **Status**: Partially addressed with `UseGeneralChannelAsProximityChat` config
- **Priority**: Medium

### 🟢 AFK Notification System (September 2024)
- **Reporter**: DejFidOFF (September 20th, 2024)
- **Request**: Notify when players are AFK (not moving or doing anything)
- **Status**: Feature request
- **Priority**: Low

### 🟢 Nickname Command Disable (April 2023)
- **Reporter**: Amarillo (April 28th, 2023)
- **Request**: Disable `/nick` command to prevent player impersonation
- **Status**: Partially addressed with `ProximityChatAllowPlayersToChangeNicknames` config
- **Priority**: Low

### 🟢 Server Save Message Color Customization (September 2023)
- **Reporter**: Gnusik2291 (September 30th, 2023)
- **Request**: Change color of server save announcement text
- **Suggested Implementation**: HTML color tags in config
- **Status**: Feature request with GitHub issue created
- **Priority**: Low

## Documentation Issues

### 📚 Player Stats Usage Unclear (October 2023)
- **Reporter**: McTaco (October 14th, 2023)
- **Issue**: How to use the stats system is unclear
- **Current Command**: `/playerstats (player)` - not well documented
- **Status**: Documentation improvement needed
- **Priority**: Low

### 📚 Missing Command Documentation
- **Issue**: Several commands missing from mod description
- **Examples**: `/playerstats`, `/clearstats`, `/clearstat`
- **Status**: Documentation update needed
- **Priority**: Low

## Historical Issues (Resolved)

### ✅ Creatures Walking After Death (June 2023)
- **Reporter**: Amarillo (June 4th, 2023)
- **Issue**: Creatures keep walking when killed, turn into bones when killed from range
- **Status**: Fixed in v4.0.0-pre.3
- **Root Cause**: API behavior change in VS 1.18 lore update

### ✅ Windows Server Compilation Issues (March 2023)
- **Reporters**: Catochondria, WickedSchnitzel, Falco
- **Issue**: Live compilation problems with Windows dedicated server
- **Status**: Resolved by converting to DLL mod format
- **Solution**: Mod now distributed as compiled DLL

## Community Feedback Patterns

### Positive Feedback Themes
- **Essential for Roleplay**: Multiple comments calling it "must-have" for multiplayer
- **Active Development Appreciation**: Users value responsive developer
- **Feature Richness**: Comprehensive feature set highly valued

### Common Pain Points
1. **Nickname System Complexity**: Multiple reports of nickname-related bugs
2. **Channel Management**: Users want more control over chat channel behavior
3. **Documentation Gaps**: Several requests for clearer usage instructions
4. **Mod Compatibility**: Some conflicts with other mods (CAN Markets example)

### Feature Request Priorities (Based on Community Interest)
1. **Channel Order/Default Channel** - High interest, multiple requests
2. **Admin Visual Distinction** - Medium interest, server admin focused
3. **Client-Side Customization** - Medium interest, user experience focused
4. **AFK Detection** - Low interest, quality of life feature

## Recommended Action Plan

### Immediate (Critical Bugs)
1. Fix TPA temporal gear consumption bug
2. Resolve OnPlayerChat null reference exception
3. Fix language text display in floating bubbles

### Short Term (High Impact Issues)
1. Address nickname system display format problems
2. Implement channel order/default channel improvements
3. Fix inconsistent nametag behavior

### Medium Term (Feature Requests)
1. Add admin username color functionality
2. Implement client-side save notification disable
3. Enhance documentation for player stats and commands

### Long Term (Quality of Life)
1. AFK notification system
2. Server save message color customization
3. Comprehensive documentation overhaul

## GitHub Issue Correlation
- Several issues mentioned have corresponding GitHub issues created by BASIC
- Community feedback should be cross-referenced with existing GitHub issues
- New issues should be created for untracked problems

## Community Engagement Notes
- BASIC actively responds to community feedback
- Discord server provides additional support channel
- Users appreciate transparency about known issues and development progress
- Feature requests often come with specific use cases and rationale