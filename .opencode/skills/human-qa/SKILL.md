---
name: human-qa
description: Structured workflow for manual QA verification before release. Covers when to trigger QA, how to generate test cards from diffs and checklists, batching by config state, strict verification standards, failure handling, and discovery triage.
compatibility: opencode
metadata:
  audience: maintainers
  domain: qa-workflow
---

## When to trigger this workflow

Use risk-based judgment with two axes:

1. **Can a robot verify it?** If the change is purely structural (renames, refactors with no behavioral change, config wiring) and the build passes, manual QA adds little value. If it touches rendering, UX, timing, client-server interaction, or anything that requires a running game client to observe — it needs human eyes.

2. **Effort vs. risk tradeoff.** High-risk changes (new UI systems, networking changes, Harmony patches against vanilla code) justify thorough multi-batch QA even if setup is tedious. Low-risk changes with high verification effort (e.g., "does this tooltip look 1px different?") can be skipped or deferred. Note: things that are annoying to test are often higher risk by nature — if it's hard to verify, it's probably easy to break.

**Rules of thumb:**
- PR adds or modifies client-side rendering? **Trigger QA.**
- PR changes server config behavior? **Trigger QA** (at least for the affected config paths).
- PR touches Harmony patches? **Trigger QA** — vanilla behavior assumptions are fragile.
- PR is a pure server-side refactor with no observable behavior change? **Skip QA** (build verification is sufficient).
- PR adds a new command? **Trigger QA** for the command; regression-test nearby commands if they share infrastructure.

---

## Phase 1: Generate QA cards

### Source material

Analyze **both** the PR diff and the PR's Manual Verification Checklist (if one exists). The diff is the primary source of truth — the checklist is a cross-reference. Flag gaps in either direction:

- Items in the checklist that the diff doesn't actually touch → ask whether they're still relevant or stale
- Behavioral changes in the diff that the checklist doesn't cover → propose new checklist items

### Card format

Each QA card is a numbered test item with these fields:

```
N. **[Short name]** (P0|P1|P2)
   - Config: [Required config state, or "any"]
   - Do: [Exact steps the human should perform]
   - Expect: [What they should observe if it works]
   - Watch for: [Specific failure mode or regression to look out for]
```

**Severity definitions:**
- **P0 — Blocks release.** Core functionality broken, data loss, crash. Must pass before merge.
- **P1 — Should fix before release.** Noticeable bug, wrong behavior under common config. Strongly prefer fixing before merge; can defer with a tracked issue only if the fix is high-risk and the bug is cosmetic.
- **P2 — Nice to verify.** Edge cases, cosmetic polish, interactions with uncommon config combinations. Can defer to a follow-up issue if QA time is tight.

### Qualities of a good QA card

- **Concrete steps**, not abstract descriptions. "Type `hello` in chat and look above Player2's head" — not "test chat bubbles."
- **Observable expected outcome.** "You should see a speech bubble with `hello` in white text that fades after ~4 seconds" — not "bubble should work."
- **Specific failure modes.** "If LOS is broken, the bubble will appear even when Player2 is behind a wall" — not "check for bugs."
- **Numbered items** so reporting is unambiguous. "Card 3 passed, card 4 failed — saw X instead of Y."

---

## Phase 2: Batch cards by environment

### Primary axis: server config state

Group cards that share the same config state into a batch. Each batch requires a server restart, so minimizing batches minimizes downtime and human wait time.

Example from a real session:
- **Batch 1:** Base config (`OverrideSpeechBubblesWithRpText=true`, `EnableLanguageSystem=false`)
- **Batch 2:** Language system enabled (`EnableLanguageSystem=true`)
- **Batch 3:** Debug mode (`DebugMode=true`, production settings otherwise)

### Secondary axis: feature area

Within a batch, group related cards together. If the human is already looking at chat bubbles, test all bubble-related cards before switching to typing indicators. This reduces cognitive context-switching.

### Present batches upfront

Before starting QA, present the full batch plan to the human so they know the scope:

```
I've organized QA into 3 batches:

**Batch 1** (base config, 8 cards): Core bubble rendering, typing indicators, save notifications
**Batch 2** (language system on, 5 cards): Language obfuscation in bubbles, language color, sign language
**Batch 3** (debug mode, 2 cards): Perf logging, debug overlays

Each batch needs a server restart + client relaunch. Ready to start Batch 1?
```

---

## Phase 3: Execute QA batches

### Environment setup (Claude's job)

Before presenting cards to the human, handle all environment setup yourself:

1. Update server config (via Pterodactyl API or config scripts)
2. Build and deploy the mod
3. Restart the server
4. Wait for clean boot — verify logs (all mod systems loaded, no exceptions, no duplicate mod warnings)
5. Tell the human to relaunch game clients

Do NOT repeat connection details, IPs, or other setup the human already knows. Keep it to: "Server is back up with [config changes]. Please relaunch both clients."

### Present the batch card

Give all cards for the batch at once. The human can work through them at their own pace and report back in whatever order makes sense.

### Collect results

When the human reports back:
- **For passes:** Confirm specifically before checking off. Example: "Marking LOS test as passed — you confirmed the bubble disappeared when you moved behind the wall and reappeared when you stepped back out. Correct?" The human must describe what they observed, not just say "pass."
- **For failures:** Record exactly what the human saw. Investigate, propose a fix.
- **For skips:** Note why and whether it blocks the PR.

