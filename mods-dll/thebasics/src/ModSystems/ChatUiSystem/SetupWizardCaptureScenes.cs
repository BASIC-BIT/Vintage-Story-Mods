using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Named data fixtures for the production dialog, shared by layout and native captures.</summary>
public static class SetupWizardCaptureScenes
{
    public static readonly string[] Names =
    [
        "wizard-sheet-builder", "wizard-sheet-builder-unselected", "wizard-sheet-builder-selected", "wizard-sheet-builder-choice", "wizard-sheet-builder-hidden", "wizard-sheet-builder-empty", "wizard-sheet-builder-inspector", "wizard-sheet-builder-invalid", "wizard-sheet-builder-review", "wizard-sheet-builder-add", "wizard-sheet-builder-browse", "wizard-sheet-builder-layout",
        "wizard-voices", "wizard-voices-bottom", "wizard-voices-off", "wizard-voices-invalid", "wizard-tts", "wizard-tts-bottom", "wizard-tts-custom", "wizard-tts-invalid",
        "wizard-textappearance-invalid", "wizard-occlusion-invalid", "wizard-occlusion-conflict", "wizard-occlusion-unlimited", "wizard-textappearance", "wizard-textappearance-bottom", "wizard-textappearance-custom", "wizard-occlusion", "wizard-occlusion-bottom", "wizard-occlusion-custom",
        "wizard-wording", "wizard-wording-bottom", "wizard-wording-custom", "wizard-shortcuts", "wizard-shortcuts-bottom", "wizard-shortcuts-custom",
        "wizard-hub", "wizard-chat-default", "wizard-chat-edited", "wizard-chat-presentation", "wizard-chat-language", "wizard-chat-ranges", "wizard-chat-tabs",
        "wizard-travel-default", "wizard-travel-edited", "wizard-notifications-default", "wizard-notifications-edited", "wizard-notifications-sleep",
        "wizard-review", "wizard-error", "wizard-restart-dedicated", "wizard-restart-integrated",
        "wizard-appearance-bubbles", "wizard-appearance-names", "wizard-map-visibility", "wizard-map-managed", "wizard-map-hidden", "wizard-characters-sheets",
        "wizard-travel-homes", "wizard-travel-spawn", "wizard-nearby-deaths", "wizard-placed-descriptions", "wizard-history-limits", "wizard-integrations", "wizard-diagnostics", "wizard-server-stats-bottom", "wizard-roleplay-dice", "wizard-server-history", "wizard-server-stats", "wizard-privileges",
        "wizard-chat-obscured", "wizard-chat-clear", "wizard-chat-signing-visible", "wizard-chat-signing-blocked",
        "wizard-chat-language-enabled", "wizard-chat-language-disabled", "wizard-travel-damage-cancels", "wizard-travel-damage-continues",
        "wizard-roleplay-markers-enabled", "wizard-roleplay-markers-disabled",
        "wizard-appearance-bubbles-visible", "wizard-appearance-bubbles-expired", "wizard-appearance-typing-active", "wizard-appearance-typing-thinking",
        "wizard-appearance-names-targeted", "wizard-appearance-names-untargeted", "wizard-appearance-names-blocked", "wizard-appearance-names-distant",
        "wizard-characters-autoopen", "wizard-characters-manual", "wizard-characters-slots-first", "wizard-characters-slots-second",
        "wizard-notes-entries", "wizard-notes-notebook", "wizard-personal-notebook",
        "wizard-chat-focus-switches", "wizard-chat-focus-stays", "wizard-chat-focus-remembers", "wizard-chat-focus-default",
        "wizard-language-configured", "wizard-language-edited", "wizard-language-invalid", "wizard-language-grants",
        "wizard-language-color-palette", "wizard-language-color-custom", "wizard-language-color-selected",
        "wizard-chat-colors", "wizard-chat-colors-custom", "wizard-nametag-colors", "wizard-nametag-colors-alpha", "wizard-nametag-colors-custom"
    ];

