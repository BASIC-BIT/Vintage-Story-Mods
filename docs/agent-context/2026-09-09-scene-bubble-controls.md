# Scene bubble controls

BASIC requested a per-marker idle-bobbing switch, a separate switch for including description text in the floating bubble, and stationary bubbles.

- Idle bobbing defaults on for existing/new markers. Protobuf member18 uses DefaultValue(true) so false survives serialization; tree attribute sceneIdleBobbing defaults true.
- Show description in bubble defaults off. Member19 and sceneShowBodyInBubble persist the preference. Both controls follow save/defaults/pickup and existing lock restrictions.
- Hidden descriptions produce an escaped title plus a smaller localized Right-click to read cue. Untitled markers with body text show the cue alone; title-only markers show the title without a misleading read cue. On interaction still suppresses floating bubbles. Full reader and inspector content remain available.
- Only icon drawing receives the idle sine offset. Text layout and position use the fixed height anchor, with clearance for the maximum bob/focus envelope, so neither animation shifts the bubble.

Validation: 794 automated tests passed, including true/false and legacy protobuf behavior, persistence/defaults and floating text formatting. Manual QA remains pending.

QA cards:
1. Enable bobbing. Observe the icon moving while the bubble stays still, including when aiming at and away from it. Disable bobbing, save/reopen: icon should stop at its configured height.
2. Leave Show description in bubble off. Expect title and Right-click to read; right-click opens the full body. Enable it and save: expect title and description in the bubble. Compare preview and world.
3. Check an untitled marker with a body, then a title-only marker. Expect reading cue alone for the first and title alone for the second. On interaction should still show no floating bubble.
4. Lock/reopen: both switches disabled. Pick up/replace and create a fresh marker: settings persist and fresh defaults copy appearance without content.

Both review axes found no blockers. Standard package build passed, packaged DLL matches tested output, and packaged JSON parses. Package SHA256 F8AAAA0A340A25F8A73AA0D339742EFA4833A7BC50CAE4AFCF14C2B0F958CB35 was verified on server 8982de16 and both QA profiles; previous packages preserved.

BASIC follow-up: removed the Right-click to read cue entirely. Hidden descriptions show only the title; untitled markers with hidden bodies show no bubble. Full reading remains available. Simplified color labels to Gold, White, Blue and Green; palette values unchanged.
