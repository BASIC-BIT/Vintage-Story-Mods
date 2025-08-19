# Feature Requests: Community-Driven Enhancements

## Overview
Comprehensive catalog of feature requests from GitHub issues and ModDB community feedback, prioritized by community interest and development feasibility.

**Last Updated**: June 2, 2025  
**Total Requests**: 12  
**High Priority**: 4  
**Medium Priority**: 5  
**Low Priority**: 3

## 🟢 High Priority Requests (High Community Interest)

### Request #1: Channel Order and Default Channel Management
- **Sources**: GitHub Issue #5, ModDB Comments (PookieBunny, March 2025)
- **Request**: Make proximity chat the default channel or swap channel order
- **Community Interest**: High - multiple requests across platforms
- **Use Case**: Easier access to proximity chat, reduce need for global OOC
- **Current Status**: Partially addressed with `UseGeneralChannelAsProximityChat` config
- **Remaining Work**: 
  - Better channel ordering control
  - Persistent channel selection across sessions
  - Prevent chat focus switching on notifications

### Request #2: Admin Username Colors and Visual Distinction
- **Sources**: ModDB Comments (GlooMeGlo, April 2025), GitHub Issue #12
- **Request**: Admin username color functionality similar to NameTag tweaks mod
- **Use Case**: Distinguish admins and staff in-game visually
- **Community Interest**: Medium-High (server admin focused)
- **Implementation Ideas**:
  - Configurable admin colors by permission level
  - Support for custom color schemes
  - Integration with existing nickname color system

### Request #3: Client-Side Customization Options
- **Sources**: ModDB Comments (Hexedian, April 2025)
- **Request**: Client-side disable of server save notifications
- **Rationale**: Notifications distracting when not in general chat tab
- **Expansion Potential**:
  - Client-side toggle for various notification types
  - Personal UI customization options
  - Per-player notification preferences

### Request #4: Nickname System Improvements
- **Sources**: GitHub Issue #19, ModDB Comments
- **Request**: Prevent duplicate nicknames and improve nickname management
- **Features Needed**:
  - Duplicate nickname prevention (vs other nicknames and usernames)
  - Admin warning system with confirmation for conflicts
  - Automatic nickname reset when username conflicts arise
  - User notification system for nickname conflicts
  - Nickname history/logging for admin oversight

## 🟡 Medium Priority Requests (Valuable Additions)

### Request #5: Chat Logging System
- **Source**: GitHub Issue #21 (NiemandNatural, March 2025)
- **Request**: Comprehensive chat logging for admin monitoring
- **Use Case**: "spy on players secret whispers"
- **Implementation Considerations**:
  - Privacy and consent implications
  - Configurable logging levels (whispers, all chat, etc.)
  - Log rotation and storage management
  - Admin access controls

### Request #6: Analytics and Usage Tracking
- **Source**: GitHub Issue #20 (BASIC-BIT, March 2025)
- **Request**: Opt-out analytics system to track feature usage
- **Data Points**:
  - Config values and feature adoption
  - Command usage frequency
  - Player toggles/nicknames/colors usage
- **Benefits**: Data-driven development decisions
- **Privacy**: Must be opt-out with clear data usage policies

### Request #7: Enhanced Area Messaging
- **Source**: GitHub Issue #13, ModDB Comments (CHR3S)
- **Request**: Re-implement `/pmessage` command for admin area messaging
- **Use Case**: Send environment messages to specific locations
- **Features**:
  - Target specific coordinates or areas
  - Radius-based messaging
  - Admin-only environment message system

### Request #8: Language Understanding Visual Indicators
- **Source**: GitHub Issue #10
- **Request**: Visual indication when hearing unknown languages
- **Implementation**: Italicize text for unknown languages
- **Benefits**: Better player understanding of language system
- **Expansion**: Different visual styles for different comprehension levels

### Request #9: General Chat Disable Option
- **Source**: ModDB Comments (Kara, December 2024)
- **Request**: Complete disable of general chat functionality
- **Current Status**: Partially addressed with `UseGeneralChannelAsProximityChat`
- **Remaining Work**: True general chat removal option

