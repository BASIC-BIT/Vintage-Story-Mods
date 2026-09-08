# The BASICs dice design interview

Status: Q1-Q13 accepted. Consolidated design awaits final owner review; no implementation approval. See [design](../superpowers/specs/2026-09-08-thebasics-dice-design.md).

## Objective and confirmed scope

Add chat/command dice rolling to The BASICs. Physical dice are outside current scope. The owner wants results to appear in Discord through Th3Essentials. The owner accepted following the existing proximity-chat relay setting, with no independent dice relay control.

The owner requested research if needed, followed by grill-with-docs in a worktree. The initial research is sufficient for the first design round. Further evaluator research follows the selected grammar; installed-version relay testing belongs to later explicitly authorized verification.

## Workspace and evidence

- Branch: codex/thebasics-dice-design.
- Base: c11dba7, the committed primary-checkout HEAD when this worktree was created.
- This worktree contains copies of all five 2026-09-07 dice research reports under docs/research/.
- The original source investigation also observed uncommitted spectator/chat work in the primary checkout. That code has intentionally not been copied here. Reconcile that work before implementation; observations of it are not claims about this branch.
- No product code changed. No build, dependency installation, deployment, manual QA, or Discord message was performed for this documentation setup.

## Settled decisions and current frontier

- Q1: Owner wants exploding dice, automated success pools, and general mathematical expressions in the initial scope, alongside standard dice, signed modifiers, keep-high/low and reasons. Do not silently reduce to the earlier small grammar. Exact syntax/semantics remain to be settled. The owner points out that deterministic tests/theories should make mechanics straightforward to verify.
- Q3: Follow the existing proximity-chat Discord relay setting. No separate dice-only relay setting or new bot.
- Q2 accepted: follow current whisper/normal/yell chat mode, independent of selected IC/OOC/global-OOC type, with complete readable results. Owner additionally requires a clear range marker, such as (W) or (Y), before whispered/yelled results. Use those markers in game and plain-text relay/history; normal unmarked is the proposed presentation.
- Evaluator research is complete at source level: see [exact-version evaluation](../research/2026-09-07-dice-evaluator-followup.md) and [deterministic acceptance cases](../research/2026-09-07-dice-acceptance-cases.md). DiceRoller 4.2.0 is the strongest evaluated candidate, conditional on resolving zero-draw group reroll caps, once-per-pool reroll semantics, and notation policy. No dependency choice is approved, no tests have been executed, and Q2 is now settled as above.

- Q4 command choice accepted: /roll with /thebasics roll fallback and bare-command help/examples. Reason separation accepted: allow unambiguous ordinary trailing text, support optional # and // boundaries, request a delimiter on ambiguity, and reject incomplete arithmetic rather than silently rolling a prefix.
- Q5 accepted: player-facing ! explosion syntax, reroll-once per eligible die, explicit strict/inclusive comparisons, and errors instead of silently capped results. Adapt or replace evaluator behavior as needed; the dependency itself remains unchosen.
- Q6 accepted: private rolls show results only to the requesting player, with no ordinary Discord relay. Selected recipients, GM and blind rolls are deferred; owner likes /gmroll as an eventual future feature. Command spelling, display label and staff-history policy are now settled in Q7/Q8 below.

- Q7 accepted: public /roll and /r; private /proll and /privateroll; namespaced fallbacks /thebasics roll and /thebasics proll. Private results use [Private Roll] without W/Y markers.
- Q8 accepted: private expressions, reasons and results stay out of shared chat logs/history and Discord. Normal command-usage analytics are allowed; do not include private roll contents.
- Q9 accepted with visual amendment: enabled by default, existing chat privilege, one server toggle covering public/private rolls, independent of OOC settings. The owner wants cutesy roll bubbles considered. This supersedes the earlier proposed no-bubble presentation for ordinary public rolls; the single-line dN shorthand is settled in Q10 below, with the simple nonstandard-expression rule settled in Q11 below. Private rolls remain self-only, and spectator identity-attached cue policy must be preserved.
- Analytics direction: owner wants good analytics for the rest of the dice feature too. Plan useful aggregate feature/usage signals through the existing infrastructure. Do not interpret this as authorization for an unrelated whole-repository analytics project.

- Q10 accepted with refinement: for standard dN, show a die icon containing its side count and the result beside it on one line, retaining W/Y markers and cute rounded styling. This replaces the earlier two-line mockup. Q11 below settles multi-die/advanced-expression presentation.

- Q11 accepted (2026-09-08): only a plain dN expression receives a numbered die icon. Every other expression receives a generic die icon plus its total or success count. Full details remain in chat. Do not implement formula-to-icon conversion, adaptive layouts, or width-dependent formula simplification.

- Q12 accepted: fixed modifier order explode -> reroll -> keep/drop -> count successes, regardless of written modifier order. Reroll replacements do not restart explosion.
- Q13 accepted: +, -, *, /, parentheses, decimal results, floor/ceil/round; no variables, scripts or user-defined functions. Reject fractional dice counts/sides.

Recommendations remain proposals until accepted. The user may answer r/rec for a question or all r/all rec for a round.
## Downstream decisions

After the first answers: exact syntax/default and help, command coexistence, feature enablement/privileges, identity/spectators, result formatting, limits/cooldown, history and event semantics, private rolls if desired, evaluator selection and packaging. Resolve only questions that remain material; do not invent extra scope.

Maintain a glossary in CONTEXT.md as terminology is agreed. Create ADRs only for decisions with meaningful reversal cost, surprising rationale, and genuine alternatives. Do not treat every configuration default as an ADR.

## Verification and retirement

Before implementation, recheck the latest relevant source and record an agreed design plus verification plan. This packet retires when the owner confirms shared understanding and a successor implementation handoff supersedes it.
