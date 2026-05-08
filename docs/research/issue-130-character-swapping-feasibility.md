# Issue 130 Character Swapping Feasibility

Date: 2026-05-07

Issue: https://github.com/BASIC-BIT/Vintage-Story-Mods/issues/130

Scope: feasibility research for issue #130. This document started as a research-only spike and now records the constraints that shaped the MVP implementation in this PR.

## Recently Merged PR Context Checked

Current merged PR list checked on 2026-05-07 after the initial research pass:

| PR | Title | Issue 130 relevance |
| --- | --- | --- |
| #144 | Add admin config panel workflow | New context not covered by the first writeup. Adds shared server config, live/restart config registry, `/thebasics config` admin UI, client config refresh, nametag refresh, typing-indicator clearing, and reviewed-setting tracking. |
| #142 | Add roleplay character sheet workflow | Core input for this writeup. Character sheet, nickname binding, nametag/title/cache behavior, and nickname parser/validation changes are referenced throughout. |
| #141 | Apply multi-point LOS to chat UI overlays | Relevant to post-swap UI refresh. Swaps should refresh nametag/typing state without bypassing current LOS/target/range gating. |
| #140 | Update vulnerable NuGet package metadata | No direct character-swapping design impact. |
| #139 | Improve chat visibility and repair UX | Relevant only through language/chat visibility behavior already considered in the language and client refresh sections. |
| #133 | Prepare The BASICs 5.5.0 release | Release/QA context only; no new character-swapping design constraint beyond existing mod behavior. |

PR #144 means issue #130 should not add standalone config plumbing for character-swap settings. New scalar settings such as max character slots, inventory-swap enablement, appearance-swap enablement, nickname uniqueness policy, and deletion/archive policy should be registered through `ConfigAdminSettingRegistry` with explicit `Live` versus `RestartRequired` behavior. Character slot records themselves are player/world data, not mod config, and must not be stored in `the_basics.json`.

PR #144 also creates a shared `ModConfig` instance in `BaseBasicModSystem` and an `OnConfigReloaded()` notification path. Character-swapping services should use the shared `Config` reference, avoid stale config snapshots, and subscribe to config reload side effects only where a setting can safely apply live. If a live character-related setting affects displayed names, typing state, or sheet requirements, reuse or align with the existing PR #144 side-effect paths: refresh nametags, clear typing indicators when disabled, and broadcast fresh client config where needed.

Future character-admin commands should fit under the existing root admin command shape (`/thebasics`, `/tb`, `/basic`) introduced by PR #144 instead of creating another unrelated admin root. New network messages should continue using the existing `thebasics` channel but must avoid colliding with the admin config and character sheet message flows.

## Executive Verdict

Multiple RP characters per account is technically feasible if it is designed as an overlay on the real Vintage Story account UID.

It is not safe to model RP characters as fake players, alternate player UIDs, alternate `ServerPlayerData` rows, or alternate vanilla group/role identities. Vintage Story has one authenticated player UID, one live `ServerWorldPlayerData` row per UID, and one live `ServerPlayer` wrapper per connection. Character swapping must preserve that account identity and swap selected character-scoped state into the one active player container.

Recommended MVP is identity-first:

| Phase | Recommendation | Reason |
| --- | --- | --- |
| 1 | Character slot registry, active character ID, character sheet/nickname/color/languages/chatter/chat-mode | Mostly The BASICs-owned moddata, low engine risk. |
| 2 | Appearance/class/traits snapshot and restore | Feasible but needs careful watched-attribute and vanilla character-system sync. |
| 3 | Inventory/equipment snapshot and restore | Feasible but highest dupe/loss risk; should be a separate hardening phase. |
| 4 | Other mod state | Only through explicit opt-in extension APIs/events, never generic moddata swapping. |

## Key Engine Findings

### Account UID is the hard identity boundary

`IPlayer.PlayerUID` is documented as the unique identifier that should be used forever; `PlayerName` is mutable and not unique identity (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Common\IPlayer.cs:37-44`).

Runtime player lookup is keyed by this UID, and login binds the authenticated player into `PlayersByUid` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ConnectedClient.cs:273-326`).

`EntityPlayer.PlayerUID` is read from the entity watched attribute `playerUID`, and login finalization sets that watched value from the authenticated packet UID. Changing it would blur account identity with RP character identity.

Design implication: never change account UID, `ServerWorldPlayerData.PlayerUID`, entity watched `playerUID`, `PlayersByUid`, or database playerdata row keys to represent an RP character.

### There is one active world player container per UID

`ServerWorldPlayerData` is the world-specific player save object. It contains the active account UID, inventory serialization bytes, serialized player entity, game mode, movement flags, selected hotbar slot, spawn position, deaths, and world player `ModData` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerWorldPlayerData.cs:18-87`).

On load, Vintage Story checks `PlayerDataManager.WorldDataByUID`, otherwise deserializes the database row by real UID, initializes inventories and entity, then creates one `ServerPlayer` (`ConnectedClient.cs:273-326`).

During world save, Vintage Story iterates `PlayerDataManager.WorldDataByUID.Values`, calls `BeforeSerialization()`, and writes the result to the DB keyed by `current.PlayerUID` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerSystemLoadAndSaveGame.cs:378-388`).

