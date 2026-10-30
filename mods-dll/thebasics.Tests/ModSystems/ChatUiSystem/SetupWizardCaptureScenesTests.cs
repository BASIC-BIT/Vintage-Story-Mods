using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardCaptureScenesTests
{
    [Fact]
    public void SleepCaptureShowsTwoPlayersInBedAtTheDefaultReminderThreshold()
    {
        var time = SetupWizardCaptureScenes.DefaultPreviewTime("wizard-notifications-sleep");
        Assert.Equal(4.5, time);
        Assert.Equal(2, (int)(time / 2) % 4);
    }

    [VisualTheory]
    [InlineData("wizard-chat-obscured", "chat.obfuscation", "EnableDistanceObfuscationSystem", "1", 5)]
    [InlineData("wizard-chat-clear", "chat.obfuscation", "EnableDistanceObfuscationSystem", "0", 5)]
    [InlineData("wizard-chat-signing-visible", "chat.signlanguage", "RequireLineOfSightForSignLanguage", "0", 8.5)]
    [InlineData("wizard-chat-signing-blocked", "chat.signlanguage", "RequireLineOfSightForSignLanguage", "1", 8.5)]
    [InlineData("wizard-chat-language-enabled", "chat.language", "EnableLanguageSystem", "1", 5)]
    [InlineData("wizard-chat-language-disabled", "chat.language", "EnableLanguageSystem", "0", 5)]
    [InlineData("wizard-travel-damage-cancels", "teleport.warmup", "Teleportation.CancelWarmupOnDamage", "1", 7)]
    [InlineData("wizard-travel-damage-continues", "teleport.warmup", "Teleportation.CancelWarmupOnDamage", "0", 7)]
    [InlineData("wizard-roleplay-markers-enabled", "roleplay.scenemarkers", "EnableSceneMarkers", "1", 0.75)]
    [InlineData("wizard-roleplay-markers-disabled", "roleplay.scenemarkers", "EnableSceneMarkers", "0", 0.75)]
    [InlineData("wizard-appearance-bubbles-visible", "appearance.bubbles", "SpeechBubbleMinimumDisplayMilliseconds", "3500", 0.5)]
    [InlineData("wizard-appearance-bubbles-expired", "appearance.bubbles", "SpeechBubbleMinimumDisplayMilliseconds", "3500", 5)]
    [InlineData("wizard-appearance-typing-active", "appearance.typing", "TypingIndicatorTimeoutSeconds", "5", 1)]
    [InlineData("wizard-appearance-typing-thinking", "appearance.typing", "TypingIndicatorTimeoutSeconds", "5", 5.1)]
    [InlineData("wizard-appearance-names-targeted", "appearance.names", "HideNametagUnlessTargeting", "1", 0.5)]
    [InlineData("wizard-appearance-names-untargeted", "appearance.names", "HideNametagUnlessTargeting", "1", 3.5)]
    [InlineData("wizard-appearance-names-blocked", "appearance.names", "HideNametagUnlessTargeting", "1", 9.5)]
    [InlineData("wizard-appearance-names-distant", "appearance.names", "HideNametagUnlessTargeting", "1", 6.5)]
    [InlineData("wizard-characters-autoopen", "characters.sheets", "CharacterSheetAutoOpenOnCharacterScreen", "1", 0.75)]
    [InlineData("wizard-characters-manual", "characters.sheets", "CharacterSheetAutoOpenOnCharacterScreen", "0", 0.75)]
    [InlineData("wizard-characters-slots-first", "characters.slots", "EnableRpCharacterSlots", "1", 0.75)]
    [InlineData("wizard-characters-slots-second", "characters.slots", "EnableRpCharacterSlots", "1", 4.75)]
    [InlineData("wizard-notes-entries", "server.staffnotes", "EnableStructuredAdminNotes", "1", 0.75)]
    [InlineData("wizard-notes-notebook", "server.staffnotes", "EnableStructuredAdminNotes", "0", 0.75)]
    [InlineData("wizard-personal-notebook", "server.personalnotes", "EnablePlayerNotes", "1", 0.75)]
    [InlineData("wizard-chat-focus-switches", "chat.tabs", "PreventProximityChannelSwitching", "0", 1.25)]
    [InlineData("wizard-chat-focus-stays", "chat.tabs", "PreventProximityChannelSwitching", "1", 1.25)]
    [InlineData("wizard-chat-focus-remembers", "chat.tabs", "PreserveDefaultChatChoice", "1", 7.25)]
    [InlineData("wizard-chat-focus-default", "chat.tabs", "PreserveDefaultChatChoice", "0", 7.25)]
    public void EffectCapturesUseAnIsolatedDraftAndTheNamedOutcome(string scenario, string pageId, string key, string value, double time)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var values = SetupWizardCaptureScenes.DefaultValues();
        var draft = new SetupWizardDraft(values);
        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        var saves = 0;
        using var wizard = new SetupWizardDialog(host.Api, draft, "qa-run", true,
            () => saves++, null!, null!, layoutOnly: true, captureOnly: true, actorFactory: host.CreateGuide);

        Assert.Equal(time, SetupWizardCaptureScenes.DefaultPreviewTime(scenario));
        SetupWizardCaptureScenes.Show(wizard, scenario, time);

        Assert.Equal(pageId, wizard.CurrentStepId);
        Assert.Equal(value, draft.Get(key));
        Assert.Equal(values.Single(setting => setting.Key == "EnableChatter").Value, draft.Get("EnableChatter"));
        Assert.False(draft.IsSavePending);
        Assert.True(wizard.IsCaptureOnly);
        Assert.Equal(0, saves);
    }

    [Theory]
    [InlineData("wizard-chat-obscured", "wizard-chat-clear", "EnableDistanceObfuscationSystem")]
    [InlineData("wizard-chat-signing-visible", "wizard-chat-signing-blocked", "RequireLineOfSightForSignLanguage")]
    [InlineData("wizard-chat-language-enabled", "wizard-chat-language-disabled", "EnableLanguageSystem")]
    [InlineData("wizard-travel-damage-cancels", "wizard-travel-damage-continues", "Teleportation.CancelWarmupOnDamage")]
    [InlineData("wizard-roleplay-markers-enabled", "wizard-roleplay-markers-disabled", "EnableSceneMarkers")]
    [InlineData("wizard-characters-autoopen", "wizard-characters-manual", "CharacterSheetAutoOpenOnCharacterScreen")]
    [InlineData("wizard-chat-focus-switches", "wizard-chat-focus-stays", "PreventProximityChannelSwitching")]
    [InlineData("wizard-chat-focus-remembers", "wizard-chat-focus-default", "PreserveDefaultChatChoice")]
    public void EffectComparisonPairsChangeOnlyTheDemonstratedSetting(string first, string second, string key)
    {
        var firstDraft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var secondDraft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        SetupWizardCaptureScenes.ApplyDraft(firstDraft, first);
        SetupWizardCaptureScenes.ApplyDraft(secondDraft, second);

        Assert.Equal(key, Assert.Single(firstDraft.Values.Where(setting => setting.Value != secondDraft.Get(setting.Key))).Key);
        Assert.Equal(SetupWizardCaptureScenes.DefaultPreviewTime(first), SetupWizardCaptureScenes.DefaultPreviewTime(second));
    }

    [Theory]
    [InlineData("wizard-appearance-bubbles-visible", "wizard-appearance-bubbles-expired")]
    [InlineData("wizard-appearance-typing-active", "wizard-appearance-typing-thinking")]
    [InlineData("wizard-appearance-names-targeted", "wizard-appearance-names-untargeted")]
    [InlineData("wizard-appearance-names-targeted", "wizard-appearance-names-blocked")]
    [InlineData("wizard-appearance-names-targeted", "wizard-appearance-names-distant")]
    [InlineData("wizard-characters-slots-first", "wizard-characters-slots-second")]
    public void LifecycleComparisonPosesChangeOnlyThePreviewTime(string first, string second)
    {
        var firstDraft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var secondDraft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        SetupWizardCaptureScenes.ApplyDraft(firstDraft, first);
        SetupWizardCaptureScenes.ApplyDraft(secondDraft, second);

        Assert.Equal(firstDraft.Values.Select(value => (value.Key, value.Value)),
            secondDraft.Values.Select(value => (value.Key, value.Value)));
        Assert.NotEqual(SetupWizardCaptureScenes.DefaultPreviewTime(first), SetupWizardCaptureScenes.DefaultPreviewTime(second));
    }

    [Fact]
    public void StaffNoteFixturesSelectTheExistingEntryOrLedgerControls()
    {
        var entries = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var notebook = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        SetupWizardCaptureScenes.ApplyDraft(entries, "wizard-notes-entries");
        SetupWizardCaptureScenes.ApplyDraft(notebook, "wizard-notes-notebook");

        Assert.Equal("1", entries.Get("EnableStructuredAdminNotes"));
        Assert.Equal("0", entries.Get("EnableAdminNoteLedger"));
        Assert.Equal("0", notebook.Get("EnableStructuredAdminNotes"));
        Assert.Equal("1", notebook.Get("EnableAdminNoteLedger"));
        Assert.Equal(new[] { "EnableAdminNoteLedger", "EnableStructuredAdminNotes" },
            entries.Values.Where(value => value.Value != notebook.Get(value.Key)).Select(value => value.Key).OrderBy(key => key));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    public void InteractivePlaybackDoesNotBroadenCaptureTimeValidation(double time)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SetupWizardCaptureScenes.Validate("wizard-appearance-typing-active", time));
    }

    [Fact]
    public void LifecycleFixturesShowTheNamedBubbleAndTypingStates()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        SetupWizardCaptureScenes.ApplyDraft(draft, "wizard-appearance-bubbles-visible");
        Assert.True(SetupWizardAppearancePreview.BubbleVisible(draft, SetupWizardCaptureScenes.DefaultPreviewTime("wizard-appearance-bubbles-visible")));
        Assert.False(SetupWizardAppearancePreview.BubbleVisible(draft, SetupWizardCaptureScenes.DefaultPreviewTime("wizard-appearance-bubbles-expired")));

        SetupWizardCaptureScenes.ApplyDraft(draft, "wizard-appearance-typing-active");
        Assert.Equal(ChatTypingIndicatorState.Typing,
            SetupWizardAppearancePreview.TypingState(draft, SetupWizardCaptureScenes.DefaultPreviewTime("wizard-appearance-typing-active")));
        Assert.Equal(ChatTypingIndicatorState.ChatOpenComposing,
            SetupWizardAppearancePreview.TypingState(draft, SetupWizardCaptureScenes.DefaultPreviewTime("wizard-appearance-typing-thinking")));
    }

    [Theory]
    [InlineData("wizard-appearance-names-targeted", true)]
    [InlineData("wizard-appearance-names-untargeted", false)]
    [InlineData("wizard-appearance-names-blocked", false)]
    [InlineData("wizard-appearance-names-distant", false)]
    public void NametagFixturesShowTheNamedVisibilityOutcome(string scene, bool visible)
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        SetupWizardCaptureScenes.ApplyDraft(draft, scene);
        var sample = SetupWizardAppearancePreview.NameScene(draft, SetupWizardCaptureScenes.DefaultPreviewTime(scene));
        Assert.Equal(visible, SetupWizardAppearancePreview.NameplateVisible(draft, sample.Distance, sample.Targeted, sample.LineOfSight));
    }

    [Theory]
    [InlineData("wizard-chat-focus-switches", 0)]
    [InlineData("wizard-chat-focus-stays", 1)]
    [InlineData("wizard-chat-focus-remembers", 0)]
    [InlineData("wizard-chat-focus-default", 1)]
    public void ChatFocusFixturesShowTheNamedSelection(string scene, int tab)
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        SetupWizardCaptureScenes.ApplyDraft(draft, scene);
        var state = SetupWizardChatFocusPreview.At(draft, SetupWizardCaptureScenes.DefaultPreviewTime(scene), autoChatOpenSelected: true);
        Assert.True(state.IsOpen);
        Assert.Equal(tab, state.SelectedTabIndex);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void PrivilegeCaptureOpensSuggestionsWithoutChangingOrSavingTheDraft(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        var values = SetupWizardCaptureScenes.DefaultValues();
        var draft = new SetupWizardDraft(values);
        var saves = 0;
        using var wizard = new SetupWizardDialog(host.Api, draft, "qa-run", true,
            () => saves++, null!, null!, layoutOnly: true, captureOnly: true, actorFactory: host.CreateGuide);
        wizard.SetSession(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Open,
            Success = true,
            RunId = "qa-run",
            IsDedicated = true,
            Values = values,
            PrivilegeSuggestions = [new() { Code = "custom.server", Description = "Manage custom server tools" }]
        });

        SetupWizardCaptureScenes.Show(wizard, "wizard-privileges", 0.75);

        Assert.Equal("server.history", wizard.CurrentStepId);
        var picker = Assert.IsType<GuiElementPrivilegePicker>(wizard.SingleComposer.GetElement("ChatHistoryPermission"));
        Assert.True(picker.IsSuggestionsOpen);
        Assert.Equal(values.Single(value => value.Key == "ChatHistoryPermission").Value, picker.Value);
        Assert.False(draft.IsDirty);
        Assert.False(draft.IsSavePending);
        Assert.True(wizard.IsCaptureOnly);
        Assert.Equal(0, saves);
    }

    [VisualTheory]
    [InlineData("wizard-map-visibility", "0", "0", "1000")]
    [InlineData("wizard-map-managed", "1", "0", "80")]
    [InlineData("wizard-map-hidden", "1", "1", "80")]
    public void MapCapturesKeepEachPolicyInAnIsolatedDraft(string scenario, string managed, string hidden, string range)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var values = SetupWizardCaptureScenes.DefaultValues();
        var draft = new SetupWizardDraft(values);
        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        var saves = 0;
        using var wizard = new SetupWizardDialog(host.Api, draft, "qa-run", true,
            () => saves++, null!, null!, layoutOnly: true, captureOnly: true, actorFactory: host.CreateGuide);

        SetupWizardCaptureScenes.Show(wizard, scenario, 0.75);

        Assert.Equal("map.visibility", wizard.CurrentStepId);
        Assert.Equal(managed, draft.Get("ManageMapPlayerVisibility"));
        Assert.Equal(hidden, draft.Get("MapHideOtherPlayers"));
        Assert.Equal(range, draft.Get("MapPlayerRenderDistance"));
        Assert.Equal(values.Single(value => value.Key == "EnableChatter").Value, draft.Get("EnableChatter"));
        Assert.False(draft.IsSavePending);
        Assert.True(wizard.IsCaptureOnly);
        Assert.Equal(0, saves);
    }

    [VisualTheory]
    [InlineData("wizard-appearance-bubbles", "appearance.bubbles")]
    [InlineData("wizard-appearance-names", "appearance.names")]
    [InlineData("wizard-map-visibility", "map.visibility")]
    [InlineData("wizard-characters-sheets", "characters.sheets")]
    [InlineData("wizard-roleplay-dice", "roleplay.dice")]
    [InlineData("wizard-server-history", "server.history")]
    [InlineData("wizard-server-stats", "server.stats")]
    public void AddedBranchCapturesOpenTheRequestedPageWithoutSaving(string scenario, string pageId)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var values = SetupWizardCaptureScenes.DefaultValues();
        var draft = new SetupWizardDraft(values);
        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        var saves = 0;
        using var wizard = new SetupWizardDialog(host.Api, draft, "qa-run", true,
            () => saves++, null!, null!, layoutOnly: true, captureOnly: true, actorFactory: host.CreateGuide);

        SetupWizardCaptureScenes.Show(wizard, scenario, 0.75);

        Assert.Equal(pageId, wizard.CurrentStepId);
        Assert.True(SetupWizardCatalog.TryGetPage(pageId, out _));
        Assert.Equal(values.Select(value => (value.Key, value.Value)),
            draft.Values.Select(value => (value.Key, value.Value)));
        Assert.False(draft.IsDirty);
        Assert.False(draft.IsSavePending);
        Assert.True(wizard.IsCaptureOnly);
        Assert.Equal(0, saves);
    }
}
