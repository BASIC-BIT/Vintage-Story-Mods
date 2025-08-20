# VTML Escaping in thebasics Mod

## Overview
VTML (Vintage Story Text Markup Language) is the markup language used by Vintage Story for text formatting in chat and UI elements. It supports HTML-like tags such as `<strong>`, `<i>`, `<font>`, etc.

## The Problem
Unescaped angle brackets (`<` and `>`) in chat messages can break VTML parsing, causing:
- Error messages in client logs: "Found closing tag char > but no tag was opened"
- Potentially broken chat rendering for all players

## How Vintage Story Handles This

### Normal Chat Messages
When a player types in normal chat, Vintage Story automatically escapes angle brackets ONLY:
- `<` becomes `&lt;`
- `>` becomes `&gt;`
- **Note**: Ampersands (`&`), quotes (`"`), and apostrophes (`'`) are NOT escaped

This happens in `ServerMain.cs` before the message is processed by mods:
```csharp
message = message.Replace(">", "&gt;").Replace("<", "&lt;");
```

### Command Arguments
Command arguments (e.g., `/ooc <test>`) are **NOT** pre-escaped by Vintage Story. The raw text is passed directly to the command handler.

## Our Solution

### 1. VtmlUtils Class
A centralized utility class for VTML escaping/unescaping:
- `EscapeVtml()` - Escapes only `<` and `>` (matching VS behavior)
- `EscapeVtml()` - Same as minimal (VS only escapes `<` and `>`)
- `UnescapeVtml()` - Reverses the escaping (also handles `&nbsp;`)

### 2. CommandMessageEscapeTransformer
- Runs early in the message pipeline
- Only processes messages with `IS_FROM_COMMAND` flag
- Escapes angle brackets to match normal chat behavior

### 3. Nickname Handling
- Nicknames are escaped using `VtmlUtils.EscapeVtml()` 
- Players can use `<` and `>` in nicknames - they'll display correctly as literal characters
- The escaping prevents VTML parsing issues while maintaining visual appearance

### 4. No Blocking
- We do NOT block angle brackets in any messages
- Users can type `<` and `>` freely in both normal chat and commands
- Everything is properly escaped to display as intended

## HTML/XML Entity Reference

Vintage Story only uses these entities in chat:
- `<` → `&lt;`
- `>` → `&gt;`
- ` ` → `&nbsp;` (non-breaking space)

Note: VS does NOT escape `&`, `"`, or `'` in chat messages

## Key Findings

1. **Vintage Story uses minimal escaping** - Only `<` and `>` are escaped in normal chat
2. **Commands bypass escaping** - Raw text is passed to command handlers
3. **VTML parser is strict** - Unmatched tags cause errors but don't crash the game
4. **Entity references work** - `&lt;` and `&gt;` are properly rendered as `<` and `>`

## Migration Notes

### For Existing Servers
- Existing nicknames with angle brackets will now display correctly
- No manual migration needed - the escaping happens automatically
- Old format `[less-than]` and `[greater-than]` will no longer be used

### Breaking Changes
- None - this is backward compatible
- Nicknames that previously showed `[less-than]` will now show `<`

## Configuration Options (Future)

Consider adding:
- `AllowVTMLInNicknames` - Allow intentional VTML in nicknames (default: false)
- Would require proper VTML validation to prevent breaking chat

## Testing

Test these scenarios:
1. Normal chat with `<test>` - Should display as `<test>` (VS escapes it)
2. `/ooc <test>` - Should display as `<test>` in chat
3. `/gooc <test>` - Should display as `<test>` globally  
4. `/nick <MyName>` - Should work and display as `<MyName>`
5. Bold/italic formatting with custom delimiters still works
6. Ampersands `&` and quotes `"` `'` work everywhere without issues