Design implication: character slots need their own storage. The one active vanilla world player object should be treated as the currently loaded character body.

### Account/server data is not character data

`ServerPlayerData` is JSON-persisted world-independent account/admin data. It stores role, permanent privileges, denied privileges, group memberships, invite setting, last known player name, custom player data, land claim allowance/areas, and character selection date (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerPlayerData.cs:12-75`).

Privilege resolution combines account role, account grants/denials, and runtime privileges (`ServerPlayerData.cs:183-254`). Groups are stored on account data (`ServerPlayerData.cs:168-219`). Bans and whitelist entries are matched by UID where possible (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\PlayerDataManager.cs:482-610`).

Design implication: vanilla roles, privileges, groups, bans, whitelist, and land-claim account allowances should remain account-scoped. Per-character admin power or per-character vanilla groups are unsafe unless implemented as independent The BASICs metadata that does not grant real server permissions.

### `ServerPlayer` is a wrapper over one worlddata and one accountdata

`ServerPlayer` holds a private `ServerWorldPlayerData worlddata`, a `ServerPlayerData serverdata`, and a `ServerPlayerInventoryManager` constructed over `worlddata.inventories` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerPlayer.cs:15-27`, `ServerPlayer.cs:248-265`).

`ServerPlayer.Entity`, `WorldData`, `InventoryManager`, `Privileges`, moddata access, and spawn position are all delegated to those backing objects (`ServerPlayer.cs:41-165`, `ServerPlayer.cs:307-363`). There is no public setter for replacing the full backing `worlddata` after construction.

Design implication: runtime character swap should not try to replace the whole `ServerWorldPlayerData` object. It should snapshot active state, restore selected fields/inventories/entity attributes into the current object, and use existing sync methods like `BroadcastPlayerData(sendInventory: true)`.

## Inventory Findings

Inventory is feasible but must be treated as the riskiest component.

Vintage Story default player inventories are `hotbar`, `creative`, `backpack`, `ground`, `mouse`, `craftinggrid`, and `character` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Common\PlayerInventoryManager.cs:16`). Only inventories inheriting `InventoryBasePlayer` are serialized into `ServerWorldPlayerData` (`ServerWorldPlayerData.cs:358-395`).

`InventoryBase` exposes public `FromTreeAttributes()` and `ToTreeAttributes()` methods (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Common\InventoryBase.cs:785-795`). That provides a plausible mod-side snapshot mechanism for player inventories by casting player-owned inventories to `InventoryBase`.

High-risk inventory cases:

| Area | Evidence | Risk |
| --- | --- | --- |
| Mouse cursor | Server only secures cursor item on disconnect/shutdown by transferring or dropping it (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerSystemInventory.cs:42-69`). Mouse inventory does not persist. | Swap with cursor item can dupe or delete unless secured first. |
| Backpacks | `InventoryPlayerBackpacks` serializes only bag slots; bag contents live inside bag item attributes and are synced through `BagInventory` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Common\InventoryPlayerBackpacks.cs:19-22`, `InventoryPlayerBackpacks.cs:84-92`, `InventoryPlayerBackpacks.cs:127-138`). | Copying bag contents separately from bag stacks can dupe/desync. |
| Active hotbar | Active slot can point into backpack contents, not only hotbar slots (`PlayerInventoryManager.cs:37-54`). Hotbar/offhand stat modifiers are updated on slot modification (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Common\InventoryPlayerHotbar.cs:85-101`). | Restoring slots without recomputing active/offhand stats can corrupt combat/tool behavior. |
| Equipment | `InventoryCharacter` has 15 slots but `DiscardAll()` and `OnOwningEntityDeath()` are empty (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Common\InventoryCharacter.cs:45-66`, `InventoryCharacter.cs:134-140`). | Equipment does not follow generic death/drop assumptions. It must be explicitly snapshotted/restored. |
| Dirty slot sync | Server dirty-slot loop special-cases `InventoryCharacter` to broadcast player data and packet dirty slots (`ServerSystemInventory.cs:241-318`). | Restore must mark slots dirty and broadcast player data. |
| In-progress use | Server tick continues using the active hotbar slot while hand use is active (`ServerSystemInventory.cs:130-182`). | Swap during hand use can apply old action to new slot or vice versa. |

Second-pass inventory sync details:

| Mechanism | Evidence | Swap implication |
| --- | --- | --- |
| Open inventory discovery | `PlayerInventoryManager.OpenedInventories` returns inventories whose `HasOpened(player)` is true (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Common\PlayerInventoryManager.cs:107`). | The server can enumerate current open inventories, but this includes owned player inventories because `InventoryBasePlayer.HasOpened()` returns true for the owner. Filter to non-`InventoryBasePlayer` when checking external containers. |
| Server-side external close | Disconnect closes and removes all non-player inventories from the manager (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerPlayerInventoryManager.cs:89-123`). | There is precedent for server-side cleanup. For live swaps, prefer rejecting open external inventories first; if auto-close is added, verify client dialogs close cleanly. |
| Close sync asymmetry | `CloseInventoryAndSync()` sends a packet only when called from client-side API; on the server it just closes (`PlayerInventoryManager.cs:397-404`). | A server-only swap service cannot assume `CloseInventoryAndSync()` notifies the player's client. Client UI closure may need a The BASICs packet or a conservative veto. |
| Cursor securing | Vanilla only secures mouse-slot contents on disconnect/shutdown by transfer-away, then drop fallback (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerSystemInventory.cs:42-69`). | The safe MVP rule is no cursor stack at swap time. A later inventory phase can copy this transfer/drop behavior deliberately. |
| Bag persistence | Bag content slots save into bag item attributes via `BagInventory.SaveSlotsIntoBags()` and rebuild with `ReloadBagInventory()` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Common\BagInventory.cs:143-188`). `InventoryPlayerBackpacks` reloads bag content views when bag slots change and broadcasts player data on the server (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Common\InventoryPlayerBackpacks.cs:127-138`). | Snapshot/restore must treat backpack bag slots as authoritative and force bag content save/reload around restore. Do not serialize bag contents separately from bag stacks. |
| Stat modifiers | Hotbar slot modification recomputes main-hand/offhand stat modifiers and removes previous modifier keys (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Common\InventoryPlayerHotbar.cs:85-167`). | Bulk `FromTreeAttributes()` restore is not enough by itself; inventory phase must explicitly mark/modify active and offhand slots or otherwise force stat recomputation. |
| Packet resync | `BroadcastPlayerData(sendInventory: true)` sends all player inventories to the owner, and hotbar/character/backpack to other players (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerWorldPlayerData.cs:421-538`; `D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerMain.cs:5488-5494`). | After inventory restore, mark affected slots dirty and also broadcast player data with inventory so other players see equipment/backpack/hotbar changes. |

