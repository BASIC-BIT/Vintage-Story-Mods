# Settings preview gaps

The next visual pass should make setting changes visible in the example itself. Several current samples redraw without demonstrating the setting beside them. The strongest opportunities are travel, understanding speech and signing, player visibility, and scene markers.

This is a source and existing-render audit, not an implemented redesign. The review covered the 40-page catalog, preview drawing and relevant gameplay code. Game launching and live QA remain paused.

## Recommended order

Start with **distant speech, languages, signing and travel**, where an administrator currently gets little help understanding the rules. Add **scene markers** using their existing renderer. Bundle the small **text-cleanup and dice-artwork corrections** with the affected chat/rendering work. Then improve nametags and typing, character sheets, and notes.

Travel has the largest visual-design cost. The two-platform scene the owner originally described fits: Pip starts on one platform, waits, teleports to the destination, then reacts. Reuse it across the travel pages with a relevant destination and event. Chat examples and scene-marker artwork can reuse more of the existing UI immediately.

## Missing or weak responses

| Setting or group | What the preview does today | A useful example |
| --- | --- | --- |
| Teleport tools, destinations, costs and delays | Every travel page uses the same four upright figures, animated only by time. Most pages add fixed command text. | Pip travels to a home, spawn flag, surface or previous position. A disabled tool does not complete its journey. Show the real gear item when that journey has a cost, and a countdown when there is a warmup. |
| Cancel warmup on damage or interaction | Neither toggle affects the picture. The fixed caption only mentions movement. | During a countdown, Pip takes a hit or interacts. The chosen rule determines whether the countdown cancels or travel finishes. Show the actual cancellation message. Movement always cancels; zero warmup skips the countdown. |
| Distant-speech obfuscation | Diamonds, circles and numbers change; no message becomes harder to read. | A listener moves away while a native chat line changes from clear to partially obscured. A wall can demonstrate the extra penalty. Keep grid-distance delivery separate from straight-line fading. Unlimited speech stays clear. In Prose, only quoted speech becomes obscured; action text stays clear. |
| Languages on or off | The Languages and OOC page's speech sample ignores the language-system toggle. | Show a sample Tradeband message received by a listener who understands it and one who does not. Use the real configured color and deterministic syllable scrambling. Turning languages off removes those language effects. This needs no language editor. |
| Sign-language range and line of sight | The same signing line stays visible regardless of either setting. | A viewer stands inside/outside the range, then behind a wall. Their chat line appears only if they can receive it. A signing range of 5 excludes a viewer at grid distance 5. |
| Text capitalization and punctuation | The sample already says `Hello, traveler!`, so the toggle has nothing to fix. | Put lowercase, unfinished input in the existing chat input and show the correctly formatted received message. Follow the actual message type's capitalization and punctuation rules. |
| Remembered/default chat tab and incoming switching | The default can select a tab; remembering and incoming-switch protection are never demonstrated. | Choose a tab, close/reopen chat, then receive a message in another group. The selected tab responds to the settings. General-as-proximity overrides the separate-tab choice; do not promise switching when the game's own auto-switch preference is off. |
| Nametag range, line of sight and targeting | Positive ranges all look alike. Line of sight is ignored. Targeting replaces the plate with a sentence. | Keep the actual nameplate above Pip. A near/far, wall and reticle sequence determines whether it appears. Show the outcome rather than text explaining the rule. Use the game's themed plate treatment for empty color values. |
| Bubble duration and typing timeout | Both samples remain static. Typing uses invented dots. | Replay a short spoken line and let its bubble expire at the real effective duration. For typing, show active typing followed by idle composing, using the real paintbrush icon and Typing/Thinking states. Typing timeout changes state, not disappearance. The custom label applies while actively typing. |
| Scene markers | No preview. | Use the existing marker icon and floating description artwork. Turning the feature off hides the marker and text while preserving the placed marker's data. |
| Character-sheet opening and inspection | The C-menu toggle does not affect the sample. The access page has no preview, including its range, sight and required-field rules. | Show the sheet beside the C character window when auto-open is enabled. For inspection, use a nearby character and a wall, opening the sheet or showing the real failure response. An empty required field can demonstrate rejected speech, followed by success after completion. Keep permission strings out of the picture. |
| Staff notes and ledger | Generic Note/Ledger rows hide the difference between separate entries and one document. | Mirror the native entry list, selected entry and freeform layout, with sample content and available actions matching enabled features. This explains the choice without illustrating access permissions or inventing a different editor layout. |
| Dice presentation | Invented beige dice and different wording. | Reuse the mod's purple result bubble and real chat formatting. The preview should look like the result players receive. |

