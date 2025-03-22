# Implementation Roadmap: Making Proximity Chat Optionally Default

## Overview
This document outlines the implementation plan for adding a configuration option to make the Proximity chat channel the default chat channel instead of the General chat channel in The Basics mod.

## Current System Analysis
The chat system in Vintage Story consists of:
1. Base game chat implementation in `HudDialogChat` class
2. The Basics mod's proximity chat implementation that adds a new chat channel

## Implementation Details

### 1. ModConfig Changes
Added new configuration options to The Basics mod's config:

```json
{
  "ProximityChatAsDefault": false,  // When true, makes Proximity chat the default channel
  "PreserveDefaultChatChoice": true // When true, remembers user's last selected channel
}
```

### 2. Client-Side Implementation

#### 2.1 Chat Dialog Modifications
Modified the base game's chat dialog behavior through Harmony patches to:
- Override the default chat channel selection
- Preserve user's channel choice between sessions if configured
- Handle switching between General and Proximity channels

#### 2.2 Configuration Handling
- Added proper config loading and validation
- Implemented fallback to defaults if config is missing or invalid
- Added comprehensive error handling and logging

#### 2.3 Channel Persistence
- Store last used channel in client settings
- Validate stored channel before restoring
- Handle invalid or outdated stored preferences

### 3. Special Considerations

#### 3.1 UseGeneralChannelAsProximityChat Compatibility
The implementation includes special handling when `UseGeneralChannelAsProximityChat` is enabled:
- Skips default channel modification
- Disables channel preference storage
- Maintains existing chat functionality

#### 3.2 Error Handling
Comprehensive error handling has been implemented for:
- Missing or uninitialized chat tabs
- Invalid channel configurations
- Failed channel switching operations
- Settings storage/retrieval issues

#### 3.3 Logging
Added detailed logging for:
- Configuration loading and validation
- Channel switching operations
- Error conditions and recovery
- Debug information for troubleshooting

### 4. Testing Requirements

1. Basic Functionality:
   - Verify default channel selection works with config enabled/disabled
   - Test channel persistence between game sessions
   - Ensure all chat features work correctly in both channels
   - Verify compatibility with `UseGeneralChannelAsProximityChat`

2. Edge Cases:
   - Test behavior when switching between characters
   - Verify handling of invalid/corrupt channel preferences
   - Test interaction with other chat mods
   - Verify error handling and recovery

3. Multiplayer Testing:
   - Verify behavior with multiple players
   - Test different config combinations between players
   - Ensure server-client synchronization works

### 5. Configuration Guide

#### 5.1 Basic Configuration
```json
{
  "ProximityChatAsDefault": false,
  "PreserveDefaultChatChoice": true,
  "UseGeneralChannelAsProximityChat": false
}
```

#### 5.2 Configuration Notes
- `ProximityChatAsDefault`: Makes Proximity chat the default channel
- `PreserveDefaultChatChoice`: Remembers user's last selected channel
- `UseGeneralChannelAsProximityChat`: Takes precedence over other settings if enabled

#### 5.3 Recommended Configurations

1. Standard RP Server:
```json
{
  "ProximityChatAsDefault": true,
  "PreserveDefaultChatChoice": true,
  "UseGeneralChannelAsProximityChat": false
}
```

2. Casual Server:
```json
{
  "ProximityChatAsDefault": false,
  "PreserveDefaultChatChoice": true,
  "UseGeneralChannelAsProximityChat": false
}
```

3. Full RP Server:
```json
{
  "ProximityChatAsDefault": true,
  "PreserveDefaultChatChoice": false,
  "UseGeneralChannelAsProximityChat": false
}
```

### 6. Technical Notes

1. **Channel Initialization**:
   - Chat tabs are initialized after GUI loading
   - Patches run after channel initialization
   - Error handling for initialization timing issues

2. **Settings Storage**:
   - Channel preferences stored in client settings
   - Automatic validation on load
   - Graceful fallback to defaults

3. **Error Recovery**:
   - Detailed error logging
   - Automatic fallback to safe defaults
   - User-friendly error handling

### 7. Future Enhancements

Potential future improvements to consider:
1. Per-character channel preferences
2. More granular channel configuration options
3. Channel-specific formatting options
4. Default channel per chat command type
5. GUI configuration interface
6. Channel switching animations/transitions

## Implementation Phases

### Phase 1: Core Implementation
- [ ] Add config options
- [ ] Implement Harmony patches
- [ ] Basic channel preference persistence

### Phase 2: UI and Settings
- [ ] Add settings UI elements
- [ ] Implement tooltips and help text
- [ ] Add configuration validation

### Phase 3: Testing and Polish
- [ ] Comprehensive testing suite
- [ ] Bug fixes and edge case handling
- [ ] Performance optimization if needed

### Phase 4: Documentation
- [ ] Update all documentation
- [ ] Add migration guide
- [ ] Create example configurations

## Technical Considerations

1. **Compatibility**:
   - Must maintain compatibility with other chat mods
   - Should not break existing chat functionality
   - Must handle mod updates gracefully

2. **Performance**:
   - Channel switching should be instantaneous
   - Preference storage should be efficient
   - Minimal impact on chat system performance

3. **Error Handling**:
   - Graceful fallback to General chat if issues occur
   - Clear error messages for configuration problems
   - Logging for debugging purposes

## Future Enhancements

Potential future improvements to consider:
1. Per-character channel preferences
2. More granular channel configuration options
3. Channel-specific formatting options
4. Default channel per chat command type 