Recommended inventory swap preconditions:

| Precondition | Reason |
| --- | --- |
| Player online and fully playing | Offline support requires separate persistence/indexing. |
| No mouse cursor stack | Reject in MVP; secure cursor first by transfer/drop equivalent in later inventory phase. |
| No open external inventories | Reject in MVP; avoid transactions with chest/trader/block inventories and unresolved client GUI state. |
| Not currently using hand/block/entity | Call `EntityPlayer.TryStopHandAction(forceStop: true, ...)` and abort if it cannot stop (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Common\EntityPlayer.cs:1365-1382`). |
| Not mounted | `TryUnmount()` can fail if `CanUnmount()` rejects it, and mounted position/control state is stored in watched `mountedOn` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Common\EntityAgent.cs:267-303`, `EntityAgent.cs:359-395`). Abort if unmount fails. |
| Alive and not in death/respawn flow | Player death stops hand use, unmounts, writes death watched attributes, and changes inventory/death behavior (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Common\EntityPlayer.cs:1185-1195`; `D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Common\Entities\Entity.cs:2450-2499`). Swapping during this flow risks body/inventory mismatch. |
| Mark all restored player inventory slots dirty | Force server-authoritative client resync. |
| Broadcast player data with inventory | Needed for character/equipment/appearance changes. |

## Appearance, Class, Traits, and Entity State

Entity attributes are central to character-like state. `Entity.WatchedAttributes` are permanently stored and sent to clients when dirty, while `Entity.Attributes` are permanently stored but side-local (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Common\Entities\Entity.cs:172-185`). Entity serialization writes watched attributes, position, code, side-local attributes, animation data, and behavior bytes (`Entity.cs:2267-2335`, `Entity.cs:2342-2389`).