## Keep the good examples

Map visibility already responds to range and hiding with native player markers and the game's palette. Save announcements already use native chat and notification coloring. Keep both.

Sleep threshold crossing already changes the reminder correctly. A later art pass should put the sleepers visibly in beds instead of leaving them upright with rectangles underneath.

Permission codes, note-length limits, retention limits and command-access fields usually need clear controls and help text, not an illustration. Remove the unrelated Common-language chat sample from Language access unless it demonstrates a language-list or grant change.

## Implementation reach

Reuse `SetupWizardDialog`'s native chat window and the existing detached actor. Use the actual formatting, obfuscation and rendering helpers with deterministic sample data. Do not send sample chat, alter player state or simulate saved configuration by touching the server.

The relevant changes would reach the preview drawing, page routing/layout, fixed-time capture fixtures and focused GUI tests. No configuration keys or wire fields need to change. Existing picture-equality cases deliberately require identical pictures after changes to bubble duration, typing timeout and C-menu auto-open. Replace those cases with time/state-specific checks while retaining the no-picture-change checks for permissions and unrelated limits.

## Source evidence

- Chat and travel samples: [SetupWizardDialog.cs](../../mods-dll/thebasics/src/ModSystems/ChatUiSystem/SetupWizardDialog.cs), especially `AddPreview`, `BuildPreviewText`, `TeleportExplanation`, `DrawDiagram` and `DrawHearing`.
- Appearance, sheets, records and dice samples: [SetupWizardFeaturePreview.cs](../../mods-dll/thebasics/src/ModSystems/ChatUiSystem/SetupWizardFeaturePreview.cs).
- Current unchanged-picture assertions: [SetupWizardChatPreviewTests.cs](../../mods-dll/thebasics.Tests/ModSystems/ChatUiSystem/SetupWizardChatPreviewTests.cs), `FeaturePicturesStayFocusedOnAppearanceWhenOnlyAccessOrLimitsChange`.
- Recipient rules and text effects: `RecipientDeterminationTransformer`, `DistanceObfuscationSystem`, `ObfuscationTransformer`, `ICSpeechFormatTransformer`, `LanguageSystem.ProcessMessage`, `LanguageScrambler.ScrambleMessage`, `AutoCapitalizationTransformer`, `AutoPunctuationTransformer` and `ChatHelper.ObfuscateOutsideMarkup`.
- Travel outcomes: `TeleportationSystem` warmup cancellation and `HomeSpawnSystem` destination, cost and timing paths.
- Native visual reuse: `SceneMarkerVisuals.DescriptionSurface`, `DiceBubbleTexture.CreateSurface`, `DicePresentation.Chat`, `PlayerNotesDialog`, `CharacterSheetDialog` and `EntityBehaviorNameTagPatches`.
- Appearance behavior: `NameTagRenderRangePatches`, `SpeechBubbleVtmlPatches` and `TypingIndicatorRenderer`.

Existing standalone captures inspected: travel, hearing ranges, nametags and character sheets in `.tmp/wizard-sidebar-native-20261015/renders-wide/scale-1.25/`. These establish the current illustrations' appearance, not the proposed motion or in-game acceptance.
