# General proximity chat cleanup QA

Objective: remove the configured Proximity group and saved memberships when
`UseGeneralChannelAsProximityChat=true`, retain unrelated groups, and explain
the four chat settings in the configuration UI and feature guide.

Baseline: main `cf8f1d5`, The BASICs 5.9.1. Disposable server: `8982de16`.
Package SHA256: `7EE843AD5A20BA7E808B268DB7C4315970472D48CB57E5A0B6052411E5C968A8`.
Build/package and 37 focused migration/config-registry tests passed.
Server download and Profile2/Profile3 packages match this hash.
Final restart reached RunGame. The startup log confirms General proximity mode;
saved playergroups.json has no Proximity group and playerdata.json has zero
memberships for its old UID (14), down from two before staging. The unrelated
flywheelpower duplicate warning and `/home/container` error still appear; do not
describe the complete server boot as warning-free.
Original config and client packages are backed up under the primary checkout's
`.tmp/general-proximity-qa/`. The file named `original-server-thebasics_5_9_1.zip`
contains the initial QA build, not a verified backup of the preceding server build.
Do not use it to claim rollback to the preceding server build.

## Batch 1: General mode

Only `UseGeneralChannelAsProximityChat` changes to true. Existing normal speech
range is 35 blocks. Global OOC stays enabled.

1. **Tab migration** (P1)
   - Config: General mode on.
   - Do: Connect both profiles, open chat, and inspect the available tabs.
   - Expect: General exists and Proximity is absent on both profiles. Any unrelated
     player-created group remains available to its members.
   - Watch for: Proximity lingering on one client, missing General, or unrelated
     group membership being removed.
2. **General uses proximity range** (P0)
   - Config: General mode on, normal speech range 35 blocks.
   - Do: Use normal speech mode and a shared language. Stand within 5 blocks and
     type `qa-near` in General. Move more than 40 blocks apart and type `qa-far`
     in General.
   - Expect: The other player receives `qa-near`, but does not receive `qa-far`.
   - Watch for: Far delivery, absent near delivery, or accidentally testing only a
     command rather than typing into General.
3. **Help and global OOC** (P1)
   - Config: General mode on, global OOC enabled.
   - Do: While more than 40 blocks apart, send `((qa-global))` in General. Open
     `/basic config` and read the four changed setting tooltips.
   - Expect: The other player receives global OOC. Tooltips explain tab replacement,
     default selection, automatic tab switching, and global OOC separately.
   - Watch for: Global OOC lost, unreadable tooltips, or text suggesting the default
     selection/switching settings hide tabs.

## Batch 2: Restore original separate-channel mode

Restore the original config, restart, and relaunch both clients after Batch 1.

4. **Separate channel returns** (P1)
   - Config: Original config (`UseGeneralChannelAsProximityChat=false`).
   - Do: Connect both profiles and inspect chat tabs. Type a normal proximity
     message while nearby.
   - Expect: Proximity returns on both profiles and nearby proximity chat works.
     Unrelated groups remain available.
   - Watch for: Missing Proximity membership or failure to recreate the group.

Owner reported: "all 3 QA passed, everything worked perfectly" for Batch 1.
Tab removal and global OOC are recorded as passed. Specific near/far delivery
confirmation is requested; the tooltip review was not explicitly reported.
Do not mark manual QA complete or claim release readiness until remaining
observations are reported and the owner approves completion.
Owner subsequently confirmed the separate Proximity tab returned and nearby chat
worked, but reported its position at the end of the tab strip. Batch 2's functional
check passed; tab placement is a new research/design request. Research client-side
pinning before considering membership changes to reorder tabs.
Retire this packet after QA results and any follow-up fix are recorded in the PR.

## Follow-up: tab position and General focus

The isolated worktree now implements `ProximityChatTabPosition` with `GameOrder`
(legacy default), `First`, and `AfterGeneral`. The server setting applies live;
it does not change memberships. General mode ignores old saved tab choices and
opens General. Build/package and 75 focused migration, tab-layout, configuration,
and wire-format tests passed. Follow-up package SHA256:
`8D936D61F4DC94F719CF086CFBCDBD8A639C3D071B8F5EDBDB73B1448EF42A0E`.
On October 2, the owner requested QA. The other VS chat was inactive and preflight
confirmed the server and both profiles still had the preceding QA build. Current
config and packages were backed up under `.tmp/general-proximity-qa/before-pinning*`.
The follow-up package was uploaded and all three hashes verified; server restart
reached running in separate-channel mode. Both test clients were relaunched.
Claims and XLib are present on the test server. The existing flywheelpower duplicate
warning and `/home/container` startup error remain. No human observation is recorded yet.
Coordinate shared server/client use with the owner because another VS agent is
working on a native configuration wizard in a separate worktree.

5. **Position and routing** (P0)
   - Config: Separate-channel mode. In `/basic config`, open Chat tabs.
   - Do: Save AfterGeneral, First, then GameOrder. Select each conversation tab
     and send a distinct line. Reconnect with AfterGeneral saved.
   - Expect: Proximity moves after General, before General, then to its original
     position. All other tabs retain their order. Messages reach the selected
     group. AfterGeneral persists on reconnect.
   - Watch for: Wrong recipients, labels no longer matching messages, duplicate
     tabs, logs or other mod tabs moving, or positions resetting after reconnect.
6. **Live layout retains state** (P1)
   - Config: Separate-channel mode, both clients connected.
   - Do: Leave an unread line in a different group. Type an unsent draft with
     caret in its middle. Have the other client change tab position and save.
     Cycle tabs using mouse and keyboard, including in a narrow game window.
   - Expect: Selected group, draft, caret, and unread marker remain associated
     with the same groups. Every tab can still be reached and selected.
   - Watch for: Draft loss, unexpected channel switches, unread marker moving to
     a different group, or inaccessible tabs after scrolling.
7. **General focus ignores old preference** (P1)
   - Config: First save a Proximity selection with Preserve default chat choice
     on. Enable General-as-proximity, save, restart, and reconnect both clients.
   - Do: Open chat, manually select a log tab, close chat, and reopen it. Check
     Proximity chat as default both on and off across reconnects.
   - Expect: Opening chat selects General in each case, and no Proximity tab
     remains. Pinning has no effect in this mode. Repeat the near/far test.
   - Watch for: Old Proximity selection, an empty selected channel, or General
     messages reaching players outside normal speech range with RP text enabled.

Claim/xlib compatibility requires those mods to be present for Card 5. Local
array tests preserve arbitrary extra group IDs but do not prove live mod patch
compatibility. No follow-up card is marked passed yet.
