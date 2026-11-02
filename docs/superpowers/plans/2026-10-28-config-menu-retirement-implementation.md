# Configuration menu retirement implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Execution and delegation must follow the owner's current authorization. Owner approved implementation with per-task subagents on October 28. Profile2 remains unchanged; human QA gates still apply.

**Goal:** Replace every old-menu setting and action in the sidebar editor, switch normal configuration commands after acceptance, and retire the legacy windows without losing configuration access or draft safety.

**Architecture:** Extend the existing catalog, registered controls and shared draft/save flow. Keep the registry as the authority for types, validation and reload classification. Search navigates the same editor rather than creating a second draft.

**Tech stack:** C#, .NET 10, Vintage Story native GUI, protobuf-net, existing xUnit and standalone GuiPreview tooling.

**Spec:** [Retirement design and review brief](2026-10-28-config-menu-retirement-design.md), with the [75-key inventory](2026-10-28-config-menu-inventory.md).

## Global constraints

- Preserve the eight-category sidebar. Add no further navigation tier.
- Languages remain one scrolling form with fixed selection and chat previews.
- Keep one shared scalar/language/template draft, Review and Save flow.
- Booleans use the current toggles; colors use the current picker; privilege controls accept arbitrary text.
- No config property renames, default changes, silent value normalization or loss of stored disabled values.
- Existing ProtoMember numbers remain unchanged. Inspect current maxima before adding any field and allocate sequentially.
- GUI snapshots prove rendering only. Audibility, real integration output, permission enforcement, persistence and restart activation need their own evidence.
- No Profile2 refresh before the owner has seen standalone previews and requests continuing in-game.
- No public GitHub actions, merge, release or production deployment is authorized by this plan.

## Review focus

- Existing shared settings occur on multiple pages. Search needs one canonical destination; coverage must count distinct keys without forbidding legitimate repeated controls.
- Configurations may contain custom privileges, empty closing delimiters, large lists, invalid drafts and unusual existing values. Opening a page must not rewrite them.
- A dependency can be off or an optional mod absent. Display that state without erasing settings or claiming a preview played real audio.
- Search can refer to renamed, reordered or newly added languages/fields. It must select the correct draft entry and never index real player answers.
- An older client or failed GUI initialization may reach the legacy packet or fallback path. Entry-point changes must not create recovery loops or silently discard active drafts.

## Source ownership

| Area | Files |
|---|---|
| Registered settings and allowed setup keys | `mods-dll/thebasics/src/ModSystems/AdminConfig/ConfigAdminSettingRegistry.cs`, `SetupWizardCatalog.cs`, `SetupWizardState.cs` |
| New pages and shared controls | `mods-dll/thebasics/src/ModSystems/ChatUiSystem/SetupWizardDialog.cs` and current viewport, toggle, color and privilege controls |
| Structured language/sheet drafts | `SetupWizardDraft.cs`, `SetupWizardLanguageEditor.cs`, `SetupWizardSheetEditor.cs` in ChatUiSystem |
| Search | `mods-dll/thebasics/src/ModSystems/AdminConfig/ConfigAdminSearch.cs`; `mods-dll/thebasics/src/ModSystems/ChatUiSystem/ConfigSearchDialog.cs` and a new `SetupWizardSearchPanel.cs` |
| Client lifecycle, legacy actions and fallback | `mods-dll/thebasics/src/ModSystems/ChatUiSystem/ChatUiSystem.cs`, `SetupWizardClientController.cs` |
| Commands and authoritative save | `mods-dll/thebasics/src/ModSystems/ProximityChat/RPProximityChatSystem.cs`, `RPProximityChatSystem.SetupWizard.cs` |
| Additive wire changes if needed | `mods-dll/thebasics/src/Models/TheBasicsSetupWizardRequestMessage.cs`, `TheBasicsSetupWizardResultMessage.cs` |
| Standalone scenes | `SetupWizardCaptureScenes.cs`, `tools/GuiPreview/PreviewScene.cs` and relevant existing GUI tests |

Paths abbreviated in this table are relative to the same directory as the preceding full path.

## Task 1: Pin the migration boundary

**Deliverable:** A coverage check makes missing destinations visible before routing changes begin.

