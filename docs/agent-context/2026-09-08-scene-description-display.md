# Description display and reading controls

BASIC approved replacing Environmental/OOC styling with Show description: When targeted, Always nearby, or On interaction. Always nearby shares the billboard's configured range and fade. Plain right-click reads the full description in every mode; Shift-right-click edits. The top-middle inspector shows the title plus a short preview.

Implementation:
- Display stored in sceneDisplay and appended at protobuf member 11, independently of the retired kind field. Old/invalid values default to When targeted. Editing remains subject to the existing server lock, claim and distance checks.
- Floating descriptions use depth-tested world billboards. Always nearby fades with the indicator. Targeted descriptions remain readable while selected even if the configured icon range is shorter than interaction range. On interaction renders no floating text.
- Long floating previews are bounded to 600 characters and 12 explicit lines, with ellipsis. The 180-character inspector preview is escaped, and the right-click book retains the full saved body. This avoids extremely tall text textures for long or newline-heavy content.
- Textures are cached for visible descriptions and disposed when they stop being shown or the marker unloads. Existing symbol, idle bob, UI locks, distance and Unlimited controls remain.

QA: compare three markers with identical text and different display modes. Check targeted-only versus always-nearby versus no floating text; shared fade and wall occlusion; plain right-click reading for all three, including locked markers and claim visitors; shift editing permissions; inspector preview, save/reopen and pickup/replacement persistence. Check text placement/readability at close range and steep camera angles. Visual acceptance remains pending.

Verification: 771 tests passed. Standard package build passed; packaged DLL matches the passing test output and packaged JSON parses. Candidate SHA256 9832492A11C02252839439DDFD6E7E20C08F53F2205AEAC05DB923DDBD4AD317. The review correction expands culling to include the description, caps panel geometry at six blocks high while preserving aspect ratio, offsets text along camera-up and sorts billboard groups by camera depth.

Both independent review rechecks found no remaining blockers. QA package uploaded to test server 8982de16 and both local profiles with matching SHA256 readbacks; previous packages backed up under .tmp/scene-display-qa-2026-09-08. In-game visual acceptance remains pending.