    public static List<ConfigAdminSettingValue> DefaultValues()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return ConfigAdminSettingRegistry.Settings.Where(setting => SetupWizardCatalog.IsSettingKey(setting.Key)).Select(setting => new ConfigAdminSettingValue
        { Key = setting.Key, Value = setting.GetValue(config) }).ToList();
    }

    public static List<LanguageConfigEntryMessage> DefaultLanguages()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return LanguageConfigAdmin.BuildEntries(config);
    }

    public static List<LanguageConfigEntryMessage> LanguagesForScenario(string scenario) =>
        scenario.StartsWith("wizard-language-", StringComparison.Ordinal) ? ConfiguredLanguages() : DefaultLanguages();

    public static List<CharacterSheetFieldConfigEntryMessage> SheetFieldsForScenario(string scenario)
    {
        if (!scenario.StartsWith("wizard-sheet-builder", StringComparison.Ordinal)) return null;
        CharacterSheetFieldConfigEntryMessage Field(string id, string label, string type = "string", string section = "body", string width = "full") =>
            new() { Id = id, OriginalId = id, Label = label, Type = type, LayoutSection = section, Width = width,
                Visibility = "public", Optional = true, ShowInLook = true, MaxLength = "0", EditorRows = "0", Description = "", Options = "", BindTo = "" };
        return new()
        {
            Field("nickname", "Known as", section: "header-side") with { BindTo = "thebasics.nickname", Optional = false },
            Field("calling", "Calling", "option") with { Options = "Cartographer,Herbalist,Wayfarer", Description = "Choose the work your character is known for." },
            Field("origin", "Home shore", width: "half"), Field("years", "Seasons travelled", "number", width: "half"),
            Field("story", "A story worth telling", "longstring") with { EditorRows = "4" },
            Field("memento", "Treasured memento"), Field("promise", "An unfinished promise"),
            Field("fear", "Quiet fear"), Field("hope", "Hope for tomorrow"),
            Field("staff", "Staff continuity note") with { Visibility = "admin", ShowInLook = false },
            Field("farewell", "Parting words")
        };
    }

    public static void Validate(string scenario, double previewTime)
    {
        if (!Names.Contains(scenario, StringComparer.Ordinal)) throw new ArgumentException("Unknown wizard capture scene: " + scenario);
        if (!double.IsFinite(previewTime) || previewTime is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(previewTime), "Preview time must be 0..10 seconds.");
    }

    public static double DefaultPreviewTime(string scenario) => scenario switch
    {
        "wizard-chat-obscured" or "wizard-chat-clear" or "wizard-chat-language-enabled" or "wizard-chat-language-disabled" => 5,
        "wizard-chat-signing-visible" or "wizard-chat-signing-blocked" or "wizard-occlusion" or "wizard-occlusion-bottom" or "wizard-occlusion-custom" => 8.5,
        "wizard-travel-damage-cancels" or "wizard-travel-damage-continues" => 7,
        "wizard-notifications-sleep" => 4.5,
        "wizard-appearance-bubbles-visible" or "wizard-appearance-names-targeted" => 0.5,
        "wizard-appearance-bubbles-expired" => 5,
        "wizard-appearance-typing-active" => 1,
        "wizard-appearance-typing-thinking" => 5.1,
        "wizard-appearance-names-untargeted" => 3.5,
        "wizard-appearance-names-distant" => 6.5,
        "wizard-appearance-names-blocked" => 9.5,
        "wizard-characters-slots-second" => 4.75,
        "wizard-chat-focus-switches" or "wizard-chat-focus-stays" => 1.25,
        "wizard-chat-focus-remembers" or "wizard-chat-focus-default" => 7.25,
        _ => 0.75
    };

    public static void ApplyDraft(SetupWizardDraft draft, string scenario)
    {
        Validate(scenario, 0);
        // Only QA-owned drafts use these deterministic values. Nothing is submitted to the server.
        foreach (var value in DefaultValues()) draft.Set(value.Key, value.Value);
        if (scenario.StartsWith("wizard-language-", StringComparison.Ordinal))
        {
            draft.Languages.Clear();
            draft.Languages.AddRange(ConfiguredLanguages());
        }
        if (scenario == "wizard-language-edited")
        {
            draft.Languages[0] = draft.Languages[0] with { Name = "Harbor Cant", Prefix = "Harbor", Color = "#f0b86e", Syllables = "vel,esh,uin" };
        }
        if (scenario == "wizard-language-invalid")
        {
            draft.Languages[0] = draft.Languages[0] with { Color = "blue", Syllables = string.Empty };
        }
        if (scenario == "wizard-language-color-selected") draft.Languages[0].Color = "#B497D6";
        if (scenario is "wizard-chat-colors" or "wizard-chat-colors-custom")
        {
            draft.Set("EmoteColor", "#9EBE82");
            draft.Set("OOCColor", "#7EAFE0");
            draft.Set("GlobalOOCColor", "#E49A3B");
        }
        if (scenario is "wizard-nametag-colors-alpha" or "wizard-nametag-colors-custom")
        {
            draft.Set("NametagBackgroundColor", "#40352980");
            draft.Set("NametagBorderColor", "#E9DDCEBF");
        }
        if (scenario is "wizard-chat-edited" or "wizard-review" or "wizard-error" or "wizard-restart-dedicated" or "wizard-restart-integrated")
        {
            draft.Set("ProximityChatPresentationMode", "Prose");
            draft.Set("UseGeneralChannelAsProximityChat", "1");
        }
        if (scenario == "wizard-travel-edited")
        {
            draft.Set("AllowPlayerTpa", "0");
            draft.Set("Teleportation.RegisterTopCommand", "0");
        }
        if (scenario == "wizard-notifications-edited")
        {
            draft.Set("SendServerSaveAnnouncement", "1");
            draft.Set("ServerSaveAnnouncementAsNotification", "0");
            draft.Set("SendServerSaveFinishedAnnouncement", "0");
        }
        if (scenario is "wizard-map-managed" or "wizard-map-hidden")
        {
            draft.Set("ManageMapPlayerVisibility", "1");
            draft.Set("MapHideOtherPlayers", scenario == "wizard-map-hidden" ? "1" : "0");
            draft.Set("MapPlayerRenderDistance", "80");
        }
        if (scenario is "wizard-chat-obscured" or "wizard-chat-clear")
        {
            draft.Set("EnableDistanceObfuscationSystem", scenario == "wizard-chat-obscured" ? "1" : "0");
            draft.Set("ProximityChatModeDistances.Normal", "40");
            draft.Set("ProximityChatModeObfuscationRanges.Normal", "8");
        }
        if (scenario is "wizard-chat-signing-visible" or "wizard-chat-signing-blocked")
        {
            draft.Set("EnableLanguageSystem", "1");
            draft.Set("SignLanguageRange", "16");
            draft.Set("RequireLineOfSightForSignLanguage", scenario == "wizard-chat-signing-blocked" ? "1" : "0");
        }
        if (scenario is "wizard-chat-language-enabled" or "wizard-chat-language-disabled")
            draft.Set("EnableLanguageSystem", scenario == "wizard-chat-language-enabled" ? "1" : "0");
        if (scenario is "wizard-travel-damage-cancels" or "wizard-travel-damage-continues")
        {
            draft.Set("Teleportation.RegisterHomeCommands", "1");
            draft.Set("Teleportation.HomeWarmupSeconds", "4");
            draft.Set("Teleportation.CancelWarmupOnDamage", scenario == "wizard-travel-damage-cancels" ? "1" : "0");
        }
        if (scenario is "wizard-roleplay-markers-enabled" or "wizard-roleplay-markers-disabled")
            draft.Set("EnableSceneMarkers", scenario == "wizard-roleplay-markers-enabled" ? "1" : "0");
        if (scenario is "wizard-appearance-bubbles-visible" or "wizard-appearance-bubbles-expired")
        {
            draft.Set("OverheadChatBubbleMode", "RpText");
            draft.Set("SpeechBubbleMinimumDisplayMilliseconds", "3500");
        }
        if (scenario is "wizard-appearance-typing-active" or "wizard-appearance-typing-thinking")
        {
            draft.Set("EnableTypingIndicator", "1");
            draft.Set("TypingIndicatorTimeoutSeconds", "5");
        }
        if (scenario is "wizard-appearance-names-targeted" or "wizard-appearance-names-untargeted" or "wizard-appearance-names-blocked" or "wizard-appearance-names-distant")
        {
            draft.Set("HideNametagUnlessTargeting", "1");
            draft.Set("NametagRenderRange", "30");
            draft.Set("NametagRequiresLineOfSight", "1");
            draft.Set("ShowNicknameInNametag", "1");
            draft.Set("ShowPlayerNameInNametag", "1");
        }
        if (scenario is "wizard-characters-autoopen" or "wizard-characters-manual")
        {
            draft.Set("EnableCharacterSheets", "1");
            draft.Set("CharacterSheetAutoOpenOnCharacterScreen", scenario == "wizard-characters-autoopen" ? "1" : "0");
        }
        if (scenario is "wizard-characters-slots-first" or "wizard-characters-slots-second")
        {
            draft.Set("EnableCharacterSheets", "1");
            draft.Set("EnableRpCharacterSlots", "1");
        }
        if (scenario is "wizard-notes-entries" or "wizard-notes-notebook")
        {
            draft.Set("EnableAdminNotes", "1");
            draft.Set("EnableStructuredAdminNotes", scenario == "wizard-notes-entries" ? "1" : "0");
            draft.Set("EnableAdminNoteLedger", scenario == "wizard-notes-notebook" ? "1" : "0");
        }
        if (scenario == "wizard-personal-notebook") draft.Set("EnablePlayerNotes", "1");
        if (scenario is "wizard-chat-focus-switches" or "wizard-chat-focus-stays" or "wizard-chat-focus-remembers" or "wizard-chat-focus-default")
        {
            draft.Set("UseGeneralChannelAsProximityChat", "0");
            draft.Set("ProximityChatAsDefault", "1");
            draft.Set("PreventProximityChannelSwitching", scenario == "wizard-chat-focus-switches" ? "0" : "1");
            draft.Set("PreserveDefaultChatChoice", scenario == "wizard-chat-focus-default" ? "0" : "1");
        }
    }

    public static void Show(SetupWizardDialog dialog, string scenario, double previewTime)
    {
        Validate(scenario, previewTime);
        ApplyDraft(dialog.Draft, scenario);
        var page = scenario switch
        {
            "wizard-hub" => "hub",
            "wizard-language-configured" or "wizard-language-edited" or "wizard-language-invalid" or "wizard-language-grants" or
                "wizard-language-color-palette" or "wizard-language-color-custom" or "wizard-language-color-selected" => "chat.languageeditor",
            "wizard-chat-language" or "wizard-chat-language-enabled" or "wizard-chat-language-disabled" => "chat.language",
            "wizard-chat-colors" or "wizard-chat-colors-custom" => "chat.colors",
            "wizard-nametag-colors" or "wizard-nametag-colors-alpha" or "wizard-nametag-colors-custom" => "appearance.nametagcolors",
            "wizard-chat-obscured" or "wizard-chat-clear" => "chat.obfuscation",
            "wizard-chat-signing-visible" or "wizard-chat-signing-blocked" => "chat.signlanguage",
            "wizard-chat-ranges" => "chat.ranges",
            "wizard-chat-tabs" or "wizard-chat-focus-switches" or "wizard-chat-focus-stays" or "wizard-chat-focus-remembers" or "wizard-chat-focus-default" => "chat.tabs",
            "wizard-travel-default" or "wizard-travel-edited" => "teleport.tools",
            "wizard-travel-damage-cancels" or "wizard-travel-damage-continues" => "teleport.warmup",
            "wizard-notifications-default" or "wizard-notifications-edited" => "notifications.savestart",
            "wizard-notifications-sleep" => "notifications.sleep",
            "wizard-appearance-bubbles" or "wizard-appearance-bubbles-visible" or "wizard-appearance-bubbles-expired" => "appearance.bubbles",
            "wizard-appearance-typing-active" or "wizard-appearance-typing-thinking" => "appearance.typing",
            "wizard-appearance-names" or "wizard-appearance-names-targeted" or "wizard-appearance-names-untargeted" or "wizard-appearance-names-blocked" or "wizard-appearance-names-distant" => "appearance.names",
            "wizard-map-visibility" or "wizard-map-managed" or "wizard-map-hidden" => "map.visibility",
            "wizard-characters-sheets" or "wizard-characters-autoopen" or "wizard-characters-manual" => "characters.sheets",
            "wizard-characters-slots-first" or "wizard-characters-slots-second" => "characters.slots",
            "wizard-notes-entries" or "wizard-notes-notebook" => "server.staffnotes",
            "wizard-personal-notebook" => "server.personalnotes",
            "wizard-roleplay-dice" => "roleplay.dice",
            "wizard-roleplay-markers-enabled" or "wizard-roleplay-markers-disabled" => "roleplay.scenemarkers",
            "wizard-server-history" or "wizard-privileges" => "server.history",
            "wizard-server-stats" or "wizard-server-stats-bottom" => "server.stats",
            "wizard-travel-homes" => "teleport.homes",
            "wizard-travel-spawn" => "teleport.spawn",
            "wizard-nearby-deaths" => "notifications.deaths",
            "wizard-placed-descriptions" => "roleplay.descriptions",
            "wizard-history-limits" => "server.retention",
            "wizard-integrations" => "server.integrations",
            "wizard-diagnostics" => "server.diagnostics",
            "wizard-review" => "review",
            _ => "chat.basics"
        };
        if (scenario.StartsWith("wizard-sheet-builder", StringComparison.Ordinal)) page = "characters.builder";
        if (scenario.StartsWith("wizard-voices", StringComparison.Ordinal)) page = "chat.voices";
        if (scenario.StartsWith("wizard-tts", StringComparison.Ordinal)) page = "chat.tts";
        if (scenario == "wizard-voices-off") { dialog.Draft.Set("EnableChatter", "0"); dialog.Draft.Set("ChatterModePitch.Normal", "0.71234567"); }
        if (scenario == "wizard-voices-invalid") dialog.Draft.Set("ChatterModePitch.Normal", "0");
        if (scenario == "wizard-tts-invalid") dialog.Draft.Set("RPTTS_ModeFalloff.Normal", "not a number");
        if (scenario == "wizard-tts-custom") { dialog.Draft.Set("RPTTS_ModeGain.Normal", "3.123456"); dialog.Draft.Set("RPTTS_ModeFalloff.Normal", "9.123456"); }
        if (scenario.StartsWith("wizard-textappearance", StringComparison.Ordinal)) page = "chat.textappearance";
        if (scenario.StartsWith("wizard-occlusion", StringComparison.Ordinal)) page = "chat.occlusion";
        if (scenario == "wizard-textappearance-invalid") dialog.Draft.Set("ProximityChatDefaultFontSize.Normal", "129");
        if (scenario == "wizard-occlusion-invalid") dialog.Draft.Set("SightPassThroughBlockCodePatterns", "unqualified");
        if (scenario == "wizard-occlusion-conflict")
        {
            dialog.Draft.Set("SightPassThroughBlockCodePatterns", "game:glass-*");
            dialog.Draft.Set("SightBlockingBlockCodePatterns", "game:glass-*");
        }
        if (scenario == "wizard-occlusion-unlimited")
        {
            dialog.Draft.Set("ProximityChatModeDistances.Normal", "-1");
            dialog.Draft.Set("RequireClearSoundPathForSpeech.Normal", "1");
        }
        if (scenario == "wizard-textappearance-custom")
        {
            dialog.Draft.Set("BoldNicknames", "1");
            dialog.Draft.Set("ApplyColorsToPlayerNames", "1");
            dialog.Draft.Set("ProximityChatDefaultFontSize.Normal", "30");
            dialog.Draft.Set("ProximityChatClampFontSizes", "30,24,18");
            dialog.Draft.Set("IsolateChatMessageFormatting", "0");
        }
        if (scenario == "wizard-occlusion-custom")
        {
            dialog.Draft.Set("RequireClearSoundPathForSpeech.Normal", "1");
            dialog.Draft.Set("SpeechOcclusionWallPenaltyBlocks", "12");
            dialog.Draft.Set("SightBlockingBlockCodePatterns", "game:glass-*");
            dialog.Draft.Set("SightPassThroughBlockCodePatterns", "game:stone-*");
        }
        if (scenario.StartsWith("wizard-wording", StringComparison.Ordinal)) page = "chat.wording";
        if (scenario.StartsWith("wizard-shortcuts", StringComparison.Ordinal)) page = "chat.shortcuts";
        if (scenario == "wizard-wording-custom")
        {
            dialog.Draft.Set("ProximityChatModeVerbs.Normal", "murmurs");
            dialog.Draft.Set("ProximityChatModeQuestionVerbs.Normal", "wonders");
            dialog.Draft.Set("ProximityChatModePunctuation.Normal", "...");
            dialog.Draft.Set("ProseNicknameToken", "@self");
        }
        if (scenario == "wizard-shortcuts-custom")
        {
            dialog.Draft.Set("ChatDelimiters.Bold.Start", "[b]");
            dialog.Draft.Set("ChatDelimiters.Bold.End", "");
            dialog.Draft.Set("ChatDelimiters.Italic.Start", "<literal>");
        }
        dialog.ShowPage(page);
        if (scenario is "wizard-wording-bottom" or "wizard-shortcuts-bottom") dialog.SingleComposer.GetScrollbar("settings-scroll").ScrollToBottom();
        if (scenario is "wizard-textappearance-bottom" or "wizard-occlusion-bottom") dialog.SingleComposer.GetScrollbar("settings-scroll").ScrollToBottom();
        if (scenario is "wizard-voices-bottom" or "wizard-tts-bottom") dialog.SingleComposer.GetScrollbar("settings-scroll").ScrollToBottom();
        if (scenario == "wizard-server-stats-bottom") dialog.SingleComposer.GetScrollbar("settings-scroll").ScrollToBottom();
        if (scenario.StartsWith("wizard-sheet-builder", StringComparison.Ordinal)) ShowSheetBuilder(dialog, scenario);
        if (scenario == "wizard-language-grants") dialog.SingleComposer.GetScrollbar("language-editor-scroll").ScrollToBottom();
        if (scenario == "wizard-error") dialog.SetRequestFailure("The setting changed on the server. Review your changes and try saving again.");
        if (scenario.StartsWith("wizard-restart-", StringComparison.Ordinal))
        {
            dialog.Draft.CreateSaveRequest(1, dialog.RunId);
            dialog.SetResult(new TheBasicsSetupWizardResultMessage
            {
                Kind = SetupWizardResultKind.SaveResult, RequestId = 1, RunId = dialog.RunId, Success = true,
                Message = "Settings saved. Restart the server to apply them.",
                Values = dialog.Draft.Values.ToList(), RestartRequiredKeys = ["UseGeneralChannelAsProximityChat"],
                IsDedicated = scenario == "wizard-restart-dedicated"
            });
            dialog.ShowPage("finish");
        }
        dialog.SetPreviewTime(previewTime);
        if (scenario.StartsWith("wizard-language-color-", StringComparison.Ordinal))
        {
            var picker = (GuiElementColorPicker)dialog.SingleComposer.GetElement("language-editor-color-picker");
            var bounds = picker.Bounds;
            var x = (int)(bounds.renderX + bounds.OuterWidth / 2);
            var y = (int)(bounds.renderY + bounds.OuterHeight / 2);
            dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            if (scenario == "wizard-language-color-custom") picker.ExpandCustom();
        }
        if (scenario is "wizard-chat-colors-custom" or "wizard-nametag-colors-alpha" or "wizard-nametag-colors-custom")
        {
            var key = scenario == "wizard-chat-colors-custom" ? "OOCColor" : "NametagBackgroundColor";
            var picker = (GuiElementColorPicker)dialog.SingleComposer.GetElement("color-picker-" + key);
            var bounds = picker.Bounds;
            var x = (int)(bounds.renderX + bounds.OuterWidth / 2);
            var y = (int)(bounds.renderY + bounds.OuterHeight / 2);
            dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            if (scenario.EndsWith("-custom", StringComparison.Ordinal)) picker.ExpandCustom();
        }
        if (scenario == "wizard-chat-presentation")
        {
            var bounds = dialog.SingleComposer.GetElement("ProximityChatPresentationMode").Bounds;
            var x = (int)(bounds.renderX + bounds.OuterWidth / 2);
            var y = (int)(bounds.renderY + bounds.OuterHeight / 2);
            dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        }
        if (scenario == "wizard-privileges")
        {
            var bounds = dialog.SingleComposer.GetElement("ChatHistoryPermission").Bounds;
            var x = (int)(bounds.renderX + bounds.InnerWidth - GuiElement.scaled(12));
            var y = (int)(bounds.renderY + bounds.InnerHeight / 2);
            dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        }
    }

    private static void ShowSheetBuilder(SetupWizardDialog dialog, string scenario)
    {
        void Click(string key)
        {
            var element = dialog.SingleComposer.GetElement(key) ?? throw new InvalidOperationException("Missing capture control: " + key);
            dialog.SingleComposer.FocusElement(element.TabIndex);
            var bounds = element.Bounds;
            var x = (int)(bounds.renderX + bounds.InnerWidth / 2);
            var y = (int)(bounds.renderY + bounds.InnerHeight / 2);
            dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        }
        void Select(int index) { Click("sheet-editor-all"); Click("sheet-editor-field-" + index); }
        if (scenario == "wizard-sheet-builder")
        {
            Select(0);
            if (dialog.SingleComposer.GetElement("sheet-editor-back") != null) Click("sheet-editor-back");
        }
        if (scenario == "wizard-sheet-builder-add") Click("sheet-editor-add");
        if (scenario == "wizard-sheet-builder-browse") Click("sheet-editor-all");
        if (scenario == "wizard-sheet-builder-empty") Click("sheet-editor-filled");
        if (scenario == "wizard-sheet-builder-selected")
        {
            Select(10);
            if (dialog.SingleComposer.GetElement("sheet-editor-back") != null) Click("sheet-editor-back");
        }
        if (scenario is "wizard-sheet-builder-choice" or "wizard-sheet-builder-invalid" or "wizard-sheet-builder-review") Select(1);
        if (scenario == "wizard-sheet-builder-layout")
        {
            Select(4);
            dialog.SingleComposer.GetHorizontalTabs("sheet-editor-tabs").SetValue(2);
            dialog.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Unknown });
        }
        if (scenario == "wizard-sheet-builder-hidden")
        {
            Select(9);
            dialog.SingleComposer.GetHorizontalTabs("sheet-editor-tabs").SetValue(1);
            dialog.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Unknown });
        }
        if (scenario == "wizard-sheet-builder-inspector")
        {
            Select(4);
            var input = dialog.SingleComposer.GetTextInput("sheet-editor-Label");
            dialog.SingleComposer.FocusElement(input.TabIndex);
        }
        if (scenario == "wizard-sheet-builder-invalid") Click("sheet-editor-option-add");
        if (scenario is "wizard-sheet-builder-choice" or "wizard-sheet-builder-invalid")
        {
            var input = dialog.SingleComposer.GetTextInput(scenario.EndsWith("-invalid", StringComparison.Ordinal) ? "sheet-editor-Option-3" : "sheet-editor-Option-2");
            dialog.SingleComposer.FocusElement(input.TabIndex);
            if (scenario == "wizard-sheet-builder-invalid")
            {
                var scrollbar = dialog.SingleComposer.GetScrollbar("sheet-editor-inspector-scroll");
                dialog.SingleComposer.FocusElement(scrollbar.TabIndex);
                dialog.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Down });
            }
        }
        if (scenario == "wizard-sheet-builder-review")
        {
            dialog.SingleComposer.GetTextInput("sheet-editor-Label").SetValue("Life's calling");
            dialog.ShowPage("review");
        }
    }

    private static List<LanguageConfigEntryMessage> ConfiguredLanguages() =>
    [
        new()
        {
            OriginalName = "Dock Cant", Name = "Dock Cant", Description = "A clipped language used by harbor crews.",
            Prefix = "Dock", Color = "#75c9bf", Syllables = "vek,shu,oram", Default = true,
            GrantedToClasses = "trader", GrantedToModels = "seraph", GrantedToModelGroups = "harborfolk", GrantedToTraits = "worldly"
        },
        new()
        {
            OriginalName = "Quayside", Name = "Quayside", Description = "Old words shared along the river.",
            Prefix = "Quay", Color = "#cca4ef", Syllables = "iel,qua,mor", Hidden = true
        }
    ];
}