**Files:** Modify `mods-dll/thebasics.Tests/ModSystems/AdminConfig/SetupWizardStateTests.cs`; add `SetupWizardCoverageTests.cs` in that directory. Keep the approved inventory beside this plan.

**Interfaces:** Consume `ConfigAdminSettingRegistry.Settings` and `SetupWizardCatalog.Pages`. Produce tests comparing registered keys with distinct covered keys plus an explicit pending-migration key set.

- [x] Recompute the current comparison. Confirm 210 registered, 135 covered, 75 pending, or explain any source drift before editing.
- [x] Write failing tests for a new unassigned registry key, a destination referencing an unknown key, and a pending key accidentally claimed as complete.
- [x] Add the explicit 75-key pending set from the inventory. Do not expand the server allowlist to all 210 before working pages exist.
- [x] Assert languages and sheet fields retain separate structured coverage and are not counted as scalar settings.
- [x] Run the coverage tests and existing catalog/protocol tests. Commit this independently.

## Task 2: Complete the smaller settings gaps

**Deliverable:** Existing pages gain their missing controls; the smaller new pages work end to end.

**Files:** Modify `SetupWizardCatalog.cs`, `SetupWizardDialog.cs`, `SetupWizardCaptureScenes.cs` and the current travel, records, speech and chat-preview tests. Add `SetupWizardRemainingSettingsTests.cs` in the ChatUiSystem test directory.

**Interfaces:** Reuse existing page setting-key lists, `SetupWizardDraft.Get/Set` and registry definitions. No alternate persistence path.

The inventory destinations in this task are `chat.tabs`, `chat.language`, `teleport.homes`, `teleport.spawn`, `notifications.deaths`, `roleplay.descriptions`, `server.retention`, `server.stats`, `server.integrations` and `server.diagnostics`: **16 keys**.

- [x] Write failing tests showing each listed key is editable, appears in Review, retains disabled values and reaches the normal save request.
- [x] Add controls and plain labels in their assigned pages. Explain milliseconds accurately for history flushing and movement tracking.
- [x] Reuse chat and placement previews where informative. No decorative pictures for diagnostics or timing controls.
- [x] Verify how changed set-home/set-spawn permissions reach registered commands. Correct metadata or runtime application if the existing Live classification is inaccurate; do not guess.
- [x] Test dependency-off cases, custom privilege strings, integration absence, non-root rejection and the three restart-classified settings.
- [x] Capture standalone compact states and review their copy and spacing. Remove only completed keys from the pending set. Commit.

## Task 3: Build speech wording and formatting shortcuts

**Deliverable:** Complete, scrollable text editors for `chat.wording` and `chat.shortcuts`: **31 keys**.

**Files:** Modify the catalog and dialog; create `SetupWizardChatTextEditor.cs` in ChatUiSystem if these specialized controls would otherwise inflate `SetupWizardDialog.cs`. Extend chat-preview and validation tests and capture scenes.

**Interfaces:** Consume exact registry keys and current draft values. Preview text comes from a cloned draft configuration and production formatting where practical; it must not emit live chat.

- [x] Write tests for the three speech modes, all nine delimiter pairs, custom prose token, attribution and RP-text privilege.
- [x] Implement mode rows labeled Whisper, Talk and Yell. Keep stored `Normal` keys intact.
- [x] Implement start/end pairs with an input-to-chat example. Use existing validators and accepted empty-end semantics; do not invent stricter delimiter rules as a UI migration.
- [x] Test literal markup, empty permitted values, long configured strings, duplicate-looking names, restored originals and invalid draft retention.
- [x] Test scrolling, keyboard focus, fixed previews and error layout at both compact scales.
- [x] Check real formatting output against the preview fixtures. Burn down the matching pending keys and commit.

## Task 4: Add text appearance and sight/sound obstruction controls

**Deliverable:** `chat.textappearance` and `chat.occlusion`: **15 keys**.

**Files:** Modify catalog/dialog/capture scenes and current speech-preview helpers. Extend `SetupWizardSpeechPreviewTests.cs`, `SetupWizardChatPreviewTests.cs` and registry validation tests.

**Interfaces:** Reuse per-mode registry definitions, current viewport controls and the production visibility/occlusion rules. No new gameplay rules.