Vanilla character class is stored in `WatchedAttributes["characterClass"]` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VSSurvivalMod\Vintagestory\GameContent\CharacterSystem.cs:393-401`). Applying a class can clear character equipment slots 0-11 and apply class gear (`CharacterSystem.cs:401-458`). Trait stat modifiers are reapplied under stat key `trait` (`CharacterSystem.cs:461-555`). Character selection applies voice, skin parts, `skinConfig`, and broadcasts player data (`CharacterSystem.cs:811-856`).

The BASICs heritage language system watches `characterClass`, `extraTraits`, and player model/appearance-derived fields to grant/revoke languages (`mods-dll/thebasics/src/ModSystems/ProximityChat/HeritageLanguageSystem.cs:56-93`, `HeritageLanguageSystem.cs:108-120`).

Design implication: class/skin/traits can be character-scoped, but swapping them must coordinate with watched attributes, trait stats, The BASICs heritage language caches, and vanilla `LastCharacterSelectionDate` which is account-level (`IServerPlayerData.LastCharacterSelectionDate`, `D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryAPI\Vintagestory\API\Server\IServerPlayerData.cs:61-65`).

Second-pass appearance details:

| Area | Evidence | Swap implication |
| --- | --- | --- |
| Skin snapshot | `EntityBehaviorExtraSkinnable.AppliedSkinParts` exposes applied part/variant pairs from `skinConfig.appliedParts` (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VSSurvivalMod\Vintagestory\GameContent\EntityBehaviorExtraSkinnable.cs:36-57`). | A character record can store a simple `Dictionary<string, string>` of applied skin part codes to variant codes. |
| Skin restore | `selectSkinPart()` writes `skinConfig.appliedParts`; for voice parts it also sets watched `voicetype`/`voicepitch` and applies the voice (`EntityBehaviorExtraSkinnable.cs:378-409`). Hair styling uses this mod-side path and then marks `skinConfig` dirty (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VSSurvivalMod\Vintagestory\GameContent\ModSystemNPCHairStyling.cs:48-63`). | Restore skin parts with `selectSkinPart(..., retesselateShape: false, playVoice: false)`, then mark `skinConfig` dirty and broadcast. |
| Voice restore | `ApplyVoice()` updates behavior fields and `talkUtil`, but does not itself write watched attributes (`EntityBehaviorExtraSkinnable.cs:411-470`). | Restore voice via `selectSkinPart("voicetype", ...)` and `selectSkinPart("voicepitch", ...)`, or explicitly set watched `voicetype`/`voicepitch` before calling `ApplyVoice()`. Do not rely on `ApplyVoice()` alone for persistent/synced state. |
| Class/trait recompute | `setCharacterClass()` sets watched `characterClass` and calls private trait recomputation; with `initializeGear: true` it clears and re-adds character gear (`CharacterSystem.cs:393-459`). Trait recomputation reads current `extraTraits` and writes persistent stat modifiers under key `trait` (`CharacterSystem.cs:461-555`). | Restore `extraTraits` before calling `setCharacterClass(entity, classCode, initializeGear: false)`. This avoids starter-gear mutation while still forcing trait stat recomputation. |
| Vanilla character selection side effects | Character selection writes `createCharacter`, changes class/gear, applies voice/skin, mutates `LastCharacterSelectionDate`, removes `allowcharselonce`, marks `skinConfig` dirty, and broadcasts player data (`CharacterSystem.cs:811-856`). | Do not drive swaps through the vanilla selection packet/dialog. It has account-level side effects and starter-gear semantics that do not match RP slot switching. |

Appearance-phase restore order should be:

1. Validate target class and skin variants exist.
2. Write `extraTraits` watched attribute for the target character.
3. Call `CharacterSystem.setCharacterClass(entity, classCode, initializeGear: false)` to recalculate trait stats without starter gear.
4. Restore each applied skin part with `EntityBehaviorExtraSkinnable.selectSkinPart(..., retesselateShape: false, playVoice: false)`.
5. Restore voice through voice skin parts or watched `voicetype`/`voicepitch` plus `ApplyVoice()`.
6. Mark `characterClass`, `extraTraits`, and `skinConfig` dirty, then `BroadcastPlayerData(sendInventory: true)`.
7. Trigger The BASICs heritage-language refresh for the new class/model/traits state.

MVP recommendation: do not use vanilla `.charsel` as the primary character-switching mechanism. Snapshot and restore selected watched attributes instead, then mark paths dirty and broadcast. If class changes are allowed, avoid `setCharacterClass(..., initializeGear: true)` for established characters unless intentionally granting starter gear.

## The BASICs State Map

The BASICs stores most RP state on `IServerPlayer` world moddata through `IServerPlayerExtensions` (`mods-dll/thebasics/src/Extensions/IServerPlayerExtensions.cs:22-72`). This is good for MVP because it is isolated from vanilla account/admin identity.

| Key/state | Current storage | Recommended scope | Notes |
| --- | --- | --- | --- |
| `BASIC_CHARACTER_SHEET` | `CharacterSheetData` list of `FieldId`/`Value` (`mods-dll/thebasics/src/ModSystems/CharacterSheets/Models/CharacterSheetData.cs:6-21`) | Character | Primary identity/bio storage. |
| `thebasics.fullName` binding | Sheet-bound field read via config (`mods-dll/thebasics/src/Extensions/IServerPlayerExtensions.cs:127-130`) | Character | Already schema-driven. |
| `thebasics.nickname` binding | Sheet-bound nickname with legacy fallback (`IServerPlayerExtensions.cs:80-115`) | Character | Recently moved into sheet-backed pattern. |
| `BASIC_NICKNAME_COLOR` | Separate moddata (`IServerPlayerExtensions.cs:222-240`) | Character likely | If color is persona styling, move with slot. |
| `BASIC_LANGUAGES` | Known language names (`IServerPlayerExtensions.cs:41`, `IServerPlayerExtensions.cs:309-357`) | Character | A character knows languages, not the account. |
| `BASIC_DEFAULT_LANGUAGE` | Current/default speaking language (`IServerPlayerExtensions.cs:42`, `IServerPlayerExtensions.cs:360-392`) | Character or session | Recommended character, with fallback if missing. |
| Heritage language last-state keys | `BASIC_LAST_MODEL_LANGUAGE`, `BASIC_LAST_MODEL_GROUP_LANGUAGE`, `BASIC_LAST_CLASS_LANGUAGE`, `BASIC_LAST_TRAITS_LANGUAGE` (`HeritageLanguageSystem.cs:27-30`) | Character | Must follow class/model/trait snapshots. |
| `BASIC_CHATMODE` | Current proximity chat mode (`IServerPlayerExtensions.cs:244-252`) | Character or session | Character is reasonable if speech style differs. |
| `BASIC_EMOTEMODE` | Emote-mode toggle (`IServerPlayerExtensions.cs:254-262`) | Character or account preference | Product decision. |
| `BASIC_RPTEXTENABLED` | RP text display toggle (`IServerPlayerExtensions.cs:264-272`) | Account preference likely | More UI preference than persona. |
| `BASIC_OOCENABLED` | OOC toggle (`IServerPlayerExtensions.cs:498-505`) | Account/session | OOC is player preference. |
| `BASIC_CHATTER_ENABLED` | Chatter opt-out (`IServerPlayerExtensions.cs:508-515`) | Account or character | If voice/personality, character. If accessibility, account. |
| `BASIC_LAST_SELECTED_GROUP_ID` | UI chat channel state (`IServerPlayerExtensions.cs:210-219`) | Account/session | Not character identity. |
| `BASIC_COUNT_*` | Player stat counters (`IServerPlayerExtensions.cs:34`, `mods-dll/thebasics/src/ModSystems/PlayerStats/PlayerStatSystem.cs:55-280`) | Ambiguous | RP history vs account analytics; needs config. |
| `BASIC_TPA_TIME` | TPA cooldown (`IServerPlayerExtensions.cs:399-417`) | Account | Anti-abuse cooldown should not reset by swapping. |
| `BASIC_TPA_ALLOWED` | TPA opt-in/out (`IServerPlayerExtensions.cs:419-427`) | Account preference | Could be character, but safer account. |
| `BASIC_OUTGOING_TPA_REQUEST` | Active request JSON (`IServerPlayerExtensions.cs:429-455`) | Session | Clear/revalidate on swap. |

### MVP risk-reduction: use active projection, not broad refactors

The lowest-risk MVP does not need every existing sheet/nickname/language/chat call site to become character-slot aware immediately.

Use the current The BASICs moddata keys as the **active character projection**:

| Projection key | Meaning in MVP |
| --- | --- |
| `BASIC_CHARACTER_SHEET` | Sheet for the currently active character. Existing `CharacterSheetSystem` keeps working. |
| `BASIC_NICKNAME_COLOR` | Nickname color for the currently active character. Existing `NameTransformer` keeps working. |
| `BASIC_LANGUAGES` | Languages for the currently active character. Existing language commands keep working. |
| `BASIC_DEFAULT_LANGUAGE` | Default language for the currently active character. Existing chat transformers keep working. |
| `BASIC_CHATMODE` | Optional character-scoped current speech mode. Existing `/say`/`/yell`/`/whisper` state keeps working. |
| Heritage last-state keys | Active character's last reconciled class/model/trait language state. |

Character records become durable snapshots of those projection keys. On switch, the service captures the current projection into the old active record, restores the target record into the projection keys, and then runs the existing refresh paths.

This avoids risky first-pass changes to:

| Existing code | Why projection reduces risk |
| --- | --- |
| `IServerPlayerExtensions.GetNickname(config)` / `SetNickname(config)` (`mods-dll/thebasics/src/Extensions/IServerPlayerExtensions.cs:80-115`) | These already read/write the current sheet-bound nickname. Projection makes that sheet be the active character sheet. |
| `CharacterSheetSystem.GetSheetData()` / `SaveSheetData()` (`mods-dll/thebasics/src/ModSystems/CharacterSheets/CharacterSheetSystem.cs:812-822`) | These can keep using `BASIC_CHARACTER_SHEET`; switching changes which character is projected there. |
| `CharacterSheetSystem.BuildNametagDisplayName()` (`CharacterSheetSystem.cs:968-1000`) | Existing nametag formatting keeps reading active character name/nickname. |
| `LanguageSystem` commands and transformers (`mods-dll/thebasics/src/Extensions/IServerPlayerExtensions.cs:308-393`) | Existing language add/remove/default operations continue to affect the active character projection. |
| `NameTransformer` (`mods-dll/thebasics/src/ModSystems/ProximityChat/Transformers/NameTransformer.cs:40-63`) | Chat display automatically uses the projected nickname/full name/color. |

Projection invariants:

1. If `BASIC_ACTIVE_CHARACTER_ID` is set, projection keys are the source of truth for that active character until captured.
2. Capture projection into the active record on character switch, player disconnect, and `GameWorldSave`.
3. On server start/player join, if an active character ID exists, reconcile by capturing the current projection into that active record before allowing a switch.
4. Restore target record into projection keys in one server-main-thread operation.
5. Do not write inactive character data into the projection keys except during an intentional switch.

This leaves a small crash window no worse than current in-memory moddata changes: edits after the last world save can still be lost. Registering a `GameWorldSave` capture hook closes the normal autosave/restart path.

### Client and UI invalidation risks

Swapping the active projection server-side is not enough; client caches need refresh.

| Client/server cache | Evidence | Swap requirement |
| --- | --- | --- |
| Nametag text | Both `RPProximityChatSystem.SwapOutNameTag()` and `CharacterSheetSystem.RefreshEntityNameTag()` set `EntityBehaviorNameTag` from `BuildNametagDisplayName()` (`mods-dll/thebasics/src/ModSystems/ProximityChat/RPProximityChatSystem.cs:772-786`; `CharacterSheetSystem.cs:945-966`). | Add a shared identity-refresh method or event so swaps refresh nametag once through one path. |
| Own character sheet cache | Client caches `_lastOwnCharacterSheetView` and opens that cached view before requesting a fresh one from the character dialog (`mods-dll/thebasics/src/ModSystems/ChatUiSystem/ChatUiSystem.cs:149-170`, `ChatUiSystem.cs:346-354`). | After a successful swap, send a fresh own `CharacterSheetViewMessage` or a character-changed packet so the cache/title are updated immediately. |
| Character dialog title override | Client title override is set from `CharacterSheetViewMessage.DisplayName` (`ChatUiSystem.cs:356-364`) and patched into the vanilla character dialog title (`ChatUiSystem.cs:377-394`). | Swap must update `_characterDialogTitleOverride`; easiest path is sending the fresh own sheet view. |
| Open character sheet dialog | Existing `CharacterSheetDialog.SetView()` recomposes the open dialog (`mods-dll/thebasics/src/ModSystems/ChatUiSystem/CharacterSheetDialog.cs:45-49`). | If the sheet is open during swap, push the new view so the dialog changes character instead of leaving stale/editable fields. |
| Typing indicator | Server and client typing state are keyed by entity ID, not character ID (`RPProximityChatSystem.cs:29-30`, `ChatUiSystem.cs:52-55`, `ChatUiSystem.cs:421-458`). | Clear typing state on swap and broadcast `ChatTypingStateMessage` with `None`, otherwise the new character can inherit the old character's active typing indicator. |

MVP should introduce one small server-side post-swap refresh routine:

1. Clear typing indicator for the player entity.
2. Refresh server nametag display.
3. Send a fresh own character-sheet view packet to the swapping client if character sheets are enabled.
4. Broadcast player data if appearance/inventory were touched; identity-only can usually avoid a full inventory broadcast.

### Nickname and character-name index

Existing nickname validation still checks active online nicknames plus all offline account usernames; it does not check offline nicknames despite the older design notes in `NicknameValidationUtils` (`mods-dll/thebasics/src/Utilities/NicknameValidationUtils.cs:14-81`, `NicknameValidationUtils.cs:94-139`). Multi-character slots make that gap bigger because inactive character names are not present on `AllOnlinePlayers`.

Add a dedicated character identity index rather than extending ad-hoc online scans.

| Index entry field | Purpose |
| --- | --- |
| Normalized name | Case-insensitive conflict key for nickname/full-name uniqueness. |
| Name kind | Track whether the key came from nickname, full name, or optional aliases. |
| Owner UID | Real account UID. |
| Character ID | The The BASICs character slot ID. |
| Is active | Distinguish active online projection from inactive/offline slots for parser behavior. |

Recommended behavior:

1. Build the index at startup from loaded `WorldDataByUID`, existing active projection keys, and stored character records.
2. Validate new nicknames against all character slots on the server, not only online active characters.
3. Keep `PlayerByNameOrNicknameArgParser` active-online only for commands that target a live player (`mods-dll/thebasics/src/Utilities/Parsers/PlayerByNameOrNicknameArgParser.cs:110-163`). Inactive character names should not resolve to a live account unless product requirements say otherwise.
4. Update the index transactionally during character create/rename/delete/archive, sheet nickname/full-name edits, and swaps.
5. On migration, log existing conflicts and mark conflicting inactive slots as needing admin resolution rather than silently deleting names.

## Character Slot Storage Model

Use real account UID as the owner and introduce a The BASICs-controlled character ID.

Suggested account-level moddata keys:

| Key | Purpose |
| --- | --- |
| `BASIC_CHARACTER_SLOTS` | List/dictionary of character metadata and snapshots owned by the account. |
| `BASIC_ACTIVE_CHARACTER_ID` | Active slot ID for the current account/world. |
| `BASIC_CHARACTER_VERSION` | Migration/version guard for serialized character records. |

Suggested per-character record:

```csharp
public sealed class RpCharacterRecord
{
    public string CharacterId { get; set; }
    public string DisplayName { get; set; }
    public CharacterSheetData Sheet { get; set; }
    public string NicknameColor { get; set; }
    public List<string> Languages { get; set; }
    public string DefaultLanguage { get; set; }
    public Dictionary<string, byte[]> TheBasicsModData { get; set; }
    public string CharacterClassCode { get; set; }
    public string[] ExtraTraits { get; set; }
    public Dictionary<string, string> SkinParts { get; set; }
    public string VoiceType { get; set; }
    public string VoicePitch { get; set; }
    public CharacterAppearanceSnapshot Appearance { get; set; }
    public CharacterInventorySnapshot Inventory { get; set; }
    public CharacterPositionSnapshot Position { get; set; }
}
```

Recommended storage location: initially `IServerPlayer.SetModdata` on the real player. It is world-specific, already backed by `ServerWorldPlayerData.ModData`, and aligns with RP characters being per-world. If records become large due to inventory snapshots, move to a dedicated JSON/protobuf file keyed by real UID and keep only active ID/index metadata in moddata.

## Swap Transaction Shape

The swap should be transactional and server-authoritative.

1. Validate request: player online, allowed by privilege/config, target character exists and belongs to UID.
2. Acquire per-player swap lock to prevent concurrent saves, chat edits, TPA actions, and inventory mutation races.
3. Reject or normalize unsafe state: cursor item, open external inventory, active hand use, mounted state, death/respawn, combat cooldown if implemented.
4. For appearance/inventory phases, stop hand use and unmount only if both operations succeed; otherwise abort before mutation.
5. Snapshot current active character state into its record.
6. Clear/revalidate ephemeral state: typing indicator, outgoing TPA, current using selections, client character-sheet cache, old nametag.
7. Restore target character state into The BASICs moddata and selected entity/inventory fields.
8. Mark affected watched attributes and inventory slots dirty.
9. Broadcast player data with inventory and refresh nametag/chat UI config.
10. Persist active character ID and audit-log the swap.
11. If any restore step fails, roll back to the pre-swap snapshot or abort before mutation.

Do not support offline swapping in MVP. Offline swapping requires editing serialized records without a live `IServerPlayer`, and error recovery gets much harder.

### Save and concurrency risk reduction

Vintage Story autosave pauses/suspends the server, triggers `GameWorldSave`, and then vanilla systems serialize player world data (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerSystemAutoSaveGame.cs:45-88`; `D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\ServerSystemLoadAndSaveGame.cs:227-269`, `ServerSystemLoadAndSaveGame.cs:371-388`). Mod event handlers run before core save handlers because `CoreServerEventManager.TriggerGameWorldBeingSaved()` triggers the mod event manager first, then the base server event manager (`D:\bench\vs\source\vintagestory\1.22.1\decompiled\VintagestoryLib\Vintagestory\Server\CoreServerEventManager.cs:196-200`).

