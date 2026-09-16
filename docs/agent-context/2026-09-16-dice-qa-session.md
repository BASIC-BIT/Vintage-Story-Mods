# Dice QA session, 2026-09-16

Owner approved normal audited private-command dispatch, merging main into PR245, QA deployment and starting manual QA.

Tested code: eb2d3d1, includes origin/main 8d2a185 and patch removal 50a7da4.
Validation: 882 tests passed with full GUI fixtures enabled. Canonical build/package succeeded.
Package: thebasics_5_9_1.zip
SHA256: 7835A6A42DA2A18ABFA346404E803D64D108AB4D235830A65BFE21C362E0A805
Verified by downloading the uploaded server package and comparing both local QA profile copies.

Server 8982de16 is running Vintage Story 1.22.7. The BASICs mod systems loaded. Profile2 connected and received world assets; Profile3 launched at main menu.
Session config: EnableDiceRolling=true, EnableTh3EssentialsDiscordRelay=true, EnableChatHistory=true, OverheadChatBubbleMode=RpText. Whisper/normal/yell ranges: 5/35/90.
Original config, logs and previous client packages are preserved under .superpowers/sdd/dice-qa. Restore original config after the QA session.

Boot findings: duplicate flywheelpower ID; three saved TheBasicsSceneDescription blockentity load failures. No dice startup exception observed. These world/mod-set findings are not silently treated as a clean boot; follow up separately from dice observations.
Discord bridge is configured and enabled, but delivery is not yet human-verified.

All manual cards remain pending. Private command arguments in vanilla audit logs are expected, not a failure. Private results must remain absent from other clients, bubbles, shared history and Discord.

## First owner observations

Owner reported redundant nested brackets and kept labels on ordinary rolls, and unexpected constant evaluation for /r 20. Requested bold numerical totals. Other observations were broadly positive, without enough detail to check off complex range/privacy/relay cards.
Changes for focused retest: bare unsigned integer expressions roll dN; ordinary dice omit kept labels unless a drop actually occurred; plain dN omits redundant face list; chat totals are bold. Arithmetic constants remain unchanged. All 892 tests pass.