- [x] Write failing tests for font limits/resizing, nickname/account formatting, formatting isolation and the six obstruction keys.
- [x] Build mode tables and editable block-pattern lists in continuous forms.
- [x] Add truthful near/far chat and wall examples. Draw only effects actually supported by the selected settings.
- [x] Verify unlimited hearing ranges versus clear-sound-path rules and current block-pattern validation, including conflicting/invalid patterns.
- [x] Test disabled-value retention, text clipping, popup isolation and malformed-formatting containment.
- [x] Capture compact before/after examples, remove the 15 completed pending keys and commit.

## Task 5: Finish voice and TTS configuration

**Deliverable:** `chat.voices` and `chat.tts`: **13 keys**.

**Files:** Modify catalog/dialog/capture scenes. Reuse `ChatterMessagePlanner.cs` and the existing RPTTS integration path in `RPProximityChatSystem.cs`. Extend chatter planning and GUI tests; add TTS parameter-forwarding tests at its existing integration boundary.

**Interfaces:** Current `ChatterModeVolume`, `ChatterModePitch`, `ChatterSelfVolumeMultiplier`, `ModConfig.GetRpttsGain` and `GetRpttsFalloff` contracts remain unchanged.

- [x] Write failing tests that edited mode values reach the production planner or integration boundary with the correct ranges and fallbacks.
- [x] Add labeled per-mode controls. State gain, pitch, falloff and self-volume meaning accurately.
- [x] Identify availability separately for optional TTS and relay integrations. Allow preconfiguration; do not claim an absent provider is working.
- [x] Test no-provider and dependency-off states without real external synthesis or Discord output.
- [x] Render standalone controls. Record audibility and optional-provider behavior as separate human QA cards.
- [x] Remove the final 13 pending keys, assert complete 210-key coverage and commit.

## Task 6: Put search in the shared editor

**Deliverable:** Search finds scalar settings and structured draft entries and navigates without opening an old editor.

**Files:** Modify `ConfigAdminSearch.cs`, `SetupWizardDialog.cs`, `SetupWizardLanguageEditor.cs` and `SetupWizardSheetEditor.cs`. Add `SetupWizardSearchPanel.cs` in ChatUiSystem. Extend `ConfigAdminSearchTests.cs` and add `SetupWizardSearchTests.cs`.

**Proposed interfaces:**

- `SetupWizardCatalog.FindPageForSetting(string key)` returns the canonical page or null. Test existing keys that appear on more than one page.
- `SetupWizardDialog.FocusSetting(string key)` returns whether the field could be found, opening its page and revealing/focusing the control.
- A structured search target carries destination, current draft-entry identity and field key. Existing originals may identify persisted entries; new entries need session-only identity. Do not persist synthetic search IDs.
- Refactor the search index to accept current scalar dictionaries and language/template model lists. Keep AdminConfig independent of GUI classes.
- `SetupWizardSearchPanel` owns query/result scroll and keyboard navigation. It never owns a settings draft.

- [x] Write failing tests for exact keys, friendly text, descriptions, current unsaved values, renamed/reordered entries and duplicate/invalid draft labels.
- [x] Add the sidebar search field and content-area result list with category/page context. Escape clears or leaves search before closing the whole editor.
- [x] Implement field navigation and short-lived focus indication. Returning to results restores query, position and selection.
- [x] Ensure searching a sheet indexes template definitions only, never real answers.
- [x] Test opening search while a picker is active, compact keyboard focus, empty/no-result states and repeated navigation with unsaved edits.
- [x] Show standalone search, result and target renders before refreshing Profile2. Commit.

## Task 7: Replace old-menu actions and prepare command compatibility

**Deliverable:** The new editor can perform every required old-menu action; existing commands can target the appropriate new page.

**Files:** Modify the client controller, ChatUiSystem, setup server handler, command registration and relevant message models only as needed. Extend authorization, persistence, setup server, protocol and controller tests.

**Contracts to settle before coding:**

