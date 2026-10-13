using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cairo;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class SetupWizardDialog : GuiDialog
{
    private const double Width = 880;
    private readonly Action _onSave;
    private readonly Action<string, string, string> _onTrack;
    private readonly Action _onClose;
    private readonly Action _onAdvanced;
    private readonly SetupGuidePreview _preview;
    private bool _isDedicated;
    private GuiElementCustomDraw _diagram;
    private GuiElementRichtext _sample;
    private GuiElementRichtext _sleepCount;
    private int _chatPreviewTab = 1;
    private GuiDialogConfirm _closeConfirm;
    private IReadOnlyList<string> _restartKeys = Array.Empty<string>();
    private string _message;
    private bool _pending;
    private bool _disposing;
    private bool _forceClose;
    private bool _closing;
    private bool _runtimeApplyFailed;
    private int _reviewOffset;
    private double _elapsed;
    private double? _fixedTime;
    private int _lastBeat = -1;
    private string _topicPageId = SetupWizardCatalog.Topics[0].EntryPageId;

    public SetupWizardDialog(ICoreClientAPI api, SetupWizardDraft draft, string runId, bool isDedicated,
        Action onSave, Action<string, string, string> onTrack, Action onClose, Action onAdvanced = null,
        bool layoutOnly = false, bool captureOnly = false) : base(api)
    {
        Draft = draft;
        RunId = runId;
        _isDedicated = isDedicated;
        _onSave = onSave;
        _onTrack = onTrack;
        _onClose = onClose;
        _onAdvanced = onAdvanced;
        LayoutOnly = layoutOnly;
        IsCaptureOnly = captureOnly;
        try
        {
            if (!layoutOnly)
            {
                _preview = new SetupGuidePreview(api);
                _preview.PlayAnimation(AnimationCode());
            }
            ComposeDialog();
        }
        catch
        {
            try { Dispose(); }
            catch (Exception cleanupError) { api.Logger.Warning("Setup dialog cleanup failed: {0}", cleanupError); }
            throw;
        }
    }

    public SetupWizardDraft Draft { get; }
    public string RunId { get; private set; }
    public string CurrentStepId { get; private set; } = "hub";
    public bool LayoutOnly { get; }
    public bool IsCaptureOnly { get; }
    public bool IsPreviewReady => LayoutOnly || SingleComposer?.GetElement("pip") == null || _preview?.IsReady == true;
    public override string ToggleKeyCombinationCode => null;
    public override bool UnregisterOnClose => true;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    public void ShowPage(string stepId)
    {
        if (stepId != "hub" && stepId != "review" && stepId != "finish" && !SetupWizardCatalog.TryGetPage(stepId, out _))
            throw new ArgumentException("Unknown setup page.", nameof(stepId));
        CurrentStepId = stepId;
        if (SetupWizardCatalog.TryGetPage(stepId, out var topicPage)) _topicPageId = topicPage.Id;
        _preview?.EndDrag(false);
        _chatPreviewTab = CurrentStepId.StartsWith("notifications.", StringComparison.Ordinal) || Bool("UseGeneralChannelAsProximityChat") || CurrentStepId == "chat.tabs" && !Bool("ProximityChatAsDefault") ? 0 : 1;
        _reviewOffset = 0;
        _elapsed = 0;
        _lastBeat = -1;
        if (_fixedTime.HasValue) _preview?.SetAnimation(AnimationCode(), _fixedTime.Value);
        else _preview?.PlayAnimation(AnimationCode());
        ComposeDialog();
        Track("viewed");
    }

    public void SetPreviewTime(double timeSeconds)
    {
        if (!double.IsFinite(timeSeconds) || timeSeconds is < 0 or > 60) throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        _fixedTime = timeSeconds;
        _preview?.SetAnimation(AnimationCode(), timeSeconds);
        RefreshPreview();
    }

    public void ResumePreview()
    {
        _fixedTime = null;
        _elapsed = 0;
        _preview?.PlayAnimation(AnimationCode());
    }

    public void SetSession(TheBasicsSetupWizardResultMessage result)
    {
        RunId = result.RunId;
        _isDedicated = result.IsDedicated;
        Draft.RefreshValues(result.Values);
        _restartKeys = result.RestartRequiredKeys?.ToArray() ?? Array.Empty<string>();
        _runtimeApplyFailed = result.RuntimeApplyFailed;
        ComposeDialog();
    }

    public void SetResult(TheBasicsSetupWizardResultMessage result)
    {
        if (!Draft.MergeSaveResult(result)) return;
        _pending = false;
        _isDedicated = result.IsDedicated;
        _message = result.Message;
        _restartKeys = result.RestartRequiredKeys?.ToArray() ?? Array.Empty<string>();
        _runtimeApplyFailed = result.RuntimeApplyFailed;
        if (result.Success && !Draft.IsDirty)
        {
            _message = null;
            ShowPage("finish");
            Track("completed");
        }
        else
        {
            if (result.Success) _message = (_runtimeApplyFailed ? result.Message + " " : "Saved. ") + "Your newer edits are not saved yet.";
            ShowPage("review");
        }
    }

    public void SetRequestFailure(string message)
    {
        Draft.CancelRequest();
        _pending = false;
        _message = message ?? "Could not save your changes. Your edits are still here.";
        ComposeDialog();
    }

    public void Suspend()
    {
        (SingleComposer?.GetElement("pip") as PreviewActorElement)?.CancelDrag();
        _closeConfirm?.TryClose();
        _forceClose = true;
        try { base.TryClose(); }
        finally { _forceClose = false; }
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();
        _diagram = null;
        _sample = null;
        _sleepCount = null;
        var body = ElementBounds.Fixed(0, 0, Width, BodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = capi.Gui.CreateCompo("thebasics-setup-wizard", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle));
        var composer = SingleComposer
            .AddShadedDialogBG(body)
            .AddDialogTitleBar(IsCaptureOnly ? "The BASICs setup (QA preview, save disabled)" : "The BASICs setup", () => TryClose())
            .BeginChildElements(body);
        SetupWizardCatalog.TryGetPage(_topicPageId, out var activePage);
        var activeTopic = SetupWizardCatalog.Topics.First(topic => topic.Id == activePage.TopicId);
        var destinations = new[] { "hub", _topicPageId, "review" };
        var tabs = new[] { "Topics", activeTopic.Title, "Review" }
            .Select((name, index) => new GuiTab { Name = name, DataInt = index }).ToArray();
        composer.AddHorizontalTabs(tabs, ElementBounds.Fixed(0, 40, Width, 32), index => ShowPage(destinations[index]),
            CairoFont.WhiteSmallText().WithFontSize(17), CairoFont.WhiteSmallText().WithFontSize(17).WithColor(GuiStyle.ActiveButtonTextColor), "topic-tabs");
        var selectedTopic = CurrentStepId == "hub" ? 0 : CurrentStepId is "review" or "finish" ? 2 : 1;
        composer.GetHorizontalTabs("topic-tabs").SetValue(selectedTopic, false);
        composer.AddStaticElement(new GuiElementCustomDraw(capi, ElementBounds.Fixed(0, FooterY - 14, Width, 1),
            (context, _, bounds) =>
            {
                context.SetSourceRGBA(0.82, 0.73, 0.59, 0.3);
                context.Rectangle(bounds.bgDrawX, bounds.bgDrawY, bounds.OuterWidth, bounds.OuterHeight);
                context.Fill();
            }), "footer-rule");

        if (CurrentStepId == "hub") ComposeHub(composer);
        else if (CurrentStepId == "review") ComposeReview(composer);
        else if (CurrentStepId == "finish") ComposeFinish(composer);
        else ComposePage(composer);

        Text(composer, _message ?? (_pending ? "Saving settings..." : Draft.IsDirty && CurrentStepId != "hub" ? "Your changes aren't saved yet." : ""),
            0, StatusY, CurrentStepId == "hub" ? 640 : Width, CompactLayout ? 30 : 42);
        Button(composer, "Close", () => TryClose(), Width - 110, FooterY, 110);
        composer.EndChildElements().Compose(focusFirstElement: false);
        _sample = SingleComposer.GetRichtext("preview-text");
        _sleepCount = SingleComposer.GetRichtext("preview-sleep-count");
    }

    private void ComposeHub(GuiComposer composer)
    {
        Text(composer, "Set up your server", 0, 92, Width, 30, true);
        Text(composer, "Choose a topic to get started.", 0, 129, Width, 48);
        for (var index = 0; index < SetupWizardCatalog.Topics.Count; index++)
        {
            var topic = SetupWizardCatalog.Topics[index];
            var x = index % 4 * 224;
            var y = (CompactLayout ? 160 : 169) + index / 4 * (CompactLayout ? 122 : 140);
            composer.AddInteractiveElement(new GuiElementSetupTopicCard(capi, topic.Title, topic.Description, TopicIcon(topic.Id),
                () => { ShowPage(topic.EntryPageId); return true; }, ElementBounds.Fixed(x, y, 208, CompactLayout ? 110 : 124)), "topic-" + topic.Id);
        }
        var changes = Draft.ChangedKeys.Count;
        if (changes > 0 && string.IsNullOrEmpty(_message) && !_pending)
            Text(composer, changes == 1 ? "1 unsaved change" : $"{changes} unsaved changes", 0, StatusY, 640, 30);
        var guideBounds = ElementBounds.Fixed(766, 76, 100, 84);
        if (LayoutOnly) Text(composer, "Native 3D Pip omitted", guideBounds.fixedX, guideBounds.fixedY, guideBounds.fixedWidth, guideBounds.fixedHeight);
        else composer.AddInteractiveElement(new PreviewActorElement(capi, guideBounds, this), "pip");
        Button(composer, "Review changes", () => ShowPage("review"), 0, FooterY, 240);
        composer.AddSmallButton("All settings", () =>
        {
            if (_onAdvanced == null) return false;
            Track("advanced");
            Suspend();
            _onAdvanced();
            return true;
        }, ElementBounds.Fixed(260, FooterY, 240, 30), key: "hub-all-settings");
        composer.GetButton("hub-all-settings").Enabled = _onAdvanced != null;
    }

    private static SetupTopicCardIcon TopicIcon(string id) => id switch
    {
        "chat" => SetupTopicCardIcon.Chat,
        "teleportation" => SetupTopicCardIcon.Teleportation,
        "notifications" => SetupTopicCardIcon.Notifications,
        "appearance" => SetupTopicCardIcon.PlayerDisplay,
        "map" => SetupTopicCardIcon.Map,
        "characters" => SetupTopicCardIcon.Characters,
        "roleplay" => SetupTopicCardIcon.Roleplay,
        "server" => SetupTopicCardIcon.ServerTools,
        _ => SetupTopicCardIcon.ServerTools
    };

    private void ComposePage(GuiComposer composer)
    {
        SetupWizardCatalog.TryGetPage(CurrentStepId, out var page);
        Text(composer, page.Title, 0, 88, Width, 27, true);
        Text(composer, page.Description, 0, 120, Width, 44);
        AddPreview(composer);
        var y = CompactLayout && CurrentStepId == "chat.basics" ? 170d : 178d;
        if (CurrentStepId == "notifications.savestart" || CurrentStepId == "notifications.savefinish")
            AddAnnouncement(composer, ref y);
        else
            foreach (var key in page.SettingKeys.Where(SettingVisible))
                AddSetting(composer, key, ref y);

        var pages = TopicPages(page.TopicId);
        var index = pages.FindIndex(candidate => candidate.Id == page.Id);
        if (index > 0) Button(composer, "< Back", () => { Track("back"); ShowPage(pages[index - 1].Id); }, 8, FooterY, 92);
        else Button(composer, "< Back", () => { Track("back"); ShowPage("hub"); }, 8, FooterY, 92);
        var next = index + 1 < pages.Count ? pages[index + 1].Id : "hub";
        Button(composer, index + 1 < pages.Count ? "Next >" : "Topics", () => ShowPage(next), 108, FooterY, 100);
        Button(composer, "All topics", () => { Track("skip"); ShowPage("hub"); }, 615, FooterY, 132);
        var codes = pages.Select(candidate => candidate.Id).ToArray();
        composer.AddInteractiveElement(new GuiElementDropDown(capi, codes, pages.Select((candidate, number) => $"{number + 1}/{pages.Count}  {candidate.Title}").ToArray(),
            Math.Max(0, index), (value, _) => ShowPage(value), ElementBounds.Fixed(220, FooterY, 380, 30), CairoFont.WhiteSmallText(), false), "page-picker");
    }

    private List<SetupWizardPage> TopicPages(string topic)
    {
        return SetupWizardCatalog.Pages.Where(page => page.TopicId == topic && PageVisible(page.Id)).ToList();
    }

    private bool PageVisible(string id) => id switch
    {
        "teleport.requests" or "teleport.requesttiming" => Bool("AllowPlayerTpa"),
        "teleport.homes" => Bool("Teleportation.RegisterHomeCommands"),
        "teleport.spawn" => Bool("Teleportation.RegisterSpawnCommands"),
        "teleport.top" => Bool("Teleportation.RegisterTopCommand"),
        "teleport.back" => Bool("Teleportation.RegisterBackCommand"),
        "teleport.stuck" or "teleport.stuckpolicy" => Bool("Teleportation.RegisterStuckCommand"),
        "teleport.warmup" => AnyTravelTool(),
        _ => true
    };

    private bool AnyTravelTool() => Bool("AllowPlayerTpa") || Bool("Teleportation.RegisterHomeCommands") || Bool("Teleportation.RegisterSpawnCommands")
        || Bool("Teleportation.RegisterTopCommand") || Bool("Teleportation.RegisterBackCommand") || Bool("Teleportation.RegisterStuckCommand");

    private bool SettingVisible(string key)
    {
        if (key == "TpaCooldownInGameHours") return Bool("TpaUseCooldown");
        if (key == "TpaTimeoutMinutes") return Bool("TpaUseTimeout");
        if (key.StartsWith("Teleportation.Home", StringComparison.Ordinal)) return Bool("Teleportation.RegisterHomeCommands");
        if (key.StartsWith("Teleportation.Spawn", StringComparison.Ordinal)) return Bool("Teleportation.RegisterSpawnCommands");
        if (key == "HomeSpawnRequireTemporalGear") return Bool("Teleportation.RegisterHomeCommands") || Bool("Teleportation.RegisterSpawnCommands");
        return true;
    }

    private void AddSetting(GuiComposer composer, string key, ref double y)
    {
        if (!ConfigAdminSettingRegistry.TryGet(key, out var definition)) return;
        var label = SettingLabel(definition);
        Text(composer, label, 334, y, 546, 21);
        var bounds = ElementBounds.Fixed(334, y + 23, 546, 28);
        var value = Draft.Get(key);
        if (definition.Kind == ConfigAdminSettingKind.Boolean)
        {
            var inverted = key == "DisableRPChat";
            var selected = Bool(key) != inverted ? 1 : 0;
            composer.AddInteractiveElement(new GuiElementDropDown(capi, new[] { "0", "1" }, new[] { "Off", "On" }, selected,
                (choice, _) => Changed(key, inverted ? (choice == "1" ? "0" : "1") : choice, choice == "1" ? "on" : "off", true),
                bounds, CairoFont.WhiteSmallText(), false), key);
        }
        else if (definition.Kind == ConfigAdminSettingKind.Select)
        {
            var options = definition.Options.ToArray();
            var selected = Math.Max(0, Array.FindIndex(options, candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase)));
            if (key == "ProximityChatPresentationMode")
            {
                bounds.fixedHeight = 58;
                composer.AddInteractiveElement(new GuiElementDescribedDropDown(capi, options, options.Select(PresentationName).ToArray(),
                    options.Select(PresentationDescription).ToArray(), selected,
                    (choice, _) => Changed(key, choice, "option" + Array.IndexOf(options, choice), true), bounds, CairoFont.WhiteSmallText()), key);
                y += 30;
            }
            else
                composer.AddInteractiveElement(new GuiElementDropDown(capi, options,
                    options.Select((option, index) => SettingOptionName(key, option, definition.OptionNames[index])).ToArray(), selected,
                    (choice, _) => Changed(key, choice, "option" + Array.IndexOf(options, choice), false), bounds, CairoFont.WhiteSmallText(), false), key);
        }
        else
        {
            var input = new GuiElementTextInput(capi, bounds, choice =>
            {
                var requested = choice;
                if (key == "SleepNotificationThreshold" && double.TryParse(choice, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
                    requested = (percentage / 100).ToString(CultureInfo.InvariantCulture);
                Changed(key, requested, "edited", false);
            }, CairoFont.TextInput());
            input.SetMaxLength(definition.Kind == ConfigAdminSettingKind.Text ? 2048 : 32);
            input.SetValue(key == "SleepNotificationThreshold" ? (Number(key) * 100).ToString(CultureInfo.InvariantCulture) : value);
            composer.AddInteractiveElement(input, key);
        }
        if (key != "ProximityChatPresentationMode")
        {
            var help = key switch
            {
                "SleepNotificationThreshold" => "Percentage of online players who must be in bed to trigger a reminder. Enter 50 for 50%. At 0% or 100%, no reminder is sent.",
                "DisableRPChat" => "Enable roleplay commands, languages and global OOC. Nearby chat still works when this is off.",
                _ => definition.Description
            };
            if (definition.ReloadBehavior == ConfigAdminReloadBehavior.RestartRequired && !help.Contains("restart", StringComparison.OrdinalIgnoreCase))
                help += "\nThis change takes effect after the server restarts.";
            composer.AddHoverText(help, CairoFont.WhiteSmallText(), 400, bounds.FlatCopy(), "help-" + key);
        }
        y += CurrentStepId == "chat.basics" ? (CompactLayout ? 50 : 61) : CompactLayout ? 54 : 65;
    }

    private void AddAnnouncement(GuiComposer composer, ref double y)
    {
        var started = CurrentStepId == "notifications.savestart";
        var enabled = started ? "SendServerSaveAnnouncement" : "SendServerSaveFinishedAnnouncement";
        var popup = started ? "ServerSaveAnnouncementAsNotification" : "ServerSaveFinishedAsNotification";
        var text = started ? "TEXT_ServerSaveAnnouncement" : "TEXT_ServerSaveFinished";
        Text(composer, "Announcement", 334, y, 546, 21);
        composer.AddInteractiveElement(new GuiElementDropDown(capi, new[] { "off", "chat", "popup" }, new[] { "Off", "Chat", "Chat notification" },
            !Bool(enabled) ? 0 : Bool(popup) ? 2 : 1, (choice, _) =>
            {
                Draft.Set(enabled, choice == "off" ? "0" : "1");
                if (choice != "off") Draft.Set(popup, choice == "popup" ? "1" : "0");
                Track("choice", choice);
                RefreshPreview();
            }, ElementBounds.Fixed(334, y + 23, 546, 28), CairoFont.WhiteSmallText(), false), "announcement-mode");
        y += CompactLayout ? 54 : 65;
        AddSetting(composer, text, ref y);
    }

    private void Changed(string key, string value, string choice, bool recompose)
    {
        if (Draft.Get(key) == value) return;
        Draft.Set(key, value);
        if (CurrentStepId == "chat.tabs")
            _chatPreviewTab = Bool("UseGeneralChannelAsProximityChat") || !Bool("ProximityChatAsDefault") ? 0 : 1;
        Track("choice", choice);
        RefreshPreview();
        if (recompose)
        {
            // Widget callbacks must finish before their composer is replaced.
            var page = CurrentStepId;
            capi.Event.RegisterCallback(_ => { if (!_disposing && CurrentStepId == page) ComposeDialog(); }, 0);
        }
    }

    private void ComposeReview(GuiComposer composer)
    {
        Text(composer, "Review changes", 0, 90, Width, 28, true);
        Text(composer, "Check the changes below, then save. Anything marked After restart will take effect when the server restarts.", 0, 125, Width, 42);
        var keys = Draft.ChangedKeys;
        var y = 180d;
        foreach (var key in keys.Skip(_reviewOffset).Take(5))
        {
            ConfigAdminSettingRegistry.TryGet(key, out var definition);
            Text(composer, SettingLabel(definition) + (definition.ReloadBehavior == ConfigAdminReloadBehavior.Live ? "" : " | After restart"), 0, y, Width, 20, true);
            Text(composer, $"{DisplayValue(definition, Draft.GetOriginal(key))}  >  {DisplayValue(definition, Draft.Get(key))}", 0, y + 22, Width, 36);
            y += CompactLayout ? 54 : 64;
        }
        if (keys.Count == 0) Text(composer, NeedsRestart ? "No new changes. Restart the server to apply your saved settings." : "No changes to save.", 0, y, Width, 60);
        if (keys.Count > 5)
            Button(composer, $"More changes ({_reviewOffset + 1}-{Math.Min(_reviewOffset + 5, keys.Count)} of {keys.Count})", () =>
            { _reviewOffset = _reviewOffset + 5 < keys.Count ? _reviewOffset + 5 : 0; ComposeDialog(); }, 335, FooterY, 420);
        Button(composer, "Back to topics", () => { Track("back"); ShowPage("hub"); }, 0, FooterY, 150);
        if (!IsCaptureOnly)
            Button(composer, _pending ? "Saving..." : "Save settings", () =>
            {
                if (_pending) return;
                _pending = true;
                _message = "Saving settings...";
                ComposeDialog();
                _onSave?.Invoke();
            }, 160, FooterY, 160);
    }

    private void ComposeFinish(GuiComposer composer)
    {
        Text(composer, IsCaptureOnly ? "QA example: saved settings" : "Settings saved", 0, 90, Width, 28, true);
        if (!NeedsRestart)
            Text(composer, "Your settings are now in use.", 0, 145, Width, 80);
        else
        {
            Text(composer, "Restart needed", 0, 143, Width, 27, true);
            var names = _restartKeys.Select(key => ConfigAdminSettingRegistry.TryGet(key, out var setting) ? SettingLabel(setting) : key);
            Text(composer, _runtimeApplyFailed ? "Restart the server to apply the saved settings.\n" + string.Join(", ", names) : string.Join(", ", names), 0, 179, Width, 126);
            Text(composer, RestartInstructions(), 0, 330, Width, 90);
        }
        Button(composer, "Back to setup", () => ShowPage("hub"), 0, FooterY, 180);
        Button(composer, NeedsRestart ? "Later" : "Done", () => TryClose(), 195, FooterY, 150);
    }

    private string RestartInstructions() => _isDedicated
        ? "Restart the server from your hosting panel. If you host it yourself, stop and start the server. Players will need to reconnect."
        : "Save and quit to the main menu, then reopen this world.";

    private void AddPreview(GuiComposer composer)
    {
        if (SetupWizardFeaturePreview.Supports(CurrentStepId))
        {
            var appearance = CurrentStepId.StartsWith("appearance.", StringComparison.Ordinal);
            _diagram = new GuiElementCustomDraw(capi, ElementBounds.Fixed(0, 174, 310,
                appearance ? 64 : 220),
                (context, surface, bounds) => SetupWizardFeaturePreview.Draw(context, surface, bounds, CurrentStepId, Draft), true);
            composer.AddInteractiveElement(_diagram, "preview-diagram");
            if (appearance)
            {
                var guide = ElementBounds.Fixed(0, 242, 310, CompactLayout ? 190 : 240);
                if (LayoutOnly) Text(composer, "Native 3D Pip omitted", guide.fixedX, guide.fixedY, guide.fixedWidth, guide.fixedHeight);
                else composer.AddInteractiveElement(new PreviewActorElement(capi, guide, this), "pip");
            }
            return;
        }
        if (CurrentStepId is "chat.basics" or "chat.language" or "chat.languageaccess" or "chat.signlanguage" or "chat.tabs" or "notifications.savestart" or "notifications.savefinish")
        {
            AddChatPreview(composer, CompactLayout ? 204 : 212, CompactLayout ? 184 : 230);
            return;
        }
        var hearing = CurrentStepId is "chat.ranges" or "chat.obfuscation";
        var sleep = CurrentStepId == "notifications.sleep";
        var pictureHeight = hearing ? CompactLayout ? 274 : 330 : sleep ? CompactLayout ? 112 : 140 : CompactLayout ? 146 : 168;
        _diagram = new GuiElementCustomDraw(capi, ElementBounds.Fixed(0, 174, 310, pictureHeight), DrawDiagram, true);
        composer.AddInteractiveElement(_diagram, "preview-diagram");
        if (sleep)
        {
            composer.AddRichtext(SleepCountText(), CairoFont.WhiteSmallText().WithFontSize(14),
                ElementBounds.Fixed(0, CompactLayout ? 289 : 317, 310, 20), "preview-sleep-count");
            AddChatPreview(composer, CompactLayout ? 314 : 347, CompactLayout ? 72 : 92);
        }
        else if (!hearing)
            composer.AddRichtext(BuildPreviewText(), CairoFont.WhiteSmallText().WithFontSize(14), ElementBounds.Fixed(10, CompactLayout ? 338 : 366, 289, CompactLayout ? 103 : 129), "preview-text");
    }

    private void AddChatPreview(GuiComposer composer, double y, double historyHeight)
    {
        const double historyWidth = 288;
        var bounds = ElementBounds.Fixed(0, y, historyWidth + 22, historyHeight + 57);
        composer.BeginChildElements(bounds)
            .AddGameOverlay(ElementBounds.Fixed(0, 23, historyWidth + 22, historyHeight + 34), new[] { 0.25, 0.208, 0.161, 0.75 });
        var tabs = CurrentStepId.StartsWith("notifications.", StringComparison.Ordinal) || Bool("UseGeneralChannelAsProximityChat")
            ? new[] { new GuiTab { Name = "General", DataInt = 0 } }
            : new[] { new GuiTab { Name = "General", DataInt = 0 }, new GuiTab { Name = "Proximity", DataInt = 1 } };
        var font = CairoFont.WhiteDetailText().WithFontSize(14);
        composer.AddHorizontalTabs(tabs, ElementBounds.Fixed(0, 0, historyWidth + 22, 23), index =>
        { _chatPreviewTab = index; RefreshPreview(); }, font, font.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "preview-chat-tabs");
        composer.GetHorizontalTabs("preview-chat-tabs").SetValue(tabs.Length == 1 ? 0 : _chatPreviewTab, false);
        var clip = ElementBounds.Fixed(6, 26, historyWidth, historyHeight);
        composer.BeginClip(clip)
            .AddRichtext(BuildPreviewText(), font, ElementBounds.Fixed(0, 0, historyWidth, historyHeight), "preview-text")
            .EndClip()
            .AddCompactVerticalScrollbar(value =>
            {
                var text = SingleComposer.GetRichtext("preview-text");
                text.Bounds.fixedY = -value;
                text.Bounds.MarkDirtyRecursive();
                text.Bounds.CalcWorldBounds();
            }, ElementBounds.Fixed(historyWidth + 12, 24, 10, historyHeight + 3), "preview-chat-scroll");
        var input = new GuiElementChatInput(capi, ElementBounds.Fixed(0, historyHeight + 29, historyWidth + 22, 25), _ => { }) { Enabled = false };
        composer.AddInteractiveElement(input, "preview-chat-input").EndChildElements();
    }

    private void RefreshPreview()
    {
        _diagram?.Redraw();
        _sample?.SetNewText(BuildPreviewText(), CairoFont.WhiteSmallText().WithFontSize(14), null);
        _sleepCount?.SetNewText(SleepCountText(), CairoFont.WhiteSmallText().WithFontSize(14), null);
        var scroll = SingleComposer.GetCompactScrollbar("preview-chat-scroll");
        if (scroll != null && _sample != null)
            scroll.SetHeights((float)_sample.InsideClipBounds.fixedHeight, (float)_sample.Bounds.fixedHeight);
    }

    private string BuildPreviewText()
    {
        if (CurrentStepId.StartsWith("chat.", StringComparison.Ordinal))
        {
            if (CurrentStepId == "chat.signlanguage")
                return Bool("EnableLanguageSystem") && !Bool("DisableRPChat") ? "Pip signs, \"Meet me by the gate.\"" : "";
            if (CurrentStepId == "chat.languageaccess")
                return Bool("EnableLanguageSystem") && !Bool("DisableRPChat") ? "Pip says in Common, \"Hello, traveler!\"" : "";
            var replacement = Bool("UseGeneralChannelAsProximityChat");
            if (!replacement && _chatPreviewTab == 0)
                return "MaraPlayer: Anyone heading north?<br>PipPlayer: Meet you by the gate.";
            var mode = Draft.Get("ProximityChatPresentationMode");
            var config = new ModConfig();
            config.InitializeDefaultsIfNeeded();
            var words = "Hello, traveler!";
            var quoted = ProximityChatPresentationModes.UsesSpeechQuotes(mode) ? ChatHelper.WrapSpeechQuotes(words, null, config, false) : words;
            var name = Bool("DisableRPChat") ? "PipPlayer" : "Pip";
            var example = mode switch
            {
                "SimpleSpeech" or "PlainProximity" => name + ": " + quoted,
                "Prose" => ChatHelper.FormatProseMessage(name + " waves. \"Hello, traveler!\"", null, config, false, text => text),
                _ => name + " says " + quoted
            };
            var oocName = Bool("UseNicknameInOOC") ? "Pip" : "PipPlayer";
            var lines = example + "<br><font color=\"#eaf188\">(OOC) " + oocName + ": One moment.</font>";
            if (Bool("EnableGlobalOOC") && !Bool("DisableRPChat"))
                lines += "<br><font color=\"#f1b288\">(GOOC) MaraPlayer: Back in a minute.</font>";
            return lines;
        }
        if (CurrentStepId.StartsWith("notifications.", StringComparison.Ordinal))
        {
            if (CurrentStepId == "notifications.sleep")
            {
                var threshold = Number("SleepNotificationThreshold");
                // Keep the reminder in history after the threshold crossing, until the example starts again.
                var triggers = Bool("EnableSleepNotifications") && threshold > 0 && threshold < 1 && Sleepers / 4d >= threshold;
                return triggers ? VtmlUtils.EscapeVtml(Draft.Get("TEXT_SleepNotification")) : "";
            }
            var start = CurrentStepId == "notifications.savestart";
            var enabled = Bool(start ? "SendServerSaveAnnouncement" : "SendServerSaveFinishedAnnouncement");
            var popup = Bool(start ? "ServerSaveAnnouncementAsNotification" : "ServerSaveFinishedAsNotification");
            var wording = Draft.Get(start ? "TEXT_ServerSaveAnnouncement" : "TEXT_ServerSaveFinished");
            if (!enabled) return "";
            var escaped = VtmlUtils.EscapeVtml(wording);
            return popup ? "<font color=\"#CCe0cfbb\">" + escaped + "</font>" : escaped;
        }
        return VtmlUtils.EscapeVtml(TeleportExplanation());
    }

    private string TeleportExplanation()
    {
        if (CurrentStepId.Contains("request", StringComparison.Ordinal))
            return "The other player must accept.\n" +
                (Bool("TpaRequireTemporalGear") ? "Sending a request costs one temporal gear." : "No temporal gear is required.") + "\n" +
                (Bool("TpaUseCooldown") ? $"Wait {Draft.Get("TpaCooldownInGameHours")} in-game hours between requests." : "No wait between requests.") + "\n" +
                (Bool("TpaUseTimeout") ? $"Requests expire after {Draft.Get("TpaTimeoutMinutes")} real minutes." : "Requests do not expire.") + "\n" +
                (Number("Teleportation.TpaWarmupSeconds") > 0 ? $"After acceptance, stand still for {Draft.Get("Teleportation.TpaWarmupSeconds")} real seconds." : "Teleport immediately after acceptance.");
        if (CurrentStepId.Contains("stuck", StringComparison.Ordinal))
            return "Use /stuck to return to spawn. No temporal gear is required.\n" +
                (Number("Teleportation.StuckWarmupSeconds") > 0 ? $"Stand still for {Draft.Get("Teleportation.StuckWarmupSeconds")} real seconds." : "Travel is immediate.");
        return CurrentStepId switch
        {
            "teleport.homes" => "Set a home with /sethome, then use /home to return.",
            "teleport.spawn" => "Use /spawn to return to the world's spawn point.",
            "teleport.top" => "Use /top to find a safe place above you.",
            "teleport.back" => "Use /back to return to the place you last teleported from.",
            "teleport.warmup" => "Moving cancels the wait before a teleport.",
            _ => ""
        };
    }

    private double Time => _fixedTime ?? _elapsed;
    private int Sleepers => (int)(Time / 2) % 4;
    private string SleepCountText() => $"{Sleepers} of 4 players are asleep.";
    private bool NeedsRestart => _runtimeApplyFailed || _restartKeys.Count > 0;
    private bool CompactLayout => capi.Render.FrameHeight / Math.Max(RuntimeEnv.GUIScale, 0.1f) < 660;
    private double BodyHeight
    {
        get
        {
            if (CurrentStepId == "hub") return CompactLayout ? 488 : 546;
            var height = CompactLayout ? 528 : 608;
            if (!SetupWizardFeaturePreview.Supports(CurrentStepId) || CurrentStepId.StartsWith("appearance.", StringComparison.Ordinal))
                return height;
            SetupWizardCatalog.TryGetPage(CurrentStepId, out var page);
            var controls = page.SettingKeys.Count(SettingVisible);
            var editorBottom = 178 + Math.Max(0, controls - 1) * (CompactLayout ? 54 : 65) + 51;
            var contentBottom = Math.Max(174 + 220, editorBottom);
            return Math.Min(height, contentBottom + (CompactLayout ? 84 : 104));
        }
    }
    private double FooterY => BodyHeight - 38;
    private double StatusY => FooterY - (CurrentStepId == "hub" ? CompactLayout ? 48 : 64 : CompactLayout ? 36 : 56);
    private string AnimationCode() => CurrentStepId == "hub" ? "wave" : CurrentStepId == "finish" ? "cheer" : "idle";

    private static string PresentationName(string mode) => ProximityChatPresentationModes.Normalize(mode) switch
    {
        ProximityChatPresentationModes.SimpleSpeech => "Quoted chat",
        ProximityChatPresentationModes.PlainProximity => "Simple chat",
        ProximityChatPresentationModes.Prose => "Storytelling",
        _ => "Roleplay dialogue"
    };
    private static string PresentationDescription(string mode) => ProximityChatPresentationModes.Normalize(mode) switch
    {
        ProximityChatPresentationModes.SimpleSpeech => "Shows the speaker's name and speech in quotes.",
        ProximityChatPresentationModes.PlainProximity => "Shows the speaker's name and message without quotes.",
        ProximityChatPresentationModes.Prose => "Mix actions and dialogue in one message.",
        _ => "Adds a speech verb, such as \"says\", before the dialogue."
    };
    private static string SettingLabel(ConfigAdminSettingDefinition definition) => definition.Key switch
    {
        "DisableRPChat" => "Roleplay features",
        "SleepNotificationThreshold" => "Sleeping players needed (%)",
        _ => definition.Label
    };
    private static string DisplayValue(ConfigAdminSettingDefinition definition, string value)
    {
        if (definition.Key == "ProximityChatPresentationMode") return PresentationName(value);
        if (definition.Key == "SleepNotificationThreshold" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction))
            return (fraction * 100).ToString(CultureInfo.InvariantCulture) + "%";
        if (definition.Kind == ConfigAdminSettingKind.Boolean && ConfigAdminSettingDefinition.TryParseBool(value, out var enabled))
            return enabled != (definition.Key == "DisableRPChat") ? "On" : "Off";
        if (definition.Kind == ConfigAdminSettingKind.Select) return SettingOptionName(definition.Key, value, value);
        return value;
    }
    private static string SettingOptionName(string key, string value, string fallback) => (key, value) switch
    {
        ("OverheadChatBubbleMode", "RpText") => "Roleplay text",
        ("OverheadChatBubbleMode", "Vanilla") => "Game default",
        ("TypingIndicatorDisplayMode", "Icon") => "Icon only",
        ("TypingIndicatorDisplayMode", "Text") => "Text only",
        ("TypingIndicatorDisplayMode", "Both") => "Icon and text",
        ("ProximityChatTabPosition", "GameOrder") => "Game default",
        ("ProximityChatTabPosition", "AfterGeneral") => "After General",
        _ => fallback
    };
    private bool Bool(string key) => ConfigAdminSettingDefinition.TryParseBool(Draft.Get(key), out var value) && value;
    private double Number(string key) => double.TryParse(Draft.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : 0;

    private void DrawDiagram(Context context, ImageSurface surface, ElementBounds bounds)
    {
        context.Save();
        context.SetSourceRGBA(0.08, 0.11, 0.12, 0.8);
        context.Rectangle(0, 0, surface.Width, surface.Height);
        context.Fill();
        var scale = Math.Min(surface.Width / 169d, surface.Height / 168d);
        context.Translate((surface.Width - 169 * scale) / 2, 0);
        context.Scale(scale, scale);
        if (CurrentStepId.StartsWith("chat.", StringComparison.Ordinal)) DrawHearing(context);
        else if (CurrentStepId == "notifications.sleep")
        {
            var sleepers = Sleepers;
            for (var index = 0; index < 4; index++)
            {
                Person(context, 25 + index * 38, 68, index < sleepers);
                if (index < sleepers) { context.Rectangle(16 + index * 38, 101, 27, 12); context.Fill(); }
            }
        }
        else
        {
            var phase = (int)(Time / 1.5) % 4;
            for (var index = 0; index < 4; index++) Person(context, 22 + index * 42, 70, index == phase);
            context.SetSourceRGBA(0.8, 0.73, 0.45, 1);
            context.MoveTo(22, 115); context.LineTo(146, 115); context.Stroke();
        }
        context.Restore();
    }

    private void DrawHearing(Context context)
    {
        var modes = new[] { "Whisper", "Normal", "Yell" };
        var ranges = modes.Select(mode => Number("ProximityChatModeDistances." + mode)).ToArray();
        var onsets = modes.Select(mode => Number("ProximityChatModeObfuscationRanges." + mode)).ToArray();
        var obfuscation = Bool("EnableDistanceObfuscationSystem");
        var extent = Math.Max(1, ranges.Concat(obfuscation ? onsets : Array.Empty<double>()).Max());
        var pixelsPerBlock = 45 / extent;
        const double centerX = 84, centerY = 57;
        context.SetSourceRGBA(0.2, 0.25, 0.27, 1);
        for (var line = -3; line <= 3; line++)
        {
            context.MoveTo(28, centerY + line * 15); context.LineTo(140, centerY + line * 15);
            context.MoveTo(centerX + line * 15, 7); context.LineTo(centerX + line * 15, 107);
        }
        context.Stroke();
        context.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Normal);
        context.SetFontSize(10);
        for (var index = modes.Length - 1; index >= 0; index--)
        {
            context.SetSourceRGBA(index == 0 ? 0.6 : index == 1 ? 0.35 : 0.98,
                index == 0 ? 0.78 : index == 1 ? 0.85 : 0.7, index == 0 ? 1 : index == 1 ? 0.52 : 0.38, 1);
            context.LineWidth = 1.5;
            if (ranges[index] > 0)
            {
                var radius = Math.Max(0.8, (Math.Ceiling(ranges[index]) - 1) * pixelsPerBlock);
                context.MoveTo(centerX, centerY - radius);
                context.LineTo(centerX + radius, centerY);
                context.LineTo(centerX, centerY + radius);
                context.LineTo(centerX - radius, centerY);
                context.ClosePath(); context.Stroke();
            }
            var fades = obfuscation && ranges[index] > 0;
            if (fades)
            {
                context.SetDash(new[] { 3d, 3d }, index);
                context.Arc(centerX, centerY, Math.Max(0, onsets[index]) * pixelsPerBlock, 0, Math.PI * 2);
                context.Stroke(); context.SetDash(Array.Empty<double>(), 0);
            }
            var rangeLabel = ranges[index] == -1 ? "server-wide" : ranges[index] <= 0 ? "no listeners" : "up to " + (Math.Ceiling(ranges[index]) - 1).ToString("G3", CultureInfo.InvariantCulture) + " blocks";
            var onsetLabel = fades ? "; fade " + onsets[index].ToString("G3", CultureInfo.InvariantCulture) : "";
            context.MoveTo(4, 122 + index * 17);
            context.ShowText((index == 1 ? "Talk" : modes[index]) + ": " + rangeLabel + onsetLabel);
        }
        context.SetSourceRGBA(1, 1, 1, 1);
        context.Arc(centerX, centerY, 1.5, 0, Math.PI * 2); context.Fill();
    }

    private static void Person(Context context, double x, double y, bool active)
    {
        context.SetSourceRGBA(active ? 0.9 : 0.4, active ? 0.78 : 0.46, active ? 0.43 : 0.48, 1);
        context.Arc(x, y, 6, 0, Math.PI * 2); context.Fill();
        context.Rectangle(x - 6, y + 9, 12, 22); context.Fill();
    }

    public override void OnRenderGUI(float deltaTime)
    {
        _elapsed += deltaTime;
        var beat = (int)(Time / (CurrentStepId == "notifications.sleep" ? 2 : 1.5));
        if (beat != _lastBeat)
        {
            _lastBeat = beat;
            RefreshPreview();
        }
        base.OnRenderGUI(deltaTime);
    }

    private void Track(string action, string choice = null) => _onTrack?.Invoke(CurrentStepId, action, choice);
    private static void Text(GuiComposer composer, string text, double x, double y, double width, double height, bool bold = false)
    {
        var font = CairoFont.WhiteSmallText().WithFontSize(bold ? 17 : 14);
        if (bold) font.WithWeight(FontWeight.Bold);
        composer.AddStaticText(text ?? string.Empty, font, ElementBounds.Fixed(x, y, width, height));
    }
    private static void Button(GuiComposer composer, string label, Action action, double x, double y, double width, double height = 30)
        => composer.AddSmallButton(label, () => { action(); return true; }, ElementBounds.Fixed(x, y, width, height));

    public override bool TryClose()
    {
        (SingleComposer?.GetElement("pip") as PreviewActorElement)?.CancelDrag();
        if (!_forceClose && !_disposing && Draft.IsDirty && !IsCaptureOnly)
        {
            if (_closeConfirm?.IsOpened() != true)
            {
                _closeConfirm?.Dispose();
                _closeConfirm = new DiscardConfirmDialog(capi, _pending ? "Close while saving? Your save may still complete." : "Discard your unsaved changes?", discard =>
                {
                    if (!discard) return;
                    _forceClose = true;
                    try { TryClose(); }
                    finally { _forceClose = false; }
                });
                _closeConfirm.TryOpen();
            }
            return false;
        }
        var closed = base.TryClose();
        if (closed && !_closing && !_disposing)
        {
            _closing = true;
            Track("close");
            _onClose?.Invoke();
        }
        return closed;
    }

    public override void Dispose()
    {
        if (_disposing) return;
        _disposing = true;
        _closeConfirm?.TryClose();
        _closeConfirm?.Dispose();
        _closeConfirm = null;
        base.TryClose();
        try { _preview?.Dispose(); }
        finally { base.Dispose(); }
    }

    private sealed class DiscardConfirmDialog(ICoreClientAPI api, string text, Action<bool> onChoice)
        : GuiDialogConfirm(api, text, onChoice)
    {
        public override bool UnregisterOnClose => true;
    }

    private sealed class PreviewActorElement : GuiElement
    {
        private readonly SetupWizardDialog _dialog;
        private bool _dragging;
        public PreviewActorElement(ICoreClientAPI api, ElementBounds bounds, SetupWizardDialog dialog) : base(api, bounds) => _dialog = dialog;
        public override void ComposeElements(Context context, ImageSurface surface) => Bounds.CalcWorldBounds();
        public override void RenderInteractiveElements(float deltaTime)
        {
            _dialog._preview.Render(deltaTime, Bounds);
        }
        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Left || !Bounds.PointInside(args.X, args.Y)) return;
            _dragging = true;
            _dialog._preview.BeginDrag(args.X);
            args.Handled = true;
        }
        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            if (!_dragging) return;
            _dialog._preview.DragTo(args.X);
            args.Handled = true;
        }
        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            if (!_dragging || args.Button != EnumMouseButton.Left) return;
            _dragging = false;
            _dialog._preview.EndDrag();
            args.Handled = true;
        }
        public override void Dispose()
        {
            CancelDrag();
            base.Dispose();
        }
        public void CancelDrag()
        {
            _dragging = false;
            _dialog._preview.EndDrag(false);
        }
    }
}