Implementation implications:

| Risk | Reduced-risk rule |
| --- | --- |
| Active projection not captured before save | Register `API.Event.GameWorldSave` and capture every online player's active projection into its active character record before vanilla `ServerWorldPlayerData.BeforeSerialization()`. |
| Reentrant swaps | Use a per-player in-memory `swapInProgress` flag even though command/network handlers are server-main-thread. It prevents nested swap attempts from hooks or future async UI. |
| Async partial state | Keep MVP swaps synchronous on the server main thread. Do not `await`, defer ticks, or do file IO midway through mutation. |
| Save during mutation | Because the main mutation is synchronous and `GameWorldSave` is main-thread triggered, a save cannot interleave inside the mutation unless implementation introduces async work. Keep it synchronous. |
| Crash after restore but before record capture | Capture restored target record/projection immediately after successful restore, and also capture on `GameWorldSave` and disconnect. |

The swap lock should protect commands, network messages, character-sheet saves, nickname/color commands, language commands, and admin character operations for the same player while a swap is in progress.

## MVP Boundaries

### MVP 1: Identity-only characters

Deliverables:

| Feature | Include? |
| --- | --- |
| Character slot create/rename/delete/select | Yes |
| Active character ID | Yes |
| Character sheet per slot | Yes |
| Nickname/full name per slot | Yes |
| Nickname color per slot | Yes |
| Languages/default language per slot | Yes |
| Nametag/chat refresh on swap | Yes |
| Admin view/force switch/lock/delete tools | Minimal |
| Inventory/equipment swapping | No |
| Position swapping | No |
| Vanilla class/skin swapping | No |
| Other mod data swapping | No |

