# Public Vintage Story Release Notes

Use this reference when drafting, reviewing, or converting public release notes for GitHub or Vintage Story ModDB. Treat the owner's final edits as stronger taste evidence than an agent draft.

## Durable requirements

These are explicit repository or owner rules:

1. Verify every claim against the shipped source and exact packaged artifact. Distinguish command availability, default settings, privileges, upgrade behavior, compatibility, dependencies, and optional-provider behavior.
2. Aggregate release-relevant merged work since the previous release, then order the body by reader value rather than commit order.
3. Lead with new player or administrator capabilities and high-value requested fixes. Put secondary improvements after them. Omit implementation trivia and routine housekeeping unless users need it.
4. Translate internal settings into user choices. Explain what is available, what defaults on or off, what requires a restart, and what a server owner can choose. Mention an internal key only when it helps the owner act.
5. Keep optional companions proportionate to their user impact. State plainly when a promoted capability is unavailable without an unpublished or unconfigured provider.
6. Omit installation boilerplate. Include unusual distribution instructions only when the owner explicitly requests them or users otherwise cannot obtain the release safely.
7. Prepare one canonical body for GitHub and ModDB. Change only platform formatting unless a platform-specific fact genuinely differs.
8. Write the ModDB body in HTML: `<h2>`, `<h3>`, `<p>`, `<ul>` with `<li>`, `<b>`. The GitHub body stays Markdown. Confirmed at the v5.9.0 upload. Releases before 5.9.0 carry a `04-moddb-richtext.md` drafted in BBCode, which the editor does not take; those bodies were reworked by hand at upload time, so they are not evidence of a working format. Do not copy their markup.
9. Before posting, present each exact platform-ready body verbatim and show its rendered preview for owner approval.
10. Never put `[AGENT]` in a public release body.
11. Never use an em dash in public release-note copy. Use a comma, colon, parentheses, semicolon, or sentence break instead.

## Attention and voice

- Use short, descriptive headings built around features or outcomes.
- Start with the content. Avoid throat-clearing, process narration, patch apologies, and claims that a release is exciting, major, comprehensive, or polished.
- Be direct, concrete, and human. Prefer "These commands are opt-in" to a paragraph explaining the implementation or defending the decision.
- Keep command lists when they help players understand the shipped surface. Pair them with the few defaults, limits, and permissions that change actual use.
- Give a meaningful fix its own heading when affected users need recovery steps.
- State limitations beside the affected feature when they materially change whether it works. Use a footnote only for genuinely secondary context.
- Add links only when they earn their space. A full-changelog link is optional, not a default requirement.

## Evidence from the v5.8.1 owner edit

The owner-edited ModDB body was about 18 percent shorter than the fact-corrected Fable draft (509 versus 619 measured words). Treat the following as observed tendencies, not universal rules:

- The owner cut the introductory framing, including the "delivered the way it should have shipped" corrective story, and began with the first feature heading.
- The owner kept the attention order: teleport commands, Sign recovery, map privacy, then smaller improvements. This supports the existing value-first hierarchy without proving that every release needs those sections.
- The owner removed the rationale about why different server types may not want teleportation, but kept the direct opt-in instruction and restart requirement.
- The owner retained the detailed teleport command list, user-facing defaults, same-dimension `/back` limit, Sign recovery instructions, map privacy explanation, and administrator settings summary.
- The owner moved the optional semantic-learning limitation into the feature bullet and stated that the unpublished provider made the feature non-operational for now.
- The owner omitted the standalone compatibility section, separate companion footnote, and full-changelog link. Infer a preference for pruning secondary material, not a blanket ban on compatibility notes, footnotes, or links.
- The owner used compact feature headings and plain statements rather than release-management framing.

## Short before-and-after examples

### Start with the feature

Before:

> This is the full feature release, delivered the way it should have shipped.

After:

> ## New teleport commands, off by default

Use the second approach when the framing adds no information the reader needs.

### Make the choice direct

Before:

> Many servers already run a teleport mod, and many RP servers do not want easy teleportation.

After:

> **These commands are opt-in.** Enable just the families you want, then restart.

Keep rationale only when it changes a decision or prevents a likely mistake.

### Put a blocking caveat beside the feature

Before:

> Describe gradual language learning, then explain provider availability in a distant footnote.

After:

> This requires an optional provider that has not yet been published, so the feature is non-operational for now.

Do not make a capability sound usable and defer the blocking condition.

### Replace em dashes

Avoid:

> Custom nametag colors [U+2014] servers can set a background and border color.

Prefer:

> Custom nametag colors: servers can set a background and border color.

## Draft and publication checklist

1. Identify the user-visible changes from merged PRs, tags, source, and the packaged artifact.
2. Rank commands and high-value fixes first, then gameplay, administration, compatibility, and minor details.
3. Draft in user language, adding exact config keys only where they enable action.
4. Check defaults, privileges, restart requirements, upgrade behavior, dependencies, compatibility, and feature gates.
5. Remove duplicated rationale, implementation vocabulary, install boilerplate, stale availability claims, and low-value links.
6. Check that unavailable optional functionality is labeled beside the claim.
7. Search the final public body for `[AGENT]` and U+2014; both counts must be zero.
8. Convert formatting for each platform without changing visible wording.
9. Present each complete platform-ready body verbatim, validate its rendered preview, and obtain owner approval.
10. Re-read both live pages after publication and compare their visible text with the approved canonical body.
