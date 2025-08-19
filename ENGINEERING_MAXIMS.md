# Engineering Maxims for AI-Assisted Development

## Core Principles

### 🩺 **Treat the Disease, Not the Symptom**
- Root cause > Workarounds
- Fix broken abstractions instead of working around them
- One consistent path > Multiple fallback paths

### 🔍 **Fail Fast, Fail Clear**
- Silent failures are bugs waiting to happen
- Error messages should point to exactly what's wrong
- Hard stops > Graceful degradation for build systems

### ⚡ **Simplicity Wins**
- Fewer moving parts = Fewer failure modes
- Standard practices > Custom solutions
- Delete code > Add code when possible

### 📏 **One Source of Truth**
- Avoid configuration duplication
- Single point of control for each concern
- Conventions > Configuration when possible

## Tactical Guidelines

### Build Systems
```
✅ DO: Use standard toolchain output locations
❌ DON'T: Create custom output path schemes

✅ DO: Let tools handle their own directory structures  
❌ DON'T: Override tool defaults without strong reason

✅ DO: Fail immediately on missing dependencies
❌ DON'T: Search multiple locations for the same file
```

### Error Handling
```
✅ DO: Make errors obvious and actionable
❌ DON'T: Add fallback logic for configuration issues

✅ DO: Stop execution on unexpected conditions
❌ DON'T: Continue with degraded functionality in build scripts
```

### AI Collaboration Patterns
```
✅ DO: Question complexity before implementing it
❌ DON'T: Assume existing complexity is necessary

✅ DO: Prefer deleting code over adding conditional logic
❌ DON'T: Layer new logic on top of broken foundations

✅ DO: Ask "Why are we doing this?" before "How do we do this?"
❌ DON'T: Implement requirements without understanding the problem
```

## When in Doubt

1. **Step back** - Is this actually solving the right problem?
2. **Strip down** - What's the minimal implementation that works?
3. **Standard first** - Does the ecosystem already solve this?
4. **Fail loudly** - Will this fail obviously when it breaks?

> *"The best code is no code. The second best code is boring code."* 