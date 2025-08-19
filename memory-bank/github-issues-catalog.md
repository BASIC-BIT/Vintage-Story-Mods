# GitHub Issues Catalog: BASIC-BIT/Vintage-Story-Mods

## Overview
Analysis of GitHub repository issues for comprehensive tracking of bugs, feature requests, and development tasks.

**Repository**: BASIC-BIT/Vintage-Story-Mods  
**Analysis Date**: June 2, 2025  
**Total Issues Analyzed**: 22 issues (open and closed)  
**Open Issues**: 15  
**Closed Issues**: 7

## Critical Open Issues (Immediate Attention)

### 🔴 Issue #22: Crash on Connection for Players (OPEN)
- **Reporter**: Ragolution (May 22, 2025)
- **Status**: Open, Critical
- **Error**: `System.Exception: Attempting to send data to a not connected channel`
- **Location**: `ChatUiSystem.cs:line 209` in `OnPlayerJoin()`
- **Impact**: Players crash when connecting after updating to 5.1.0-rc.1
- **Root Cause**: Network channel not connected when sending packets
- **Note**: This is the same issue documented in memory bank as "VALIDATED WITH LIMITATIONS"

### 🔴 Issue #11: Server Error When Using /say (OPEN)
- **Reporter**: StanberyTrask (January 15, 2025)
- **Status**: Open, Assigned to BASIC-BIT
- **Error**: `Object reference not set to an instance of an object` in `RPProximityChatSystem.Say()`
- **Location**: Line 873 in `RPProximityChatSystem.cs`
- **Impact**: Server console `/say` command fails with null reference exception
- **Context**: VS version 1.20.0-rc.9

## Nickname System Issues (Open)

### 🟡 Issue #14: Nickname Nameplates Inconsistent Behavior (OPEN)
- **Reporter**: BASIC-BIT (February 5, 2025) - from Royal_X5 ModDB comment
- **Status**: Open, Bug, Assigned to BASIC-BIT
- **Issue**: Can't set range correctly, sometimes nicknames always show regardless
- **Impact**: Unreliable nametag visibility behavior

### 🟡 Issue #19: Prevent Duplicate Nicknames (OPEN)
- **Reporter**: BASIC-BIT (March 12, 2025)
- **Status**: Open, Enhancement
- **Request**: Prevent taking nickname that's already taken by another player or matches existing username
- **Features Needed**:
  - Duplicate nickname prevention
  - Admin warning system with confirmation
  - Automatic nickname reset when conflicts arise
  - User notification system for conflicts

## Language System Issues (Open)

### 🟢 Issue #17: Language Limits for Players (OPEN) - ✅ IMPLEMENTED
- **Reporter**: Ragolution (February 19, 2025)
- **Status**: Open, Enhancement, Assigned to BASIC-BIT
- **Request**: Limit number of languages a player can know
- **Note**: This feature is already implemented in current version with `MaxLanguagesPerPlayer` config

### 🟢 Issue #16: Hidden Languages (OPEN) - ✅ IMPLEMENTED
- **Reporter**: Ragolution (February 19, 2025)
- **Status**: Open, Enhancement
- **Request**: Allow languages to be hidden to prevent players from adding them
- **Note**: This feature is already implemented with `Hidden` flag in language config

### 🟡 Issue #9: Sign Language Should Not Use Spoken Languages (OPEN)
- **Reporter**: BASIC-BIT (January 11, 2025)
- **Status**: Open, Bug
- **Issue**: Languages like Common/Tradeband should not be used in sign language mode
- **Impact**: Sign language system behavior inconsistency

### 🟡 Issue #8: Sign Language Line of Sight Not Working (OPEN)
- **Reporter**: BASIC-BIT (January 11, 2025)
- **Status**: Open, Bug
- **Issue**: Sign language does not adhere to line of sight requirements
- **Impact**: Core feature not working as designed

### 🟢 Issue #10: Language Understanding Indication (OPEN)
- **Reporter**: BASIC-BIT (January 11, 2025)
- **Status**: Open, Enhancement
- **Request**: Add indication when hearing a language you don't understand (italicize text)
- **Priority**: Low-Medium

## Channel Management Requests (Open)

### 🟢 Issue #5: Force Proximity as Default Channel (OPEN)
- **Reporter**: NiemandNatural (May 9, 2024)
- **Status**: Open
- **Request**: Make "Proximity" the default chat channel instead of "General"
- **Community Interest**: High - multiple requests for this feature
- **Note**: Partially addressed with `UseGeneralChannelAsProximityChat` config

### 🟢 Issue #15: Togglable OOC Chat When Using General as Proximity (OPEN)
- **Reporter**: Ragolution (February 19, 2025)
- **Status**: Open, Enhancement
- **Request**: Toggle OOC chat with command like `.OOC` when using General as Proximity
- **Use Case**: Prevent mistells from game notifications changing chat focus
- **Suggested Implementation**: Bindable key command

## Feature Requests (Open)