Rationale: this builds the user-facing RP value on top of The BASICs-owned data and avoids the engine's highest-risk areas.

### MVP 2: Appearance/class experiment

Add skin/class/trait snapshot only after MVP 1 is stable. Treat `characterClass`, `extraTraits`, `skinConfig.appliedParts`, watched `voicetype`/`voicepitch`, behavior voice state, and heritage language last-state keys as one unit. Validate that mark-dirty and broadcast paths fully refresh all clients.

### MVP 3: Inventory beta behind config

Only after a dedicated inventory test suite/manual QA plan exists. Gate behind a config flag, likely disabled by default.

Inventory beta must explicitly test:

| Test area | Failure mode |
| --- | --- |
| Cursor item | Dupe/loss when swapping mid-drag. |
| Bag contents | Desync between bag item attributes and visible bag slots. |
| Active hotbar into backpack slot | Wrong active item/stat modifier. |
| Offhand/mainhand modifiers | Persistent stat modifier leak. |
| Equipment armor/clothes | Not dropped/serialized as expected. |
| Open containers/traders | Item movement race. |
| Death/respawn | Swap body versus dropped inventory mismatch. |
| Disconnect during swap | Partial snapshot/restore. |
| Server save during swap | Old/new records both writing safely. |

## Extension Hooks For Other Mods

