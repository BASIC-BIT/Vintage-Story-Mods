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
            if (!layoutOnly) _preview = new SetupGuidePreview(api);
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
    public bool IsPreviewReady => LayoutOnly || _preview?.IsReady == true;
    public override string ToggleKeyCombinationCode => null;
    public override bool UnregisterOnClose => true;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    public void ShowPage(string stepId)
    {
        if (stepId != "hub" && stepId != "review" && stepId != "finish" && !SetupWizardCatalog.TryGetPage(stepId, out _))
            throw new ArgumentException("Unknown setup page.", nameof(stepId));
        CurrentStepId = stepId;
        _preview?.EndDrag(false);
        _chatPreviewTab = Bool("UseGeneralChannelAsProximityChat") || CurrentStepId == "chat.tabs" && !Bool("ProximityChatAsDefault") ? 0 : 1;
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
            ShowPage("finish");
            Track("completed");
        }
        else
        {
            if (result.Success) _message = (_runtimeApplyFailed ? result.Message + " " : "Saved. ") + "Changes made while saving are still in your draft.";
            ShowPage("review");
        }
    }

    public void SetRequestFailure(string message)
    {
        Draft.CancelRequest();
        _pending = false;
        _message = message ?? "The request failed. Your changes are kept.";
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
        var body = ElementBounds.Fixed(0, 0, Width, CompactLayout ? 528 : 608).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = capi.Gui.CreateCompo("thebasics-setup-wizard", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle));
        var composer = SingleComposer
            .AddShadedDialogBG(body)
            .AddDialogTitleBar(IsCaptureOnly ? "The BASICs setup (QA preview, save disabled)" : "The BASICs setup", () => TryClose())
            .BeginChildElements(body);
        var destinations = new[] { "hub", "chat.basics", "teleport.tools", "notifications.savestart", "review" };
        var tabs = new[] { "Topics", "Chat", "Teleportation", "Notifications", "Review" }
            .Select((name, index) => new GuiTab { Name = name, DataInt = index }).ToArray();
        composer.AddHorizontalTabs(tabs, ElementBounds.Fixed(0, 40, Width, 32), index => ShowPage(destinations[index]),
            CairoFont.WhiteSmallText().WithFontSize(17), CairoFont.WhiteSmallText().WithFontSize(17).WithColor(GuiStyle.ActiveButtonTextColor), "topic-tabs");
        var selectedTopic = CurrentStepId == "hub" ? 0 : CurrentStepId.StartsWith("chat.", StringComparison.Ordinal) ? 1
            : CurrentStepId.StartsWith("teleport.", StringComparison.Ordinal) ? 2 : CurrentStepId.StartsWith("notifications.", StringComparison.Ordinal) ? 3 : 4;
        composer.GetHorizontalTabs("topic-tabs").SetValue(selectedTopic, false);
        composer.AddInset(ElementBounds.Fixed(0, FooterY - 8, Width, 46), 1);

        if (CurrentStepId == "hub") ComposeHub(composer);
        else if (CurrentStepId == "review") ComposeReview(composer);
        else if (CurrentStepId == "finish") ComposeFinish(composer);
        else ComposePage(composer);

        Text(composer, _message ?? (_pending ? "Waiting for the server to acknowledge the save..." : "Changes stay in this draft until you save."), 0, StatusY, Width, CompactLayout ? 30 : 42);
        Button(composer, "Close", () => TryClose(), Width - 110, FooterY, 110);
        composer.EndChildElements().Compose(focusFirstElement: false);
        if (_diagram != null) _sample = SingleComposer.GetRichtext("preview-text");
    }

    private void ComposeHub(GuiComposer composer)
    {
        Text(composer, "A few choices, with previews of your draft", 0, 92, Width, 30, true);
        Text(composer, "Choose a topic. Back and skipping keep your edits. The advanced editor remains available.", 0, 129, Width, 48);
        Button(composer, "Chat", () => ShowPage("chat.basics"), 0, 205, 270, 52);
        Button(composer, "Teleportation", () => ShowPage("teleport.tools"), 305, 205, 270, 52);
        Button(composer, "Notifications", () => ShowPage("notifications.savestart"), 610, 205, 270, 52);
        Text(composer, "Chat\nPresentation, languages, hearing ranges and tabs.", 12, 275, 248, 80);
        Text(composer, "Teleportation\nChoose BASICs commands, then their costs and timing.", 317, 275, 248, 80);
        Text(composer, "Notifications\nSave announcements and sleep reminders.", 622, 275, 248, 80);
        Text(composer, $"{Draft.ChangedKeys.Count} changes ready to review." + (NeedsRestart ? " Some saved changes still need a server restart." : ""), 0, CompactLayout ? 358 : 397, Width, 40);
        Button(composer, "Review changes", () => ShowPage("review"), 0, CompactLayout ? 403 : 453, 240);
        if (_onAdvanced != null)
            Button(composer, "Advanced editor", () => { Track("advanced"); Suspend(); _onAdvanced(); }, 260, CompactLayout ? 403 : 453, 240);
    }

    private void ComposePage(GuiComposer composer)
    {
        SetupWizardCatalog.TryGetPage(CurrentStepId, out var page);
        Text(composer, page.Title, 0, 88, Width, 27, true);
        Text(composer, page.Description, 0, 120, Width, 44);
        AddPreview(composer);
        var y = 178d;
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
        Button(composer, "Skip topic", () => { Track("skip"); ShowPage("hub"); }, 615, FooterY, 132);
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
        var label = key == "DisableRPChat" ? "RP features" : definition.Label;
        if (key == "SleepNotificationThreshold") label = "Sleeping players needed (%)";
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
                composer.AddInteractiveElement(new GuiElementDescribedDropDown(capi, options, options.Select(PresentationName).ToArray(),
                    options.Select(PresentationDescription).ToArray(), selected,
                    (choice, _) => Changed(key, choice, "option" + Array.IndexOf(options, choice), true), bounds, CairoFont.WhiteSmallText()), key);
                Text(composer, PresentationDescription(value), 334, y + 55, 546, 20);
                y += 18;
            }
            else
                composer.AddInteractiveElement(new GuiElementDropDown(capi, options, definition.OptionNames.ToArray(), selected,
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
            composer.AddHoverText(definition.Description + (definition.ReloadBehavior == ConfigAdminReloadBehavior.RestartRequired
                ? "\nThis change takes effect after the server restarts." : ""), CairoFont.WhiteSmallText(), 400, bounds.FlatCopy(), "help-" + key);
        y += CurrentStepId == "chat.basics" ? (CompactLayout ? 50 : 61) : CompactLayout ? 54 : 65;
    }

    private void AddAnnouncement(GuiComposer composer, ref double y)
    {
        var started = CurrentStepId == "notifications.savestart";
        var enabled = started ? "SendServerSaveAnnouncement" : "SendServerSaveFinishedAnnouncement";
        var popup = started ? "ServerSaveAnnouncementAsNotification" : "ServerSaveFinishedAsNotification";
        var text = started ? "TEXT_ServerSaveAnnouncement" : "TEXT_ServerSaveFinished";
        Text(composer, "Announcement", 334, y, 546, 21);
        composer.AddInteractiveElement(new GuiElementDropDown(capi, new[] { "off", "chat", "popup" }, new[] { "Off", "Chat", "Popup" },
            !Bool(enabled) ? 0 : Bool(popup) ? 2 : 1, (choice, _) =>
            {
                Draft.Set(enabled, choice == "off" ? "0" : "1");
                if (choice != "off") Draft.Set(popup, choice == "popup" ? "1" : "0");
                Track("choice", choice);
                RefreshPreview();
            }, ElementBounds.Fixed(334, y + 23, 546, 28), CairoFont.WhiteSmallText(), false), "announcement-mode");
        y += CompactLayout ? 54 : 65;
        AddSetting(composer, text, ref y);
        Text(composer, "This controls the announcement. Saving and save pauses continue when it is Off.", 334, y + 12, 546, 75);
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
        Text(composer, "Check your changes before saving. Settings marked After restart take effect when the server restarts.", 0, 125, Width, 42);
        var keys = Draft.ChangedKeys;
        var y = 180d;
        foreach (var key in keys.Skip(_reviewOffset).Take(5))
        {
            ConfigAdminSettingRegistry.TryGet(key, out var definition);
            Text(composer, (key == "DisableRPChat" ? "RP features" : definition.Label) + (definition.ReloadBehavior == ConfigAdminReloadBehavior.Live ? "" : " | After restart"), 0, y, Width, 20, true);
            Text(composer, $"{DisplayValue(definition, Draft.GetOriginal(key))}  >  {DisplayValue(definition, Draft.Get(key))}", 0, y + 22, Width, 36);
            y += CompactLayout ? 54 : 64;
        }
        if (keys.Count == 0) Text(composer, "No settings changed. Save to confirm the current pending restart state.", 0, y, Width, 60);
        if (keys.Count > 5)
            Button(composer, $"More changes ({_reviewOffset + 1}-{Math.Min(_reviewOffset + 5, keys.Count)} of {keys.Count})", () =>
            { _reviewOffset = _reviewOffset + 5 < keys.Count ? _reviewOffset + 5 : 0; ComposeDialog(); }, 335, FooterY, 420);
        Button(composer, "Back to topics", () => { Track("back"); ShowPage("hub"); }, 0, FooterY, 150);
        if (!IsCaptureOnly)
            Button(composer, _pending ? "Saving..." : "Save settings", () =>
            {
                if (_pending) return;
                _pending = true;
                _message = "Waiting for the server to acknowledge the save...";
                ComposeDialog();
                _onSave?.Invoke();
            }, 160, FooterY, 160);
    }

    private void ComposeFinish(GuiComposer composer)
    {
        Text(composer, IsCaptureOnly ? "QA example: saved settings" : "Settings saved", 0, 90, Width, 28, true);
        if (!NeedsRestart)
            Text(composer, "Your changes are active. You can return to the game.", 0, 145, Width, 80);
        else
        {
            Text(composer, "Saved settings awaiting restart:", 0, 143, Width, 27, true);
            var names = _restartKeys.Select(key => ConfigAdminSettingRegistry.TryGet(key, out var setting) ? setting.Label : key);
            Text(composer, _runtimeApplyFailed ? "Live application failed. Restart to apply the saved settings.\n" + string.Join(", ", names) : string.Join(", ", names), 0, 179, Width, 126);
            Text(composer, RestartInstructions(), 0, 330, Width, 90);
            Button(composer, "How to restart", () => { _message = RestartInstructions(); ComposeDialog(); }, 0, CompactLayout ? 411 : 455, 210);
        }
        Button(composer, "Back to setup", () => ShowPage("hub"), 0, FooterY, 180);
        Button(composer, NeedsRestart ? "Later" : "Done", () => TryClose(), 195, FooterY, 150);
    }

    private string RestartInstructions() => _isDedicated
        ? "Restart the server from your hosting panel, or stop and start it using your normal launch method. Players must reconnect."
        : "Save and quit to the main menu, then reopen this world.";

    private void AddPreview(GuiComposer composer)
    {
        var chat = CurrentStepId.StartsWith("chat.", StringComparison.Ordinal);
        var pictureHeight = chat ? CompactLayout ? 107 : 140 : CompactLayout ? 146 : 168;
        composer.AddInset(ElementBounds.Fixed(0, 174, 310, CompactLayout ? 274 : 330), 3);
        if (LayoutOnly)
            Text(composer, "Layout only\nNative 3D Pip omitted", 8, 190, 122, 160);
        else
            composer.AddInteractiveElement(new PreviewActorElement(capi, ElementBounds.Fixed(4, 184, 120, pictureHeight), this), "pip");
        _diagram = new GuiElementCustomDraw(capi, ElementBounds.Fixed(132, 184, 169, pictureHeight), DrawDiagram, true);
        composer.AddInteractiveElement(_diagram, "preview-diagram");
        if (chat)
        {
            Text(composer, "Drag Pip to turn", 4, CompactLayout ? 294 : 329, 125, 16);
            AddChatPreview(composer);
        }
        else
            composer.AddRichtext(BuildPreviewText(), CairoFont.WhiteSmallText().WithFontSize(14), ElementBounds.Fixed(10, CompactLayout ? 338 : 366, 289, CompactLayout ? 103 : 129), "preview-text");
    }

    private void AddChatPreview(GuiComposer composer)
    {
        const double historyWidth = 288;
        var y = CompactLayout ? 314 : 347;
        var historyHeight = CompactLayout ? 72 : 92;
        var bounds = ElementBounds.Fixed(0, y, historyWidth + 22, historyHeight + 57);
        composer.BeginChildElements(bounds)
            .AddGameOverlay(ElementBounds.Fixed(0, 23, historyWidth + 22, historyHeight + 34), new[] { 0.25, 0.208, 0.161, 0.75 });
        var tabs = Bool("UseGeneralChannelAsProximityChat")
            ? new[] { new GuiTab { Name = "General", DataInt = 0 } }
            : new[] { new GuiTab { Name = "General", DataInt = 0 }, new GuiTab { Name = "Proximity", DataInt = 1 } };
        var font = CairoFont.WhiteDetailText().WithFontSize(14);
        composer.AddHorizontalTabs(tabs, ElementBounds.Fixed(0, 0, historyWidth + 22, 23), index =>
        { _chatPreviewTab = index; RefreshPreview(); }, font, font.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "preview-chat-tabs");
        composer.GetHorizontalTabs("preview-chat-tabs").SetValue(Bool("UseGeneralChannelAsProximityChat") ? 0 : _chatPreviewTab, false);
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
        input.SetValue("Example chat");
        composer.AddInteractiveElement(input, "preview-chat-input").EndChildElements();
    }

    private void RefreshPreview()
    {
        _diagram?.Redraw();
        _sample?.SetNewText(BuildPreviewText(), CairoFont.WhiteSmallText().WithFontSize(14), null);
        if (CurrentStepId.StartsWith("chat.", StringComparison.Ordinal))
        {
            var scroll = SingleComposer.GetCompactScrollbar("preview-chat-scroll");
            if (scroll != null && _sample != null)
                scroll.SetHeights(CompactLayout ? 72 : 92, (float)(_sample.Bounds.fixedHeight));
        }
    }

    private string BuildPreviewText()
    {
        if (CurrentStepId.StartsWith("chat.", StringComparison.Ordinal))
        {
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
                var sleepers = ((int)(Time / 2) % 2) + 1;
                var triggers = Bool("EnableSleepNotifications") && threshold > 0 && threshold < 1 && (threshold <= 0.25 ? sleepers == 1 : threshold <= 0.5 && sleepers == 2);
                return VtmlUtils.EscapeVtml($"{sleepers}/4 sleeping. " + (triggers ? Draft.Get("TEXT_SleepNotification") : "No reminder at this beat.") + "\nThis reminder does not change night skipping. 0% and 100% send no reminder.");
            }
            var start = CurrentStepId == "notifications.savestart";
            var enabled = Bool(start ? "SendServerSaveAnnouncement" : "SendServerSaveFinishedAnnouncement");
            var popup = Bool(start ? "ServerSaveAnnouncementAsNotification" : "ServerSaveFinishedAsNotification");
            var wording = Draft.Get(start ? "TEXT_ServerSaveAnnouncement" : "TEXT_ServerSaveFinished");
            return VtmlUtils.EscapeVtml(enabled ? (popup ? "Popup: " : "Chat: ") + wording : "Announcement off. Server saving continues.");
        }
        return VtmlUtils.EscapeVtml(TeleportExplanation());
    }

    private string TeleportExplanation()
    {
        if (CurrentStepId.Contains("request", StringComparison.Ordinal))
            return "Request > accept > stand still > arrive.\n" +
                (Bool("TpaRequireTemporalGear") ? "TPA gear is charged when submitting." : "TPA needs no gear.") +
                (Bool("TpaUseCooldown") ? $" Cooldown: {Draft.Get("TpaCooldownInGameHours")} in-game hours." : " Cooldown off.") +
                (Bool("TpaUseTimeout") ? $" Timeout: {Draft.Get("TpaTimeoutMinutes")} real minutes." : " Timeout off.") +
                $" Warmup: {Draft.Get("Teleportation.TpaWarmupSeconds")} real seconds.";
        if (CurrentStepId.Contains("stuck", StringComparison.Ordinal))
            return "Emergency return to spawn. Stuck is gear-free, with separate staff-online rules and notices. Stand still, then arrive.";
        return "Choose BASICs commands independently. Other mods keep their own commands.\nIf a gear is required, home, spawn, top and back charge it only after a successful teleport. Warmup and cooldown use real seconds.";
    }

    private double Time => _fixedTime ?? _elapsed;
    private bool NeedsRestart => _runtimeApplyFailed || _restartKeys.Count > 0;
    private bool CompactLayout => capi.Render.FrameHeight / Math.Max(RuntimeEnv.GUIScale, 0.1f) < 660;
    private double FooterY => CompactLayout ? 490 : 570;
    private double StatusY => CompactLayout ? 454 : 514;
    private string AnimationCode() => CurrentStepId == "finish" ? "cheer" : CurrentStepId.StartsWith("chat.", StringComparison.Ordinal) ? "wave" : "idle";

    private static string PresentationName(string mode) => ProximityChatPresentationModes.Normalize(mode) switch
    {
        ProximityChatPresentationModes.SimpleSpeech => "Quoted chat",
        ProximityChatPresentationModes.PlainProximity => "Simple chat",
        ProximityChatPresentationModes.Prose => "Storytelling",
        _ => "Roleplay dialogue"
    };
    private static string PresentationDescription(string mode) => ProximityChatPresentationModes.Normalize(mode) switch
    {
        ProximityChatPresentationModes.SimpleSpeech => "Names and quoted speech.",
        ProximityChatPresentationModes.PlainProximity => "Names and unquoted speech.",
        ProximityChatPresentationModes.Prose => "Mix actions and quoted speech in one line.",
        _ => "Names, speech verbs and quoted speech."
    };
    private static string DisplayValue(ConfigAdminSettingDefinition definition, string value)
    {
        if (definition.Key == "ProximityChatPresentationMode") return PresentationName(value);
        if (definition.Kind == ConfigAdminSettingKind.Boolean && ConfigAdminSettingDefinition.TryParseBool(value, out var enabled))
            return enabled != (definition.Key == "DisableRPChat") ? "On" : "Off";
        return value;
    }
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
            var sleepers = ((int)(Time / 2) % 2) + 1;
            for (var index = 0; index < 4; index++)
            {
                Person(context, 25 + index * 38, 68, index < sleepers);
                if (index < sleepers) { context.Rectangle(16 + index * 38, 101, 27, 12); context.Fill(); }
            }
        }
        else if (CurrentStepId.StartsWith("notifications.", StringComparison.Ordinal))
        {
            var start = CurrentStepId == "notifications.savestart";
            if (Bool(start ? "SendServerSaveAnnouncement" : "SendServerSaveFinishedAnnouncement"))
            {
                var popup = Bool(start ? "ServerSaveAnnouncementAsNotification" : "ServerSaveFinishedAsNotification");
                context.SetSourceRGBA(0.8, 0.73, 0.45, 1);
                context.Rectangle(17, popup ? 22 : 98, 136, 40); context.Stroke();
                for (var line = 0; line < 3; line++) { context.MoveTo(29, (popup ? 34 : 110) + line * 8); context.LineTo(139 - line * 15, (popup ? 34 : 110) + line * 8); context.Stroke(); }
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
        if (CurrentStepId is "chat.ranges" or "chat.obfuscation")
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
                if (obfuscation)
                {
                    context.SetDash(new[] { 3d, 3d }, index);
                    context.Arc(centerX, centerY, Math.Max(0, onsets[index]) * pixelsPerBlock, 0, Math.PI * 2);
                    context.Stroke(); context.SetDash(Array.Empty<double>(), 0);
                }
                var rangeLabel = ranges[index] == -1 ? "global" : ranges[index] <= 0 ? "no listeners" : ranges[index].ToString("G3", CultureInfo.InvariantCulture) + " blocks";
                var onsetLabel = obfuscation ? "; fade " + onsets[index].ToString("G3", CultureInfo.InvariantCulture) : "";
                context.MoveTo(4, 122 + index * 17);
                context.ShowText(modes[index] + ": " + rangeLabel + onsetLabel);
            }
            context.SetSourceRGBA(1, 1, 1, 1);
            context.Arc(centerX, centerY, 1.5, 0, Math.PI * 2); context.Fill();
            return;
        }
        var range = Number("ProximityChatModeDistances.Normal");
        var onset = Number("ProximityChatModeObfuscationRanges.Normal");
        const double center = 84;
        for (var x = -5; x <= 5; x++)
            for (var z = -5; z <= 5; z++)
            {
                var blocks = Math.Abs(x * 7) + Math.Abs(z * 7);
                var hearing = range == -1 || blocks < range;
                context.SetSourceRGBA(hearing ? 0.35 : 0.17, hearing ? 0.65 : 0.2, hearing ? 0.5 : 0.23, 0.9);
                context.Rectangle(center + x * 13 - 5, center + z * 13 - 5, 10, 10); context.Fill();
            }
        if (Bool("EnableDistanceObfuscationSystem"))
        {
            context.SetSourceRGBA(0.9, 0.65, 0.24, 1);
            context.Arc(center, center, Math.Min(75, onset / 7 * 13), 0, Math.PI * 2); context.Stroke();
        }
        Person(context, center, center - 8, true);
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
        var beat = (int)(Time / 1.5);
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
                _closeConfirm = new DiscardConfirmDialog(capi, "Discard your unsaved setup changes?", discard =>
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