### 🟢 Issue #21: Chat Logging (OPEN)
- **Reporter**: NiemandNatural (March 27, 2025)
- **Status**: Open
- **Request**: Chat logging system for admin monitoring
- **Use Case**: "spy on players secret whispers"

### 🟢 Issue #20: Analytics System (OPEN)
- **Reporter**: BASIC-BIT (March 15, 2025)
- **Status**: Open, Enhancement
- **Request**: Opt-out analytics system to track feature usage
- **Data Points**:
  - Config values
  - Command usage frequency
  - Player toggles/nicknames/colors usage

### 🟢 Issue #13: Admin Area Message Command (OPEN)
- **Reporter**: BASIC-BIT (February 5, 2025)
- **Status**: Open, Enhancement, Assigned to BASIC-BIT
- **Request**: Re-implement `/pmessage` command for sending environment messages to specific areas

### 🟢 Issue #12: Message Text Color Support (OPEN)
- **Reporter**: BASIC-BIT (January 25, 2025)
- **Status**: Open, Enhancement
- **Request**: Allow changing color of all message text from a player
- **Source**: Discord suggestion from owen

## Resolved Issues (Closed)

### ✅ Issue #18: RP Chat Message Handling Refactor (CLOSED)
- **Reporter**: BASIC-BIT (March 10, 2025)
- **Status**: Closed, Merged (May 12, 2025)
- **Type**: Major refactor pull request
- **Scope**: Complete overhaul of RP chat message handling system

### ✅ Issue #7: Message Colors Support (CLOSED)
- **Reporter**: BASIC-BIT (June 17, 2024)
- **Status**: Closed (January 11, 2025)
- **Request**: Server save announcement color customization and nickname colors
- **Resolution**: Implemented in current version

### ✅ Issue #6: Nickname Only Working in Proximity Channel (CLOSED)
- **Reporter**: NiemandNatural (June 13, 2024)
- **Status**: Closed (January 11, 2025)
- **Issue**: Nicknames not working above player heads or in other channels
- **Resolution**: Fixed with nametag integration

### ✅ Issue #4: Chat Accents Auto-Punctuation Bug (CLOSED)
- **Reporter**: BASIC-BIT (May 22, 2023)
- **Status**: Closed (June 15, 2023)
- **Issue**: Auto-punctuation system acting oddly with chat accents (|text| and +text+)
- **Resolution**: Fixed accent handling with punctuation system

### ✅ Issue #3: ModInfo.json Update (CLOSED)
- **Reporter**: RogueRaiden (May 18, 2023)
- **Status**: Closed (May 22, 2023)
- **Request**: Change `"requiredOnClient": false` to `"side": "server"`
- **Resolution**: Updated mod metadata

### ✅ Issue #2: 1.18.5 Server Exception (CLOSED)
- **Reporter**: RogueRaiden (May 18, 2023)
- **Status**: Closed (May 22, 2023)
- **Issue**: NullReferenceException in player stats system on entity death
- **Resolution**: Fixed null reference handling in stats tracking

### ✅ Issue #1: Disable or Restrict Nicknames (CLOSED)
- **Reporter**: Amarilloo (April 28, 2023)
- **Status**: Closed (January 3, 2024)
- **Request**: Restrict nickname changes to prevent impersonation
- **Resolution**: Implemented `ProximityChatAllowPlayersToChangeNicknames` config option

## Issue Patterns and Insights

### Most Common Issue Types
1. **Nickname System Problems** (4 issues) - Most problematic area
2. **Language System Bugs** (3 issues) - Core feature issues
3. **Channel Management Requests** (2 issues) - High community demand
4. **Network/Connection Issues** (2 issues) - Critical stability problems

### Priority Assessment
- **Critical**: Issues #22, #11 - Server crashes and connection problems
- **High**: Issues #14, #19 - Nickname system reliability
- **Medium**: Issues #8, #9 - Language system core functionality
- **Low**: Enhancement requests for new features

### Development Focus Areas
1. **Network Stability**: Fix connection and packet sending issues
2. **Nickname System Overhaul**: Address multiple reliability problems
3. **Language System Polish**: Complete sign language implementation
4. **Channel Management**: Implement community-requested channel improvements

## Cross-Reference with ModDB Comments
Many GitHub issues correspond to ModDB comments:
- Issue #14 ↔ Royal_X5 ModDB comment about nickname bugs
- Issue #5 ↔ PookieBunny ModDB request for channel order
- Issue #7 ↔ Gnusik2291 ModDB request for save message colors
- Issue #1 ↔ Amarillo ModDB request to disable nicknames

## Implementation Status Notes
- **Issue #17** (Language Limits): Already implemented but issue remains open
- **Issue #16** (Hidden Languages): Already implemented but issue remains open
- **Issue #22**: Has implementation in memory bank but needs validation
- **Issue #18**: Major refactor completed and merged

## Recommended Actions
1. **Close Implemented Issues**: #16, #17 should be closed as they're already implemented
2. **Prioritize Critical Bugs**: Focus on #22 and #11 for stability
3. **Nickname System Review**: Comprehensive audit needed for reliability issues
4. **Community Communication**: Update community on progress for high-interest requests