# Memory Bank Update Process

## Implementation Workflow

When implementing new features or making significant changes, follow this structured process:

### 1. Implementation Phase
- Complete the requested implementation
- Create/modify files as needed
- **DO NOT** automatically update memory bank files

### 2. Validation Phase
- Present the implementation to the user
- Wait for user confirmation that functionality works as expected
- User should test the implementation before proceeding

### 3. Log Analysis Phase
- After user confirms functionality, check relevant logs:
  - Client logs: `C:\Users\steve\AppData\Roaming\VintagestoryData\Logs`
  - Server logs: Use `.\mods-dll\thebasics\scripts\fetch-logs.ps1` to fetch from remote server
  - Build logs: Check for any compilation or deployment issues
- Analyze logs for errors, warnings, or unexpected behavior
- Report findings to user

### 4. Memory Bank Update Phase
- **Only after** user validation AND log analysis
- User must explicitly request memory bank update with **"update memory bank"**
- When triggered, review ALL memory bank files systematically
- Focus particularly on:
  - `activeContext.md` - Current state and recent changes
  - `progress.md` - What works and what's left to build
  - Other files as relevant to the changes made

## Key Principles

### Never Auto-Update
- Memory bank updates require explicit user request
- Implementation completion ≠ automatic documentation update
- Always validate functionality first

### Systematic Review
- When updating memory bank, review ALL files even if some don't need changes
- Ensure consistency across all memory bank documents
- Document lessons learned and new patterns discovered

### User-Driven Process
- User controls when memory bank gets updated
- Roo should prompt user through the workflow steps
- User validates each phase before proceeding to next

## Workflow Prompts

After implementation completion:
1. "Implementation complete. Please test the functionality and confirm it works as expected."
2. After user confirmation: "Now let's check the logs to ensure everything is working properly."
3. After log analysis: "Logs look good. Would you like me to update the memory bank with these changes?"

## Memory Bank Update Trigger

User says **"update memory bank"** → Review ALL memory bank files systematically, focusing on current state and recent changes.