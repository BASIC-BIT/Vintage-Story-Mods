# Error Messaging Guidelines

## User-Friendly Error Messages

When writing error messages for users, follow these guidelines to maintain a human and helpful tone:

### ❌ Avoid Corporate Language
- **Don't say**: "Please contact an admin"
- **Don't say**: "Contact support"
- **Don't say**: "This is a system error"

### ✅ Use Human, Helpful Language
- **Do say**: "This is probably a mod bug, please report this!"
- **Do say**: "Something went wrong - this might be a bug"
- **Do say**: "This shouldn't happen - please let us know!"

### Rationale
- **Corporate messaging** feels impersonal and unhelpful
- **Bug reporting language** encourages community participation
- **Honest communication** builds trust with users
- **Clear attribution** helps users understand the issue source

### Examples

#### Bad Error Messages
```csharp
"Failed to return temporal gear - please contact an admin."
"System error occurred - contact support."
"An unexpected error has occurred."
```

#### Good Error Messages
```csharp
"Failed to return temporal gear - this is probably a mod bug, please report this!"
"Something went wrong with the teleport system - this might be a bug!"
"This shouldn't happen - please let us know about this error!"
```

### Implementation Notes
- Apply this to all user-facing error messages
- Log technical details separately for debugging
- Encourage community engagement through bug reporting
- Maintain transparency about potential issues

This approach helps build a more engaged and helpful community around the mod.