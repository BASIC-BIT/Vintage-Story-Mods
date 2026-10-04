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

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class SetupWizardDialog : GuiDialog
{
    private const double Width = 880;
    private readonly Action _onSave;
    private readonly Action<string, string, string> _onTrack;
    private readonly Action _onClose;
    private readonly Action _onAdvanced;
    private readonly SetupGuidePreview _preview;
    private readonly bool _isDedicated;
    private GuiElementCustomDraw _diagram;
    private GuiElementRichtext _sample;
    private GuiDialogConfirm _closeConfirm;
    private IReadOnlyList<string> _restartKeys = Array.Empty<string>();
    private string _message;
    private bool _pending;
    private bool _disposing;
    private bool _forceClose;
    private bool _closing;
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
        if (!layoutOnly) _preview = new SetupGuidePreview(api);
        ComposeDialog();
    }

    public SetupWizardDraft Draft { get; }
    public string RunId { get; private set; }
    public string CurrentStepId { get; private set; } = "hub";
    public bool LayoutOnly { get; }
    public bool IsCaptureOnly { get; }
    public bool IsPreviewReady => LayoutOnly || _preview?.IsReady == true;
    public override string ToggleKeyCombinationCode => null;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    public void ShowPage(string stepId)
    {
        if (stepId != "hub" && stepId != "review" && stepId != "finish" && !SetupWizardCatalog.TryGetPage(stepId, out _))
            throw new ArgumentException("Unknown setup page.", nameof(stepId));
        CurrentStepId = stepId;
        _reviewOffset = 0;
        _elapsed = 0;
        _lastBeat = -1;
        ComposeDialog();
        Track("viewed");
    }

    public void SetPreviewTime(double timeSeconds)
    {
        if (!double.IsFinite(timeSeconds) || timeSeconds < 0) throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        _fixedTime = timeSeconds;
        _preview?.SetAnimation(AnimationCode(), timeSeconds);
        RefreshPreview();
    }

    public void SetSession(TheBasicsSetupWizardResultMessage result)
    {
        RunId = result.RunId;
        Draft.RefreshValues(result.Values);
        _restartKeys = result.RestartRequiredKeys?.ToArray() ?? Array.Empty<string>();
        ComposeDialog();
    }

    public void SetResult(TheBasicsSetupWizardResultMessage result)
    {
        if (!Draft.MergeSaveResult(result)) return;
        _pending = false;
        _message = result.Message;
        _restartKeys = result.RestartRequiredKeys?.ToArray() ?? Array.Empty<string>();
        if (result.Success && !Draft.IsDirty)
        {
            ShowPage("finish");
            Track("completed");
        }
        else
        {
            if (result.Success) _message = "Saved. Changes made while saving are still in your draft.";
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
        _forceClose = true;
        try { base.TryClose(); }
        finally { _forceClose = false; }
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();
        _diagram = null;
        _sample = null;
        var body = ElementBounds.Fixed(0, 0, Width, 608).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var composer = capi.Gui.CreateCompo("thebasics-setup-wizard", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(body)
            .AddDialogTitleBar(IsCaptureOnly ? "The BASICs setup (QA preview, save disabled)" : "The BASICs setup", () => TryClose())
            .BeginChildElements(body);
        Button(composer, "Topics", () => ShowPage("hub"), 0, 40, 105);
        Button(composer, "Chat", () => ShowPage("chat.basics"), 115, 40, 160);
        Button(composer, "Teleportation", () => ShowPage("teleport.tools"), 285, 40, 180);
        Button(composer, "Notifications", () => ShowPage("notifications.savestart"), 475, 40, 180);
        Button(composer, "Review", () => ShowPage("review"), 665, 40, 215);

        if (CurrentStepId == "hub") ComposeHub(composer);
        else if (CurrentStepId == "review") ComposeReview(composer);
        else if (CurrentStepId == "finish") ComposeFinish(composer);
        else ComposePage(composer);

        Text(composer, _message ?? (_pending ? "Waiting for the server to acknowledge the save..." : "Changes stay in this draft until you save."), 0, 514, Width, 42);
        Button(composer, "Close", () => TryClose(), Width - 110, 570, 110);
        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
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
        Text(composer, $"{Draft.ChangedKeys.Count} setting(s) changed. {_restartKeys.Count} saved setting(s) await restart.", 0, 397, Width, 40);
        Button(composer, "Review changes", () => ShowPage("review"), 0, 453, 240);
        if (_onAdvanced != null)
            Button(composer, "Advanced editor", () => { Track("advanced"); Suspend(); _onAdvanced(); }, 260, 453, 240);
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
        if (index > 0) Button(composer, "Back", () => { Track("back"); ShowPage(pages[index - 1].Id); }, 0, 570, 110);
        else Button(composer, "Back", () => { Track("back"); ShowPage("hub"); }, 0, 570, 110);
        var next = index + 1 < pages.Count ? pages[index + 1].Id : "hub";
        Button(composer, index + 1 < pages.Count ? "Next" : "Topics", () => ShowPage(next), 125, 570, 135);
        Button(composer, "Skip topic", () => { Track("skip"); ShowPage("hub"); }, 275, 570, 145);
        var codes = pages.Select(candidate => candidate.Id).ToArray();
        composer.AddInteractiveElement(new GuiElementDropDown(capi, codes, pages.Select(candidate => candidate.Title).ToArray(),
            Math.Max(0, index), (value, _) => ShowPage(value), ElementBounds.Fixed(440, 570, 305, 30), CairoFont.WhiteSmallText(), false), "page-picker");
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
        var label = definition.Label + (definition.ReloadBehavior == ConfigAdminReloadBehavior.RestartRequired ? " (restart)" : "");
        if (key == "DisableRPChat") label = "RP features (restart)";
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
            composer.AddInteractiveElement(new GuiElementDropDown(capi, options, definition.OptionNames.ToArray(),
                Math.Max(0, Array.FindIndex(options, candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase))),
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
        composer.AddHoverText(definition.Description, CairoFont.WhiteSmallText(), 400, bounds.FlatCopy(), "help-" + key);
        y += 65;
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
        y += 65;
        AddSetting(composer, text, ref y);
        Text(composer, "This controls the announcement. Saving and save pauses continue when it is Off.", 334, y + 12, 546, 75);
    }

    private void Changed(string key, string value, string choice, bool recompose)
    {
        if (Draft.Get(key) == value) return;
        Draft.Set(key, value);
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
        Text(composer, "Only changed settings are sent. The server checks your original values before saving.", 0, 125, Width, 42);
        var keys = Draft.ChangedKeys;
        var y = 180d;
        foreach (var key in keys.Skip(_reviewOffset).Take(5))
        {
            ConfigAdminSettingRegistry.TryGet(key, out var definition);
            Text(composer, definition.Label + (definition.ReloadBehavior == ConfigAdminReloadBehavior.Live ? " (applies live)" : " (restart required)"), 0, y, Width, 20, true);
            Text(composer, $"{Draft.GetOriginal(key)}  >  {Draft.Get(key)}", 0, y + 22, Width, 36);
            y += 64;
        }
        if (keys.Count == 0) Text(composer, "No settings changed. Save to confirm the current pending restart state.", 0, y, Width, 60);
        if (keys.Count > 5)
            Button(composer, $"More changes ({_reviewOffset + 1}-{Math.Min(_reviewOffset + 5, keys.Count)} of {keys.Count})", () =>
            { _reviewOffset = _reviewOffset + 5 < keys.Count ? _reviewOffset + 5 : 0; ComposeDialog(); }, 335, 570, 420);
        Button(composer, "Back to topics", () => { Track("back"); ShowPage("hub"); }, 0, 570, 150);
        if (!IsCaptureOnly)
            Button(composer, _pending ? "Saving..." : "Save settings", () =>
            {
                if (_pending) return;
                _pending = true;
                _message = "Waiting for the server to acknowledge the save...";
                ComposeDialog();
                _onSave?.Invoke();
            }, 160, 570, 160);
    }

    private void ComposeFinish(GuiComposer composer)
    {
        Text(composer, IsCaptureOnly ? "QA example: saved settings" : "Settings saved", 0, 90, Width, 28, true);
        if (_restartKeys.Count == 0)
            Text(composer, "Your changes are active. You can return to the game.", 0, 145, Width, 80);
        else
        {
            Text(composer, "Saved settings awaiting restart:", 0, 143, Width, 27, true);
            var names = _restartKeys.Select(key => ConfigAdminSettingRegistry.TryGet(key, out var setting) ? setting.Label : key);
            Text(composer, string.Join(", ", names), 0, 179, Width, 126);
            Text(composer, RestartInstructions(), 0, 330, Width, 90);
            Button(composer, "How to restart", () => { _message = RestartInstructions(); ComposeDialog(); }, 0, 455, 210);
        }
        Button(composer, "Back to setup", () => ShowPage("hub"), 0, 570, 180);
        Button(composer, _restartKeys.Count > 0 ? "Later" : "Done", () => TryClose(), 195, 570, 150);
    }

    private string RestartInstructions() => _isDedicated
        ? "Restart the server from your hosting panel, or stop and start it using your normal launch method. Players must reconnect."
        : "Save and quit to the main menu, then reopen this world.";

    private void AddPreview(GuiComposer composer)
    {
        composer.AddInset(ElementBounds.Fixed(0, 174, 310, 330), 3);
        if (LayoutOnly)
            Text(composer, "Layout only\nNative 3D Pip omitted", 8, 190, 122, 160);
        else
            composer.AddInteractiveElement(new PreviewActorElement(capi, ElementBounds.Fixed(4, 184, 120, 168), this), "pip");
        _diagram = new GuiElementCustomDraw(capi, ElementBounds.Fixed(132, 184, 169, 168), DrawDiagram, true);
        composer.AddInteractiveElement(_diagram, "preview-diagram");
        composer.AddRichtext(BuildPreviewText(), CairoFont.WhiteSmallText().WithFontSize(14), ElementBounds.Fixed(10, 366, 289, 129), "preview-text");
    }

    private void RefreshPreview()
    {
        _diagram?.Redraw();
        _sample?.SetNewText(BuildPreviewText(), CairoFont.WhiteSmallText().WithFontSize(14), null);
    }

    private string BuildPreviewText()
    {
        if (CurrentStepId.StartsWith("chat.", StringComparison.Ordinal))
        {
            var replacement = Bool("UseGeneralChannelAsProximityChat");
            var tabs = replacement ? "General = proximity" : "General = global | Proximity = nearby";
            var mode = Draft.Get("ProximityChatPresentationMode");
            var config = new ModConfig();
            var words = "Hello, traveler!";
            var quoted = ProximityChatPresentationModes.UsesSpeechQuotes(mode) ? ChatHelper.WrapSpeechQuotes(words, null, config, false) : words;
            var example = mode switch
            {
                "SimpleSpeech" or "PlainProximity" => "Pip: " + quoted,
                "Prose" => ChatHelper.FormatProseMessage("Pip waves. \"Hello, traveler!\"", null, config, false, text => text),
                _ => "Pip says " + quoted
            };
            if (Bool("DisableRPChat")) example = "Pip: Hello, traveler!";
            return VtmlUtils.EscapeVtml(tabs) + "<br>" + example + "<br>" + VtmlUtils.EscapeVtml(ChatExplanation());
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

    private string ChatExplanation() => CurrentStepId switch
    {
        "chat.basics" => Bool("DisableRPChat") ? "RP formatting is off; proximity delivery remains. This is not a vanilla-chat switch." : "Presentation changes text; it does not disable hearing ranges.",
        "chat.language" => (Bool("EnableLanguageSystem") && !Bool("DisableRPChat") ? "Languages on. " : "Languages inactive. ") + "(local OOC) stays nearby. " + (Bool("EnableGlobalOOC") && !Bool("DisableRPChat") ? "((global OOC)) reaches everyone." : "Global OOC is inactive.") + " Sticky OOC is a separate permission.",
        "chat.ranges" => "Integer-block Manhattan distance must be less than range. Exactly -1 is server-wide speech.",
        "chat.obfuscation" => "Diamond: delivery. Circle: straight-line obfuscation onset. Their distances differ.",
        _ => "Players using /rptext off bypass chat-tab filtering. The draft controls the default experience."
    };

    private string TeleportExplanation()
    {
        if (CurrentStepId.Contains("request", StringComparison.Ordinal))
            return $"Request > accept > stand still > arrive.\nTPA gear is charged when submitting. Cooldown: {Draft.Get("TpaCooldownInGameHours")} in-game hours. Timeout: {Draft.Get("TpaTimeoutMinutes")} real minutes. Warmup: {Draft.Get("Teleportation.TpaWarmupSeconds")} real seconds.";
        if (CurrentStepId.Contains("stuck", StringComparison.Ordinal))
            return "Emergency return to spawn. Stuck is gear-free, with separate staff-online rules and notices. Stand still, then arrive.";
        return "Choose BASICs commands independently. Other mods keep their own commands.\nHome, spawn, top and back charge their gear only after a successful teleport. Warmup and cooldown use real seconds.";
    }

    private double Time => _fixedTime ?? _elapsed;
    private string AnimationCode() => CurrentStepId == "finish" ? "cheer" : CurrentStepId.StartsWith("chat.", StringComparison.Ordinal) ? "wave" : "idle";
    private bool Bool(string key) => ConfigAdminSettingDefinition.TryParseBool(Draft.Get(key), out var value) && value;
    private double Number(string key) => double.TryParse(Draft.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : 0;

    private void DrawDiagram(Context context, ImageSurface surface, ElementBounds bounds)
    {
        context.Save();
        context.Translate(bounds.drawX, bounds.drawY);
        context.SetSourceRGBA(0.08, 0.11, 0.12, 0.8);
        context.Rectangle(0, 0, bounds.InnerWidth, bounds.InnerHeight);
        context.Fill();
        var scale = bounds.InnerWidth / 169;
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
        if (!_forceClose && !_disposing && Draft.IsDirty && !IsCaptureOnly)
        {
            if (_closeConfirm?.IsOpened() != true)
            {
                _closeConfirm = new GuiDialogConfirm(capi, "Discard your unsaved setup changes?", discard =>
                {
                    _closeConfirm = null;
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
        _closeConfirm?.Dispose();
        _closeConfirm = null;
        base.TryClose();
        _preview?.Dispose();
        base.Dispose();
    }

    private sealed class PreviewActorElement : GuiElement
    {
        private readonly SetupWizardDialog _dialog;
        public PreviewActorElement(ICoreClientAPI api, ElementBounds bounds, SetupWizardDialog dialog) : base(api, bounds) => _dialog = dialog;
        public override void ComposeElements(Context context, ImageSurface surface) => Bounds.CalcWorldBounds();
        public override void RenderInteractiveElements(float deltaTime)
        {
            _dialog._preview.SetAnimation(_dialog.AnimationCode(), _dialog.Time);
            _dialog._preview.Render(deltaTime, Bounds);
        }
    }
}