Do not generically snapshot all moddata. Unknown moddata may contain account identity, cooldowns, permissions, caches, or serialized runtime handles.

Offer explicit integration points instead:

```csharp
public interface ICharacterSwapParticipant
{
    string Id { get; }
    void Capture(IServerPlayer player, CharacterSwapCaptureContext context);
    void Restore(IServerPlayer player, CharacterSwapRestoreContext context);
    bool CanSwap(IServerPlayer player, CharacterSwapValidationContext context, out string reason);
}
```

Events/hooks to expose:

| Hook | Purpose |
| --- | --- |
| `BeforeCharacterSwapValidation` | Allow systems to veto swaps. |
| `CaptureCharacterState` | Let opt-in systems write their own serializable data. |
| `RestoreCharacterState` | Let opt-in systems restore state. |
| `AfterCharacterSwap` | Refresh UI/caches/nametags. |
| `CharacterDeleted` | Cleanup external indexes. |

Strict rule: an extension hook can only mutate its own namespaced data unless it is a The BASICs internal participant.

## Permissions and Moderation

Keep moderation account-first.

| Concern | Recommendation |
| --- | --- |
| Bans/whitelist | Account-level only. |
| Roles/privileges | Account-level only. |
| Vanilla groups | Account-level only. |
| Claims | Account-level only. |
| Server logs | Log both account name/UID and active character name/ID. |
| Admin audit | Every create/delete/select/rename/force-swap should audit account UID and character ID. |
| Character locks | Add The BASICs-level lock/disable flag for abusive characters without changing account role. |

