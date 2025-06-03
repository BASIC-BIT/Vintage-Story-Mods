# Debugging Methodology

## Systematic Problem Diagnosis

When debugging issues in The BASICs mod, follow this structured approach:

### 1. Multiple Hypothesis Generation
- Consider 5-7 different possible sources of the problem
- Don't jump to conclusions based on symptoms alone
- Think about: network issues, null references, config problems, API usage, build issues, timing problems, data corruption

### 2. Hypothesis Prioritization
- Distill potential sources down to 1-2 most likely causes
- Consider frequency of similar issues in the codebase
- Prioritize based on error messages and stack traces

### 3. Log-Driven Investigation
- Always use server logs to validate assumptions
- Use `.\mods-dll\thebasics\scripts\fetch-logs.ps1` to get recent server logs
- Check both client logs (`C:\Users\steve\AppData\Roaming\VintagestoryData\Logs`) and server logs
- Look for exact error locations and stack traces

### 4. Validation Before Fixing
- Add logs or test commands to validate your diagnosis
- Explicitly ask the user to confirm the diagnosis before implementing fixes
- Create isolated test cases when possible (like `/tpatest` command)

### 5. Build Process Verification
- Always use `.\mods-dll\thebasics\scripts\build-and-package.ps1` instead of just `package.ps1`
- Verify that changes are actually being deployed (check for stale builds)
- Test the fix in the actual environment, not just in theory

## Common Issue Patterns

### Network Timing Issues
- Symptoms: "Attempting to send data to a not connected channel"
- Solution: Check `channel.Connected` before sending packets
- Pattern: Implement retry mechanisms with queuing

### Null Reference Exceptions
- Symptoms: "Object reference not set to an instance of an object"
- Investigation: Check for optional parameters, missing config values, uninitialized objects
- Pattern: Add null checks and default value handling

### Incomplete Feature Implementation
- Symptoms: Features not working despite being "implemented"
- Investigation: Look for TODO comments that were never completed
- Pattern: Check if the feature was actually implemented or just stubbed out

### Build and Deployment Issues
- Symptoms: Changes not taking effect despite code modifications
- Investigation: Check if DLLs are being rebuilt and deployed correctly
- Pattern: Use `build-and-package.ps1` to ensure fresh builds

## Testing Framework Approach

### Create Isolated Test Commands
- Use admin-only commands (`Privilege.controlserver`) for testing
- Create single-player test scenarios when possible
- Remove test code after validation is complete

### Example Test Command Pattern
```csharp
API.ChatCommands.GetOrCreate("testcommand")
    .WithDescription("Test specific functionality")
    .RequiresPrivilege(Privilege.controlserver)
    .RequiresPlayer()
    .WithArgs(new StringArgParser("action", true))  // Required parameters
    .HandleWith(HandleTestCommand);
```

## User Validation Process

### Always Confirm Diagnosis
- Present your hypothesis to the user before implementing fixes
- Ask for explicit confirmation that the diagnosis makes sense
- Don't assume you understand the problem without user validation

### Test in Real Environment
- Have the user test the fix in their actual environment
- Don't rely solely on theoretical fixes or isolated testing
- Wait for user confirmation that the issue is resolved

### Document Learnings
- Update memory bank with key insights from debugging sessions
- Document patterns that can help with future similar issues
- Record both successful approaches and dead ends to avoid repeating mistakes

## Key Debugging Tools

### Server Log Analysis
- `.\mods-dll\thebasics\scripts\fetch-logs.ps1` - Get server logs
- Look for stack traces, error messages, and timing information
- Check both current logs and archived logs if needed

### Build Verification
- `.\mods-dll\thebasics\scripts\build-and-package.ps1` - Ensure fresh builds
- Check file timestamps to verify deployment
- Test changes immediately after deployment

### Code Investigation
- Use VS source code at `D:\bench\vs_source` for API understanding
- Check git history to understand how code evolved
- Look for TODO comments and incomplete implementations

## Success Criteria

### Diagnosis Confirmed
- User agrees with the root cause analysis
- Evidence supports the hypothesis (logs, code inspection, testing)
- Fix addresses the actual problem, not just symptoms

### Fix Validated
- User confirms the issue is resolved in their environment
- No new issues introduced by the fix
- Solution follows established patterns and best practices

### Documentation Updated
- Memory bank reflects the new understanding
- Patterns documented for future reference
- Rules updated if new insights about debugging process discovered