## 🔵 Low Priority Requests (Nice to Have)

### Request #10: AFK Notification System
- **Source**: ModDB Comments (DejFidOFF, September 2024)
- **Request**: Notify when players are AFK (not moving or doing anything)
- **Implementation**: Configurable AFK detection with time thresholds
- **Use Case**: Server management and player awareness

### Request #11: Enhanced Message Color Customization
- **Source**: GitHub Issue #12, ModDB Comments (Gnusik2291)
- **Request**: Comprehensive color customization for all message types
- **Features**:
  - Server save message colors
  - Per-player message text colors
  - Theme-based color schemes
- **Current Status**: Basic nickname colors implemented

### Request #12: Nickname Command Restrictions
- **Source**: ModDB Comments (Amarillo, April 2023)
- **Request**: More granular control over nickname permissions
- **Current Status**: Basic restriction with `ProximityChatAllowPlayersToChangeNicknames`
- **Enhancement**: Per-group permissions, nickname approval systems

## Implementation Roadmap

### Phase 1: High-Impact User Experience (Next Release)
1. **Channel Management Improvements**
   - Better default channel handling
   - Persistent channel selection
   - Notification focus prevention

2. **Admin Visual Tools**
   - Username color system
   - Admin distinction features

### Phase 2: Administrative Tools (Following Release)
1. **Nickname Management Overhaul**
   - Duplicate prevention
   - Conflict resolution
   - Admin oversight tools

2. **Enhanced Messaging**
   - Area-based admin messaging
   - Improved environment message system

### Phase 3: Advanced Features (Future Releases)
1. **Analytics and Monitoring**
   - Usage tracking system
   - Chat logging capabilities
   - Performance metrics

2. **Client Customization**
   - Personal notification preferences
   - UI customization options

## Community Engagement Strategy

### Feature Voting System
- **Discord Polls**: Let community vote on feature priorities
- **GitHub Discussions**: Detailed feature discussion threads
- **ModDB Feedback**: Regular community check-ins

### Beta Testing Program
- **Feature Previews**: Early access to new features for testing
- **Feedback Collection**: Structured feedback forms
- **Iterative Development**: Community-driven refinements

### Documentation and Communication
- **Feature Announcements**: Clear communication of new features
- **Configuration Guides**: Help admins utilize new capabilities
- **Migration Guides**: Smooth transitions for major changes

## Technical Considerations

### Client-Side vs Server-Side
- **Current**: Primarily server-side mod
- **Future**: May need client-side components for:
  - Personal notification preferences
  - UI customizations
  - Enhanced visual features

### Performance Impact
- **Analytics**: Minimal overhead with efficient data collection
- **Chat Logging**: Storage and I/O considerations
- **Visual Enhancements**: Client rendering performance

### Mod Compatibility
- **Nickname System**: Ensure compatibility with other name-related mods
- **Chat Systems**: Avoid conflicts with other chat modifications
- **Admin Tools**: Integration with existing admin mod ecosystems

## Success Metrics

### Adoption Metrics
- **Feature Usage**: Track adoption of new features
- **Community Feedback**: Positive reception indicators
- **Server Deployment**: Number of servers using new features

### Quality Metrics
- **Bug Reports**: Reduced issues with new features
- **Performance**: No degradation in server performance
- **Stability**: Reliable operation of new systems

### Community Metrics
- **Engagement**: Increased community participation
- **Satisfaction**: Positive feedback on feature implementations
- **Retention**: Continued mod usage and server adoption

## Feature Request Submission Process

### For Community Members
1. **Check Existing Requests**: Review current catalog to avoid duplicates
2. **Provide Use Cases**: Clear explanation of why feature is needed
3. **Consider Implementation**: Think about how feature might work
4. **Submit via GitHub**: Use issue templates for structured requests

### For Development Team
1. **Community Impact Assessment**: Evaluate request popularity
2. **Technical Feasibility**: Assess implementation complexity
3. **Priority Assignment**: Place in appropriate priority tier
4. **Roadmap Integration**: Include in development planning

This feature request catalog will be updated regularly as new requests emerge and existing ones are implemented or deprioritized based on community feedback and technical constraints.