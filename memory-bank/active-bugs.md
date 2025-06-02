# Active Bugs: Immediate Attention Required

## Overview
Critical and high-priority bugs that need immediate development attention, compiled from GitHub issues and ModDB community reports.

**Last Updated**: June 2, 2025  
**Total Active Bugs**: 8  
**Critical**: 3  
**High Priority**: 3  
**Medium Priority**: 2

## 🔴 Critical Bugs (Server Stability)

### Bug #1: Connection Crash in 5.1.0-rc.1
- **Source**: GitHub Issue #22, ModDB Comments
- **Error**: `System.Exception: Attempting to send data to a not connected channel`
- **Location**: `ChatUiSystem.cs:line 209` in `OnPlayerJoin()`
- **Impact**: Players crash when connecting after mod update
- **Status**: Implementation complete, validation limited
- **Root Cause**: Network packets sent before channel connection established
- **Solution**: Safe packet sending system implemented but needs stress testing

### Bug #2: Server Console /say Command Crash
- **Source**: GitHub Issue #11
- **Error**: `Object reference not set to an instance of an object`
- **Location**: `RPProximityChatSystem.Say()` line 873
- **Impact**: Server console commands fail with null reference
- **Status**: Open, assigned to BASIC-BIT
- **Context**: Occurs in VS 1.20.0-rc.9

### Bug #3: OnPlayerChat Null Reference Exception
- **Source**: ModDB Comments (CHR3S, January 2025)
- **Error**: `Object reference not set to an instance of an object`
- **Location**: `RPProximityChatSystem.Event_PlayerChat` line 412
- **Impact**: Server crashes, prevents chat functionality
- **Status**: Unresolved
- **Likely Cause**: Missing `UseGeneralChannelAsProximityChat` config option

## 🟡 High Priority Bugs (Core Features)

### Bug #4: TPA Temporal Gear Not Consumed
- **Source**: ModDB Comments (Kasel, May 2025)
- **Issue**: TPA requires temporal gear but doesn't consume it
- **Impact**: Game balance issue - infinite teleportation with single gear
- **Config**: `TpaRequireTemporalGear: true`
- **Status**: Unresolved

### Bug #5: Language Text Display in Floating Bubbles
- **Source**: ModDB Comments (Ragolution, March 2025)
- **Issue**: Floating text bubbles show original language instead of spoken language
- **Example**: Player types in Tradeband, bubble shows English
- **Impact**: Breaks language system immersion
- **Status**: Unresolved

### Bug #6: Nickname System Display Format Problems
- **Source**: GitHub Issue #14, ModDB Comments (Ragolution, February 2025)
- **Issue**: Nicknames showing as "Ragolution (Ragolution)" format
- **Impact**: Interferes with other mods (CAN Markets) using player names for ownership
- **Workaround**: Disable `ShowNicknameInNametag` config
- **Status**: Workaround available, root cause unresolved

## 🟠 Medium Priority Bugs (Feature Issues)

### Bug #7: Sign Language Line of Sight Not Working
- **Source**: GitHub Issue #8
- **Issue**: Sign language does not adhere to line of sight requirements
- **Impact**: Core sign language feature not working as designed
- **Status**: Open bug

### Bug #8: Sign Language Using Spoken Languages
- **Source**: GitHub Issue #9
- **Issue**: Languages like Common/Tradeband used in sign language mode
- **Impact**: Sign language system behavior inconsistency
- **Expected**: Sign language should be separate from spoken languages
- **Status**: Open bug

## Additional Issues Requiring Investigation

### Inconsistent Nametag Behavior
- **Source**: ModDB Comments (Amarillo, March 2025)
- **Issue**: "Hide name tag unless targeting" works inconsistently
- **Status**: Needs investigation

### Basic Commands Not Working
- **Source**: ModDB Comments (CHR3S, January 2025)
- **Issue**: Can't use `/say` or `/yell` commands
- **Likely Cause**: Mod compatibility or config regeneration needed
- **Status**: Needs investigation

## Bug Resolution Priority

### Immediate (This Week)
1. **Connection Crash** - Critical stability issue affecting all users
2. **Server Console Crash** - Prevents admin functionality
3. **OnPlayerChat Exception** - Core chat system failure

### Short Term (Next 2 Weeks)
1. **TPA Gear Consumption** - Game balance issue
2. **Language Display Bug** - Core feature malfunction
3. **Nickname Format Problems** - Mod compatibility issues

### Medium Term (Next Month)
1. **Sign Language Issues** - Complete feature implementation
2. **Nametag Inconsistencies** - Polish existing features

## Testing Requirements

### Connection Crash Testing
- Multiple simultaneous connections
- Network interruption scenarios
- Extended gameplay sessions
- Various server configurations

### TPA System Testing
- Verify temporal gear consumption
- Test cooldown mechanisms
- Validate permission requirements

### Language System Testing
- Test all language combinations
- Verify floating bubble text accuracy
- Test sign language line of sight

## Development Notes

### Network Issues Pattern
Multiple bugs relate to network timing and connection handling:
- Connection crash (packets before channel ready)
- Server console crash (null reference in network context)
- Chat system crashes (network-related null references)

**Recommendation**: Comprehensive network handling audit needed.

### Nickname System Complexity
Multiple nickname-related bugs suggest system needs overhaul:
- Display format issues
- Nametag inconsistencies
- Mod compatibility problems

**Recommendation**: Consider nickname system redesign for reliability.

### Language System Polish
Sign language implementation appears incomplete:
- Line of sight not working
- Spoken language interference
- Display inconsistencies

**Recommendation**: Complete sign language feature implementation.

## Bug Tracking Integration

### GitHub Issues
- Issue #22: Connection crash
- Issue #11: Server console crash
- Issue #14: Nickname nameplates
- Issue #8: Sign language line of sight
- Issue #9: Sign language spoken languages

### ModDB Comments
- Multiple user reports correlate with GitHub issues
- Community provides valuable reproduction scenarios
- User workarounds documented for some issues

## Validation Strategy

### Pre-Release Testing
1. **Network Stress Testing**: Multiple connections, interruptions
2. **Feature Integration Testing**: All systems working together
3. **Mod Compatibility Testing**: Interaction with popular mods
4. **Long-term Stability Testing**: Extended server sessions

### Community Beta Testing
1. **Staged Rollout**: Test with willing community members first
2. **Feedback Collection**: Structured bug reporting process
3. **Rapid Response**: Quick fixes for critical issues found in beta

## Success Metrics

### Stability Metrics
- Zero connection crashes in 48-hour test period
- No server console command failures
- Chat system 100% uptime

### Feature Metrics
- TPA gear consumption working 100% of time
- Language display accuracy 100%
- Nickname system compatibility with major mods

### Community Metrics
- Reduced bug reports on ModDB
- Positive feedback on stability improvements
- Increased adoption of new features