- Root and player-only requirements remain identical for all config aliases.
- Preserve `ReviewedConfigSettingKeys`. The New settings filter and explicit Mark reviewed action use an authoritative server snapshot, not guesses from visited pages.
- A reload action uses the existing safe disk-reload implementation, with dirty-draft confirmation. It is distinct from refreshing the setup snapshot.
- If a page destination or reviewed-key data needs wire fields, add optional fields after the currently highest ProtoMember. Both setup messages currently end at 13; verify again before assigning tags.
- Additive request-kind values go after existing enum values. Capture-only sessions cannot review metadata, reload disk, or save.
- Deep links to languages/sheet fields navigate the existing draft when one is active. Reissuing a command must not silently replace it with a fresh server snapshot.
- During a pending or uncertain save, direct commands must retain the existing session until its result is resolved. Test delayed results and repeated commands rather than creating a competing save session.

- [x] Test Mark reviewed, failed/valid disk reload, handbook return, dirty-draft cancellation and failed/uncertain save recovery.
- [x] Add the New settings filter, deliberate reviewed-metadata action, maintenance reload and Help access.
- [x] Add page-targeted opens for direct language/sheet commands, preserving aliases. Keep the old routes until command-switch acceptance.
- [x] Implement and test `/tb config advanced` as the temporary old-menu route.
- [x] Change the setup-initialization failure message to the explicit fallback before changing the default command.
- [x] Test older/unknown destinations and messages, permission revocation mid-session, malformed files and atomic mixed saves.
- [x] Capture the action/search states and present the concrete human QA cards. Commit.

## Task 8: Switch the default, validate, then delete legacy windows

**Deliverable:** The sidebar editor becomes the normal entry point. Old UI removal happens only after its separate acceptance gate.

**Files:** Command registration in `RPProximityChatSystem.cs`; setup controller/dialog/ChatUiSystem lifecycle; handbook/help text; legacy dialog classes, fixtures and routing tests; applicable QA documentation.

- [ ] Confirm the pending key set is empty, structured editors retain parity, all action replacements work and planned human observations have been supplied.
- [ ] Route `/tb config`, `/basic config` and the equivalent `thebasics` root alias to the new UI. Preserve `setup`, `config languages/language`, `config charsheetfields/sheetfields/biofields` and top-level sheet-field aliases.
- [ ] Remove the main editor's All settings escape to the old UI. Keep the deliberate advanced command during the agreed compatibility period.
- [ ] Human QA: direct commands, search-to-field and return, one mixed atomic save, conflict/permission/write-failure recovery, disk reload, reviewed metadata, keyboard access and both compact scales.
- [ ] Separately test actual voice output and optional integrations if available. Label absent-provider verification unavailable rather than passed.
- [ ] With owner approval, perform the final restart/reconnect activation batch and obtain explicit human QA completion.
- [ ] After the compatibility decision, remove old windows and their client navigation, static drafts and obsolete capture scenes. Retain shared registry, validation, language/template models and gameplay dependencies.
- [ ] Replace the temporary setup-failure fallback with a tested supported recovery route before removing its advanced command.
- [ ] Decide old packet-handler support separately. Do not renumber or delete wire contracts merely because their UI was removed.
- [x] Run the appropriate full regression suite, canonical local build/package and standalone captures. Record the exact package/source identity for any later staged QA. Commit, with no merge or release inferred.

Task 8 local validation is complete. Default routing, human acceptance, activation, fallback retirement and older-client compatibility remain pending. See the [automated review](../../qa/2026-10-28-config-retirement-automated-review.md) and [pending human cards](../../qa/2026-10-28-config-retirement-human-qa.md).

## Validation commands

Run only checks relevant to the task, then the complete regression pass at final retirement. Set the usual GUI environment when executing visual tests.

```powershell
$env:VINTAGE_STORY = 'D:/Games/Vintagestory'
$env:THEBASICS_GUI_ASSETS = Join-Path (Get-Location) 'mods-dll/thebasics/assets'
dotnet test mods-dll/thebasics.Tests/thebasics.Tests.csproj -p:SkipPostBuildPackage=true --filter 'FullyQualifiedName~SetupWizard|FullyQualifiedName~ConfigAdmin'
.\scripts\preview-gui.ps1 -GamePath $env:VINTAGE_STORY -Wizard -Width 1280 -Height 720 -Output <new-artifact-directory>
.\mods-dll\thebasics\scripts\build-and-package.ps1 -LocalOnly
```

Use a new artifact directory and a build receipt/source hash when preparing an actual QA package. Build/package alone is not installation, in-game acceptance, a merge or a release.