### Challenge vague responses — proportionally

The value of manual QA is that someone actually tested it. But the level of verification detail you demand should be **proportional to the complexity of what's being tested.**

**High-complexity cards** (rendering behavior, timing, multi-step interactions, edge cases with specific failure modes): Push back on vague responses. Ask what they observed. These are the cards where "looks good" is meaningless — the failure mode is subtle and the human needs to describe what they actually saw.

**Low-complexity / binary cards** (e.g., "does this show English text or a raw key?", "does the command respond at all?"): A batch confirmation like "all 7 passed" is acceptable when the pass/fail criteria is obvious and binary. The human doesn't need to recite the exact string back to you. Trust their judgment when the observation is trivial.

**Rules of thumb for pushback:**
- If the expected outcome requires _interpretation_ (timing, positioning, visual appearance, interaction between systems) → ask for specifics.
- If the expected outcome is _self-evident_ (readable text vs. raw key, command works vs. errors) → accept batch confirmation.
- If the human confirms a batch but one card had a non-obvious expected outcome mixed in → ask about that specific card, not all of them.

| Human says | Card complexity | You respond |
|---|---|---|
| "All passed" | All binary/simple | Accept. Mark them off. |
| "All passed" | Mix of simple + complex | "Great — for card N [the complex one], can you describe what you saw?" |
| "Looks good" | Complex behavioral test | "Can you describe what you saw? Did [expected outcome] happen?" |
| "I think so" | Any | "Let's be sure. Can you try [specific repro steps] one more time?" |

**Never check off a complex behavioral PR checklist item without explicit human confirmation describing what they observed.** For simple binary checks, batch confirmation is sufficient.

---

## Phase 4: Handle failures

When a QA card fails:

### 1. Diagnose and fix
Investigate the failure using logs, code review, and the human's description. Implement a fix in code.

### 2. Rebuild and redeploy
Build → deploy → restart server → verify clean boot via logs.

### 3. Re-test with smart scoping
You have flexibility here — use judgment to minimize total round-trips:

- **If the fix is isolated** and the current batch has remaining untested cards: re-test the failed card alongside the remaining cards in this batch. Don't make the human redo cards that already passed.
- **If the fix could affect other cards** in the current batch: re-test the failed card plus any cards whose behavior could be impacted.
- **If the failed card's config requirements match the next batch**: consider rolling the re-test into the next batch instead of doing an extra restart now. This is the preferred approach when configs don't conflict — it reduces total round-trips.
- **If the fix is risky or touches shared infrastructure**: re-test the entire current batch.

The goal is correctness with minimum round-trips. Be fluid — don't rigidly re-test a single card if you're about to restart anyway, and don't re-test everything if only one isolated path changed.

---

## Phase 5: Discovery triage

During QA, the human will often notice things that aren't covered by the current cards: new bugs, UX improvements, edge cases, feature ideas. These are valuable — but they should not block the current PR unless they're P0.

### Formalized triage pause

After completing each batch (or after all batches if the session is short), explicitly pause to triage discoveries:

```
Before we move to Batch 2, let's triage what we found:

1. [Description of discovery] — I'd classify this as [P0/P1/P2/enhancement].
   Recommendation: [fix now / file issue / defer]

2. [Description of discovery] — ...

Which of these should we address before continuing?
```

### Triage rules

- **P0 in-scope:** Fix now, before continuing QA. The batch is invalid if a P0 is present.
- **P1 in-scope:** Strongly prefer fixing before merge. If the fix is simple and low-risk, do it now. If it's complex, file an issue and discuss with the human whether it blocks merge.
- **P2 or out-of-scope:** File a GitHub issue with a clear description, expected behavior, and any relevant context. Do not block the PR.
- **Enhancement ideas:** File as GitHub issues. Tag appropriately. These are gifts from the QA process — capture them, don't lose them.

When filing issues, include enough context that someone picking up the issue later doesn't need to re-discover the problem. Reference the PR where it was found.

---

## Phase 6: Update the PR checklist

After all batches are complete:

1. Check off all items that passed with human confirmation
2. Note any items that were deferred with issue numbers
3. Add any new checklist items that emerged from QA (and their pass/fail/defer status)
4. Summarize the QA session in a PR comment or update the PR description

---

## Anti-patterns

- **Checking items off in bulk without individual confirmation.** Each item gets its own explicit confirmation.
- **Assuming visual correctness from code review alone.** If it's on the manual checklist, it needs manual eyes.
- **Skipping re-test after a fix.** If you changed code to fix a failure, the human re-tests that item (at minimum).
- **Letting the human rush through.** Better to leave items unchecked than to check them dishonestly.
- **Filing no issues for discoveries.** If QA surfaced something, capture it. The human took the time to notice it — don't let it evaporate.
- **Rigid re-test scoping.** Don't mechanically re-test everything or mechanically re-test only one card. Think about what the fix actually touched and minimize round-trips.
- **Presenting QA cards without environment setup.** The human should never have to configure the server or figure out what config state they need. That's your job.
- **Repeating setup instructions the human already knows.** Don't re-explain how to connect to the server every batch. Keep it to what changed.

---

## Example: complete QA session flow

```
[Claude analyzes diff, generates 15 cards across 3 batches]

Claude: "I've organized QA into 3 batches. Batch 1 (base config, 8 cards), Batch 2
        (language on, 5 cards), Batch 3 (debug, 2 cards). Ready?"