This prevents character swaps from becoming moderation evasion.

## Product Decisions Needed

| Decision | Options | Recommendation |
| --- | --- | --- |
| Nickname uniqueness | Active-only, all slots on server, or per-account only | All slots on server for RP clarity, with admin override. |
| Stats scope | Account, character, or both | Configurable; default existing account behavior until character stats UI exists. |
| Position scope | Shared account body or per-character body | Shared for MVP. Per-character position is much riskier and should be later. |
| Inventory scope | Shared account inventory or per-character inventory | Shared for MVP. Per-character inventory later behind config. |
| Character deletion | Hard delete or archive | Archive/disable first; hard delete admin-only. |
| Offline management | Allowed or online-only | Online-only for MVP. |
| Max characters | Unlimited or configured cap | Configured cap. |

## Hard Blockers / Red Flags

These should block implementation until deliberately solved:

| Blocker | Why |
| --- | --- |
| Fake player UID approach | Breaks auth, bans, whitelist, groups, claims, inventory ownership, DB rows. |
| Generic moddata swap | Can corrupt account permissions/cooldowns/third-party state. |
| Inventory swap without cursor/open-inventory handling | High dupe/loss risk. |
| Class swap with gear initialization | Can delete/replace equipment unexpectedly. |
| Offline inventory editing | Hard to recover from corruption without live sync/validation. |
| Per-character vanilla groups/roles | Access control leak and moderation confusion. |

## Recommended Implementation Sequence

1. Add internal data models for account-owned character slots and active character ID.
2. Treat existing sheet/nickname/color/languages/default language keys as the active character projection.
3. Add migration from existing single sheet/nickname/languages into a default character slot, preserving projection keys.
4. Add server-only character service for capture/restore/select with a per-player in-memory swap flag.
5. Add `GameWorldSave`, disconnect, and join reconciliation hooks to capture the active projection into the active record.
6. Add the character identity index before exposing multiple slots, so nickname conflicts include inactive/offline slots from the start.
7. Add commands/admin commands for create/list/select/rename/archive.
8. Add post-swap refresh: clear typing, refresh nametag, send fresh own sheet view, audit-log.
9. Add tests for migration, active projection capture/restore, active slot switching, nickname uniqueness, required sheet gating, and chat/name fallback.
10. Manual QA MVP 1 with two clients: switch identity, verify chat/nametag/sheet/languages, reconnect, server restart.
11. Only then research/implement appearance/class participant.
12. Only then research/implement inventory participant behind a config flag.

## Open Questions For Follow-up Spelunking

| Question | Why it matters |
| --- | --- |
| Does server-side external inventory auto-close need a custom The BASICs client packet/dialog close path? | `CloseInventoryAndSync()` does not send a close packet when called server-side, so auto-close may leave stale client GUI state. |
| Which watched attributes beyond `characterClass`, `extraTraits`, `skinConfig`, `voicetype`, and `voicepitch` must be included? | Partial appearance snapshots may leave stale client state. |
| Can `ServerPlayerInventoryManager` be driven to recompute active/offhand stat modifiers after bulk restore? | Needed to avoid stat leaks. |
| Can inventory restore safely force `BagInventory.SaveSlotsIntoBags()`/`ReloadBagInventory()` without reflection for every backpack implementation? | Needed to avoid bag-content dupes/desyncs. |
| Should per-character inventory include position/spawn/death count? | Design decision changes risk profile significantly. |
| How should nickname uniqueness include inactive/offline character slots? | Needs an index, especially for offline players. |

## Bottom Line

The feature is worth pursuing, but only if scoped deliberately.

Identity-only multi-character support is a reasonable next feature after character sheets because it mostly moves The BASICs-owned RP state behind an active-character abstraction.

Full body swapping with appearance, inventory, position, and third-party mod state is a larger engine-integration project. It should be split into separate phases with explicit invariants, test-server QA cards, and rollback behavior before touching inventory.
