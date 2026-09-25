# Scene marker physical plate QA

Change: always-visible stone plates on ground, walls, and ceilings. The plate and floating icon select the same marker. No new toggle. Automated verification does not replace these visual checks.

Status: package staged on the disposable test server and Profile2/Profile3 with owner approval on September 23. Human visual checks are pending. Record observations before marking any card passed.

One batch with EnableSceneMarkers=true. The exact package is staged on the test server and both client profiles. The server reached RunGame with SceneDescriptionSystem loaded; Profile2 loaded the mod and connected, and Profile3 was launched for manual connection.

1. **Short-distance floor marker (P0)**
   - Do: Place on a full floor block. Add a title and body, select When targeted, set icon and text distances to 1. Stand about two blocks away within normal interaction reach and aim at the stone plate.
   - Expect: The plate is visible and lit by the world, its outline follows the plate, and its targeted bubble appears despite the faded icon. Right-click reads; Shift-right-click edits with permission.
   - Watch for: An invisible hitbox, floating-cube outline, blocked access, or an unread bubble remaining hidden. Mark it unread first if necessary.
2. **Placement faces (P0)**
   - Do: Place on each wall orientation and under a full ceiling block. Pick up and place a ceiling marker again on the floor. Change its floating icon height.
   - Expect: Each plate lies against the clicked surface, remains targetable, keeps its content after pickup, and stays fixed while only the icon height changes.
   - Watch for: Ceiling plate at the bottom of the empty block, flipped wall placement, hovering plate, or item content loss.
3. **Reach, occlusion, and damage (P1)**
   - Do: Aim from outside normal reach, then through an intervening solid block. Return within reach, partially break the plate and look away. Repeat on its visible icon.
   - Expect: No interaction beyond normal reach or through terrain. Each outline and crack effect belongs to the damaged part. Healing cracks stay on that part after looking away. Walking across the plate does not obstruct movement.
   - Watch for: Invisible targets intercepting clicks, cracks jumping between parts, or a new movement obstacle.
4. **Reading and permissions (P1)**
   - Do: Read and mark a marker as read, aim at its plate, then mark unread. Lock the marker and attempt editing and pickup via both targets, including from a second player.
   - Expect: Read markers keep their existing suppressed-bubble behavior; unread restores the bubble. Right-click can reread. Both targets obey the same existing lock and claim permissions.
   - Watch for: Plate interactions bypassing locks or claims, or read marks being lost.
5. **Editor ranges across display changes (P1)**
   - Config: EnableSceneMarkers=true. Use a marker that can be edited.
   - Do: Shift-right-click the marker, select Always nearby, edit its title and body, type `50` for icon distance and `12` for text distance, then enable both Unlimited switches. Change display to When targeted, then back to Always nearby. Turn both Unlimited switches off without saving or closing the editor.
   - Expect: The icon distance still reads `50`, the text distance still reads `12`, the title and body still contain the edits, and the selected display remains Always nearby. Save, reopen, and confirm those values persist.
   - Watch for: Either range reverting to its previous value (usually `24` or `8`), title/body edits disappearing during the display change, or the display selection changing unexpectedly. This card needs the new package staged and human observation.

Automated evidence: full The BASICs suite passed 1,072 tests with six existing skips. Build-and-package succeeded. Local package SHA-256: `98224FD47DD29989702234CC33C295677CECE2E2B182FE447BAA8FCCC18F2EAE`. The package is a local build from the PR branch, not a published 5.9.1 release. The prior server ZIP is backed up locally under `.tmp/scene-plate-qa-backup/`.

PR: https://github.com/BASIC-BIT/Vintage-Story-Mods/pull/243
Retire this packet after the PR is merged or superseded and its manual QA observations are recorded.
