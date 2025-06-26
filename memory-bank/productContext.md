# Product Context: The BASICs Mod

## Why This Project Exists
Vintage Story's default chat system is basic and doesn't support the immersive roleplay experience that many communities desire. Players need:
- **Realistic Communication**: Chat that respects physical distance and line of sight
- **Character Identity**: Ability to have in-character names separate from usernames
- **Language Barriers**: Roleplay languages that create authentic communication challenges
- **Server Management**: Tools to improve server experience and player coordination

## Problems It Solves

### Communication Problems
- **Global Chat Breaking Immersion**: Default chat reaches everyone regardless of distance
- **No Character Identity**: Players stuck with their usernames in roleplay scenarios
- **Lack of Communication Variety**: No whisper/yell mechanics or sign language
- **No Language Roleplay**: Missing authentic language barriers for different cultures/regions

### Server Management Problems
- **Save Lag Confusion**: Players don't know when server lag is due to saving
- **Sleep Coordination**: No way to encourage coordinated sleeping for time progression
- **Player Engagement**: Lack of statistics to track player achievements
- **Transportation Balance**: No controlled teleportation system for large worlds

## How It Should Work

### Core User Experience
1. **Proximity Chat**: Players join a "Proximity" chat channel where messages only reach nearby players
2. **Distance-Based Effects**: Messages become harder to read and smaller as distance increases
3. **Chat Modes**: Players can whisper (short range), talk normally, or yell (long range)
4. **Nicknames**: Players set character names that appear in roleplay contexts
5. **Languages**: Players learn languages and can only understand those they know

### Key Workflows

#### Setting Up Character Identity
```
/nick "Aldric the Blacksmith"
/nickcolor #8B4513
```

#### Communication Modes
```
/whisper "Meet me at the tavern"  # Short range, quiet
/say "Good morning everyone!"     # Normal range
/yell "Help! Wolves attacking!"   # Long range, loud
/hands "I agree"                  # Sign language, line of sight required
```

#### Language System
```
/addlang common                   # Learn a language
:tr "Feng tar kin ga"            # Speak in Tradeband
```

#### Out-of-Character Communication
```
(This is local OOC chat)         # Local out-of-character
((This is global OOC chat))      # Server-wide out-of-character
```

## User Experience Goals

### For Roleplay Players
- **Immersive Communication**: Feel like they're actually talking to nearby characters
- **Character Development**: Build unique character identities through names and languages
- **Authentic Interactions**: Experience realistic communication limitations and opportunities

### For Server Administrators
- **Flexible Configuration**: Customize every aspect to fit their server's needs
- **Performance Monitoring**: Track player engagement through statistics
- **Community Building**: Tools to encourage player interaction and coordination

### For General Players
- **Enhanced Social Experience**: More engaging ways to communicate and interact
- **Quality of Life**: Helpful notifications and convenience features
- **Optional Complexity**: Can use basic features without learning advanced systems

## Success Metrics
- **Adoption Rate**: Percentage of players actively using proximity chat and nicknames
- **Engagement**: Increased player interaction and communication frequency
- **Server Stability**: No performance degradation from mod features
- **Community Feedback**: Positive reception from roleplay communities
- **Configuration Usage**: Server admins actively customizing settings for their needs