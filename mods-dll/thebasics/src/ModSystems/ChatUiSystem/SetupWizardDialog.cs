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
    private const double SidebarWidth = 236;
    private const double ContentOffset = SidebarWidth + 24;
    private double Width => Math.Min(880, Math.Max(640,
        capi.Render.FrameWidth / Math.Max(RuntimeEnv.GUIScale, 0.1f) - ContentOffset - 2 * GuiStyle.ElementToDialogPadding - 24));
    private double DialogWidth => ContentOffset + Width;
    private readonly Action _onSave;
    private readonly Action<string, string, string> _onTrack;
    private readonly Action _onClose;
    private readonly Action _onAdvanced;
    private readonly Action<SetupWizardRequestKind> _onMaintenance;
    private readonly Action _onHelp;
    public bool SupportsMaintenance { get; private set; }
    private readonly ISetupGuidePreview _preview;
    private readonly System.Func<ICoreClientAPI, string, ISetupGuidePreview> _actorFactory;
    private readonly SetupWizardActorPool _actors;
    private readonly SetupWizardLanguageEditor _languageEditor;
    private readonly SetupWizardSheetEditor _sheetEditor;
    private readonly List<SetupWizardActorLayer> _actorLayers = new();
    private readonly SetupWizardSettingsForm _settingsForm = new();
    private readonly SetupWizardSearchPanel _search = new();
    private bool _searching;
    private bool _returnToSearch;
    private string _searchReturnPage = "hub";
    private string _searchFocusKey;
    private bool _settingSearchText;
    private string _searchHighlight;
    private float _searchHighlightSeconds;
    internal SetupWizardSearchPanel SearchPanel => _search;
    private bool _inSettingsForm;
    private bool _isDedicated;
    private GuiElementSetupPreviewDraw _diagram;
    private GuiElementRichtext _sample;
    private GuiElementRichtext _sleepCount;
    private int _chatPreviewTab = 1;
    private GuiDialogConfirm _closeConfirm;
    private IReadOnlyList<string> _restartKeys = Array.Empty<string>();
    private string _message;
    private bool _pending;
    private long _navigationVersion;
    private long? _saveNavigationVersion;
    private float _reviewScroll;
    private bool _disposing;
    private bool _forceClose;
    private bool _closing;
    private bool _runtimeApplyFailed;
    private double _elapsed;
    private double? _fixedTime;
    private bool _previewPaused;
    private int? _manualChatTab;
    private GuiElementSlider _timeline;
    private GuiElementRichtext _previewStage;
    private SetupWizardChatFocusElement _chatFocus;
    private int _timelineValue = -1;
    private int _lastBeat = -1;
    private readonly Dictionary<string, string> _topicPages = SetupWizardCatalog.Topics
        .ToDictionary(topic => topic.Id, topic => topic.EntryPageId, StringComparer.Ordinal);
    private IReadOnlyList<ConfigAdminPrivilegeSuggestion> _privilegeSuggestions = Array.Empty<ConfigAdminPrivilegeSuggestion>();
    private readonly List<GuiElementPrivilegePicker> _privilegePickers = new();
    private readonly List<GuiElementColorPicker> _colorPickers = new();
    private GuiElementSetupSidebar _sidebar;
    private GuiElementRichtext _draftStatus;
    private string _activeTopicId = "chat";
    private Action _pendingNavigation;
    private bool _focusSidebarAfterNavigation;
    private double _composedWidth;
    private double _composedHeight;

    public SetupWizardDialog(ICoreClientAPI api, SetupWizardDraft draft, string runId, bool isDedicated,
        Action onSave, Action<string, string, string> onTrack, Action onClose, Action onAdvanced = null,
        bool layoutOnly = false, bool captureOnly = false,
        System.Func<ICoreClientAPI, string, ISetupGuidePreview> actorFactory = null,
        Action<SetupWizardRequestKind> onMaintenance = null, Action onHelp = null) : base(api)
    {
        Draft = draft;
        _languageEditor = new SetupWizardLanguageEditor(api, draft,
            () => { RefreshNavigation(); Track("choice", "edited"); },
            action => QueueNavigation(() => { action(); ComposeDialog(); }));
        RunId = runId;
        _isDedicated = isDedicated;
        _onSave = onSave;
        _onTrack = onTrack;
        _onClose = onClose;
        _onAdvanced = onAdvanced;
        _onMaintenance = onMaintenance;
        _onHelp = onHelp;
        LayoutOnly = layoutOnly;
        IsCaptureOnly = captureOnly;
        _actorFactory = actorFactory ?? (layoutOnly ? null : (client, identity) => new SetupGuidePreview(client, identity));
        _actors = _actorFactory == null ? null : new(api, _actorFactory);
        _sheetEditor = new SetupWizardSheetEditor(api, draft, RefreshNavigation,
            action => QueueNavigation(() => { action(); ComposeDialog(); }), _actorFactory,
            action => Track(action));
        try
        {
            if (_actors != null)
            {
                _preview = _actors.Get("guide", "Pip");
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
    public void ShowLanguageEditor(int index = 0)
    {
        _languageEditor.SelectedIndex = index;
        ShowPage("chat.languageeditor");
    }
    public string RunId { get; private set; }
    public string CurrentStepId { get; private set; } = "hub";
    public bool LayoutOnly { get; }
    public bool IsCaptureOnly { get; }
    public bool IsPreviewReady => (_preview == null || _preview.IsReady) && _actorLayers.All(layer => layer.IsReady) &&
        SetupWizardRecordsPreview.IsReady(SingleComposer) && (_sheetEditor?.Preview?.IsReady ?? true);
    public override string ToggleKeyCombinationCode => null;
    public override bool UnregisterOnClose => true;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    public void ShowPage(string stepId)
    {
        _navigationVersion++;
        if (stepId != "hub" && stepId != "review" && stepId != "finish" && !SetupWizardCatalog.TryGetPage(stepId, out _))
            throw new ArgumentException("Unknown setup page.", nameof(stepId));
        _searching = false;
        _returnToSearch = false;
        _searchFocusKey = null;
        CurrentStepId = stepId;
        if (SetupWizardCatalog.TryGetPage(stepId, out var topicPage))
        {
            _topicPages[topicPage.TopicId] = topicPage.Id;
            _activeTopicId = topicPage.TopicId;
        }
        _preview?.EndDrag(false);
        _chatPreviewTab = CurrentStepId.StartsWith("notifications.", StringComparison.Ordinal) || Bool("UseGeneralChannelAsProximityChat") || CurrentStepId == "chat.tabs" && !Bool("ProximityChatAsDefault") ? 0 : 1;
        _elapsed = 0;
        _previewPaused = false;
        _manualChatTab = null;
        _lastBeat = -1;
        if (_fixedTime.HasValue) _preview?.SetAnimation(AnimationCode(), _fixedTime.Value);
        else _preview?.PlayAnimation(AnimationCode());
        ComposeDialog();
        Track("viewed");
    }

    public void ShowSearch(string query = null)
    {
        _navigationVersion++;
        OpenColorPicker()?.Close();
        foreach (var picker in _privilegePickers) picker.CloseSuggestions();
        if (!_searching) _searchReturnPage = CurrentStepId;
        _searching = true;
        _search.Search(Draft, query ?? _search.Query);
        ComposeDialog();
        SingleComposer.FocusElement(SingleComposer.GetTextInput("setup-search").TabIndex);
    }

    internal bool FocusSearchResult(ConfigSearchResult result)
    {
        if (result.Destination == ConfigSearchDestination.Setting)
        {
            var found = FocusSetting(result.Key);
            _returnToSearch = found;
            return found;
        }
        string control;
        if (result.Destination == ConfigSearchDestination.Language)
        {
            var entry = _search.ResolveLanguage(Draft, result.EntryId);
            if (entry == null) return false;
            _languageEditor.SelectedIndex = Draft.Languages.FindIndex(candidate => ReferenceEquals(candidate, entry));
            ShowPage("chat.languageeditor");
            control = "language-editor-" + result.Key;
        }
        else
        {
            var entry = _search.ResolveField(Draft, result.EntryId);
            if (entry == null) return false;
            control = _sheetEditor.SelectSearchField(entry, result.Key);
            ShowPage("characters.builder");
        }
        _returnToSearch = FocusControl(control, result.Label);
        return _returnToSearch;
    }

    public bool FocusSetting(string key)
    {
        var page = SetupWizardCatalog.FindPageForSetting(key);
        if (page == null) return false;
        ShowPage(page.Id);
        _searchFocusKey = key;
        if (key is "SendServerSaveAnnouncement" or "ServerSaveAnnouncementAsNotification" or "SendServerSaveFinishedAnnouncement" or "ServerSaveFinishedAsNotification")
            return FocusControl("announcement-mode", ConfigAdminSettingRegistry.Settings.First(setting => setting.Key == key).Label);
        if (SingleComposer.GetElement(key) == null) ComposeDialog();
        return FocusControl(key, ConfigAdminSettingRegistry.Settings.First(setting => setting.Key == key).Label);
    }

    private bool FocusControl(string key, string label)
    {
        if (SingleComposer.GetElement(key) is not { } control) return false;
        var scrollbarKey = key.StartsWith("language-editor-", StringComparison.Ordinal) ? "language-editor-scroll" :
            key.StartsWith("sheet-editor-", StringComparison.Ordinal) ? "sheet-editor-inspector-scroll" : "settings-scroll";
        if (control.InsideClipBounds is { } clip && SingleComposer.GetScrollbar(scrollbarKey) is SetupWizardSheetEditor.BuilderScrollbar scrollbar)
        {
            var top = Math.Max(0, control.Bounds.fixedY - 23);
            var bottom = control.Bounds.fixedY + control.Bounds.fixedHeight;
            if (top < scrollbar.CurrentYPosition) scrollbar.MoveTo((float)top);
            else if (bottom > scrollbar.CurrentYPosition + clip.fixedHeight) scrollbar.MoveTo((float)(bottom - clip.fixedHeight));
        }
        SingleComposer.FocusElement(control.TabIndex);
        _searchHighlight = key;
        _searchHighlightSeconds = 2;
        SingleComposer.GetRichtext("setup-message")?.SetNewText(VtmlUtils.EscapeVtml("Found: " + label), CairoFont.WhiteSmallText().WithFontSize(14));
        return true;
    }

    public void SetPreviewTime(double timeSeconds)
    {
        if (!double.IsFinite(timeSeconds) || timeSeconds is < 0 or > 60) throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        _fixedTime = timeSeconds;
        _timelineValue = -1;
        _preview?.SetAnimation(AnimationCode(), timeSeconds);
        RefreshPreview();
    }

    public void ResumePreview()
    {
        _fixedTime = null;
        _elapsed = 0;
        _previewPaused = false;
        _manualChatTab = null;
        _timelineValue = -1;
        _preview?.SetAnimation(AnimationCode(), 0);
        _preview?.PlayAnimation(AnimationCode());
        RefreshPreview();
    }

    public void SetSession(TheBasicsSetupWizardResultMessage result)
    {
        RunId = result.RunId;
        SupportsMaintenance = result.SupportsMaintenance;
        _search.ReviewedKeys = result.SupportsMaintenance ? result.ReviewedKeys?.ToArray() ?? Array.Empty<string>() : null;
        if (!SupportsMaintenance) _search.NewOnly = false;
        _isDedicated = result.IsDedicated;
        Draft.RefreshValues(result.Values);
        Draft.RefreshLanguages(result.Languages);
        Draft.RefreshSheetFields(result.SheetFields);
        if (_searching) _search.Search(Draft, _search.Query);
        RefreshPrivilegeSuggestions(result.PrivilegeSuggestions);
        _restartKeys = result.RestartRequiredKeys?.ToArray() ?? Array.Empty<string>();
        _runtimeApplyFailed = result.RuntimeApplyFailed;
        ComposeDialog();
    }

    internal void RememberSaveNavigation() => _saveNavigationVersion =
        !_searching && CurrentStepId == "review" ? _navigationVersion : null;

    public void SetResult(TheBasicsSetupWizardResultMessage result)
    {
        if (!Draft.MergeSaveResult(result)) return;
        var completeOnReview = _saveNavigationVersion == _navigationVersion && !_searching && CurrentStepId == "review";
        _saveNavigationVersion = null;
        RefreshPrivilegeSuggestions(result.PrivilegeSuggestions);
        _pending = false;
        _isDedicated = result.IsDedicated;
        _message = result.Message;
        _restartKeys = result.RestartRequiredKeys?.ToArray() ?? Array.Empty<string>();
        _runtimeApplyFailed = result.RuntimeApplyFailed;
        if (result.Success && !Draft.IsDirty)
        {
            if (completeOnReview) { _message = null; ShowPage("finish"); }
            Track("completed");
        }
        else
        {
            if (result.Success) _message = (_runtimeApplyFailed ? result.Message + " " : "Saved. ") + "Your newer edits are not saved yet.";
            if (completeOnReview) ShowPage("review");
        }
        if (!completeOnReview)
        {
            if (result.Success && NeedsRestart) _message += " Restart needed. See Review changes for details.";
            if (_searching) _search.Search(Draft, _search.Query);
            ComposeDialog();
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
        _navigationVersion++;
        _pendingNavigation = null;
        (SingleComposer?.GetElement("pip") as PreviewActorElement)?.CancelDrag();
        _closeConfirm?.TryClose();
        _forceClose = true;
        try { base.TryClose(); }
        finally { _forceClose = false; }
    }

    private void ComposeDialog()
    {
        _sheetEditor?.Detach();
        SingleComposer?.Dispose();
        _privilegePickers.Clear();
        _colorPickers.Clear();
        _diagram = null;
        _sample = null;
        _sleepCount = null;
        _draftStatus = null;
        _timeline = null;
        _timelineValue = -1;
        _previewStage = null;
        _chatFocus = null;
        _actorLayers.Clear();
        _composedWidth = Width;
        _composedHeight = BodyHeight;
        var body = ElementBounds.Fixed(0, 0, DialogWidth, BodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = capi.Gui.CreateCompo("thebasics-setup-wizard", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle));
        var composer = SingleComposer
            .AddShadedDialogBG(body)
            .AddDialogTitleBar(IsCaptureOnly ? "The BASICs setup (QA preview, save disabled)" : "The BASICs setup", () => TryClose())
            .BeginChildElements(body);
        composer.AddTextInput(ElementBounds.Fixed(0, 44, SidebarWidth, 28), value =>
        {
            if (!_settingSearchText) QueueNavigation(() =>
            {
                var caret = SingleComposer.GetTextInput("setup-search").CaretPosWithoutLineBreaks;
                ShowSearch(value);
                SingleComposer.GetTextInput("setup-search").CaretPosWithoutLineBreaks = caret;
            });
        }, CairoFont.TextInput().WithFontSize(14), "setup-search");
        _settingSearchText = true;
        composer.GetTextInput("setup-search").SetValue(_search.Query);
        _settingSearchText = false;
        composer.AddSmallButton(_searching ? "Back to settings" : string.IsNullOrEmpty(_search.Query) ? "Search settings" : "Back to results", () =>
        {
            QueueNavigation(() => { if (_searching) ShowPage(_searchReturnPage); else { ShowSearch(); _search.FocusSelection(SingleComposer); } });
            return true;
        }, ElementBounds.Fixed(0, 78, SidebarWidth, 28), key: "search-back");
        _sidebar = new GuiElementSetupSidebar(capi, ElementBounds.Fixed(0, 114, SidebarWidth, FooterY - 140),
            destination =>
            {
                var page = destination.StartsWith("topic:", StringComparison.Ordinal)
                    ? _topicPages[destination[6..]] : destination;
                QueueNavigation(() => ShowPage(page), true);
            });
        _sidebar.SetState(CurrentStepId, _activeTopicId, Draft.ChangedKeys);
        composer.AddInteractiveElement(_sidebar, "setup-sidebar");
        composer.AddStaticElement(new GuiElementCustomDraw(capi, ElementBounds.Fixed(0, FooterY - 14, DialogWidth, 1),
            (context, _, bounds) =>
            {
                context.SetSourceRGBA(0.82, 0.73, 0.59, 0.3);
                context.Rectangle(bounds.bgDrawX, bounds.bgDrawY, bounds.OuterWidth, bounds.OuterHeight);
                context.Fill();
            }), "footer-rule");

        composer.BeginChildElements(ElementBounds.Fixed(ContentOffset, 0, Width, BodyHeight));
        if (_searching)
        {
            _search.Add(composer, capi, Width, StatusY, result => QueueNavigation(() => FocusSearchResult(result)));
            composer.AddSmallButton(_search.NewOnly ? "Show all settings" : "New settings", () =>
            {
                QueueNavigation(() => { _search.NewOnly = !_search.NewOnly; ShowSearch(); });
                return true;
            }, ElementBounds.Fixed(Width - 180, 48, 180, 28), key: "new-settings");
            composer.GetButton("new-settings").Enabled = SupportsMaintenance;
            if (_search.NewOnly)
            {
                composer.AddSmallButton("Mark all new settings reviewed", () => { QueueNavigation(() => RequestMaintenance(SetupWizardRequestKind.MarkReviewed)); return true; },
                    ElementBounds.Fixed(Width - 300, 84, 300, 28), key: "mark-reviewed");
                composer.GetButton("mark-reviewed").Enabled = SupportsMaintenance && !IsCaptureOnly && !Draft.IsSavePending && !Draft.HasUnconfirmedSave && _onMaintenance != null;
            }
        }
        else if (CurrentStepId == "hub") ComposeHub(composer);
        else if (CurrentStepId == "review") ComposeReview(composer);
        else if (CurrentStepId == "finish") ComposeFinish(composer);
        else ComposePage(composer);
        composer.EndChildElements();
        composer.AddRichtext(VtmlUtils.EscapeVtml(_message ?? (_pending ? "Saving settings..." : "")), CairoFont.WhiteSmallText().WithFontSize(14),
            ElementBounds.Fixed(ContentOffset, StatusY, Width, CompactLayout ? 30 : 42), "setup-message");
        ComposeFooter(composer);
        composer.EndChildElements().Compose(focusFirstElement: false);
        if (!_searching && CurrentStepId == "map.visibility") RefreshMapControls(composer);
        _sample = SingleComposer.GetRichtext("preview-text");
        _sleepCount = SingleComposer.GetRichtext("preview-sleep-count");
        _draftStatus = SingleComposer.GetRichtext("footer-draft-count");
        _previewStage = SingleComposer.GetRichtext("preview-stage");
        _timeline = SingleComposer.GetSlider("preview-time");
        RefreshPreview();
        if (_focusSidebarAfterNavigation)
        {
            SingleComposer.FocusElement(_sidebar.TabIndex);
            _focusSidebarAfterNavigation = false;
        }
    }

    private void ComposeFooter(GuiComposer composer)
    {
        var finished = !_searching && CurrentStepId == "finish";
        var reviewing = !_searching && CurrentStepId == "review";
        composer.AddSmallButton("Old settings", () =>
        {
            if (_onAdvanced == null) return false;
            QueueNavigation(() => { Track("advanced"); Suspend(); _onAdvanced(); });
            return true;
        }, ElementBounds.Fixed(0, FooterY, 140, 30), key: "all-settings");
        composer.GetButton("all-settings").Enabled = _onAdvanced != null && !_pending;
        composer.AddSmallButton("Help", () => { QueueNavigation(() => _onHelp?.Invoke()); return true; },
            ElementBounds.Fixed(150, FooterY, 70, 30), key: "setup-help");
        composer.GetButton("setup-help").Enabled = _onHelp != null;
        composer.AddRichtext(DraftStatusText(), CairoFont.WhiteSmallText().WithFontSize(14),
            ElementBounds.Fixed(232, FooterY + 6, DialogWidth - 552, 24), "footer-draft-count");
        composer.AddSmallButton(finished ? NeedsRestart ? "Later" : "Done" : "Close",
            () => { QueueNavigation(() => TryClose()); return true; },
            ElementBounds.Fixed(DialogWidth - 300, FooterY, 110, 30), key: "close");
        var save = reviewing && !IsCaptureOnly;
        composer.AddSmallButton(finished ? "Back to setup" : save ? _pending ? "Saving..." : "Save settings" : "Review changes", () =>
        {
            QueueNavigation(() =>
            {
                if (finished) ShowPage("hub");
                else if (!save) ShowPage("review");
                else if (!_pending)
                {
                    _pending = true;
                    _message = "Saving settings...";
                    ComposeDialog();
                    _onSave?.Invoke();
                }
            });
            return true;
        }, ElementBounds.Fixed(DialogWidth - 180, FooterY, 180, 30), key: "review");
        composer.GetButton("review").Enabled = !_pending && !(reviewing && IsCaptureOnly) &&
            !(save && (Draft.HasUnconfirmedSave || Draft.LanguagesChanged && LanguageConfigAdmin.ValidateEntries(Draft.Languages, out _).Count > 0 ||
                Draft.SheetFieldsChanged && (!Draft.CanEditSheetFields || SetupWizardState.ValidateSheetSnapshots(Draft.SheetFields, Draft.OriginalSheetFields).Count > 0 ||
                    CharacterSheetFieldConfigAdmin.ValidateEntries(SetupWizardState.NormalizeSheetDraft(Draft.SheetFields), out _).Count > 0 || SetupWizardState.ValidateSheetChoices(Draft.SheetFields).Count > 0)));
    }

    private string DraftStatusText() => Draft.ChangedKeys.Count switch
    {
        0 => "",
        1 => "1 unsaved change",
        var count => $"{count} unsaved changes"
    };

    private void RefreshNavigation()
    {
        _sidebar?.SetState(CurrentStepId, _activeTopicId, Draft.ChangedKeys);
        _draftStatus?.SetNewText(DraftStatusText(), CairoFont.WhiteSmallText().WithFontSize(14));
    }

    private void QueueNavigation(Action action, bool sidebar = false)
    {
        if (_disposing || _closing) return;
        _pendingNavigation = action;
        _focusSidebarAfterNavigation = sidebar;
    }

    private void FlushNavigation()
    {
        var action = _pendingNavigation;
        _pendingNavigation = null;
        if (!_disposing && !_closing) action?.Invoke();
    }

    private void ComposeHub(GuiComposer composer)
    {
        Text(composer, "Set up your server", 0, 52, Width - 150, 30, true);
        Text(composer, "Choose a topic to get started.", 0, 89, Width - 150, 48);
        var cardWidth = (Width - 48) / 4;
        for (var index = 0; index < SetupWizardCatalog.Topics.Count; index++)
        {
            var topic = SetupWizardCatalog.Topics[index];
            var x = index % 4 * (cardWidth + 16);
            var y = (CompactLayout ? 120 : 129) + index / 4 * (CompactLayout ? 122 : 140);
            composer.AddInteractiveElement(new GuiElementSetupTopicCard(capi, topic.Title, topic.Description, TopicIcon(topic.Id),
                () => { QueueNavigation(() => ShowPage(_topicPages[topic.Id])); return true; }, ElementBounds.Fixed(x, y, cardWidth, CompactLayout ? 110 : 124)), "topic-" + topic.Id);
        }
        var guideBounds = ElementBounds.Fixed(Width - 114, 36, 100, 84);
        if (_preview == null) Text(composer, "Native seraph omitted", guideBounds.fixedX, guideBounds.fixedY, guideBounds.fixedWidth, guideBounds.fixedHeight);
        else composer.AddInteractiveElement(new PreviewActorElement(capi, guideBounds, this), "pip");
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
        if (CurrentStepId == "characters.builder")
        {
            _sheetEditor.SetAvailableHeight(StatusY - 166);
            _sheetEditor.Add(composer, Width, CompactLayout);
            return;
        }
        if (CurrentStepId == "chat.languageeditor")
        {
            _languageEditor.Add(composer, Width, CompactLayout);
            return;
        }
        SetupWizardCatalog.TryGetPage(CurrentStepId, out var page);
        Text(composer, page.Title, 0, 48, Width, 27, true);
        Text(composer, page.Description, 0, 80, Width, 44);
        AddPreview(composer);
        var y = CompactLayout && CurrentStepId == "chat.basics" ? 130d : 138d;
        if (CurrentStepId == "notifications.savestart" || CurrentStepId == "notifications.savefinish")
            AddAnnouncement(composer, ref y);
        else
        {
            var keys = page.SettingKeys.Where(SettingVisible).ToArray();
            var rowHeight = CompactLayout ? 54 : 65;
            var scroll = keys.Length * rowHeight > StatusY - y - 8 && CurrentStepId != "chat.basics";
            if (scroll)
            {
                _settingsForm.Begin(composer, CurrentStepId, ElementBounds.Fixed(SettingsX, y, SettingsWidth, StatusY - y - 8));
                _inSettingsForm = true;
                y = 0;
            }
            if (SetupWizardChatTextEditor.Supports(CurrentStepId))
            {
                y = SetupWizardChatTextEditor.Add(composer, capi, Draft, CurrentStepId, SettingsWidth,
                    (key, value) => Changed(key, value, "edited", false));
                if (CurrentStepId == "chat.wording")
                    foreach (var key in SetupWizardChatTextEditor.OtherWordingKeys) AddSetting(composer, key, ref y);
            }
            else foreach (var key in keys) AddSetting(composer, key, ref y);
            if (scroll)
            {
                _inSettingsForm = false;
                _settingsForm.End(composer, capi, y, () =>
                {
                    foreach (var picker in _privilegePickers) picker.CloseSuggestions();
                    foreach (var picker in _colorPickers) picker.Close();
                });
            }
        }
        if (CurrentStepId == "server.integrations")
            composer.AddRichtext("Relay availability: not checked on the server.", CairoFont.WhiteSmallText().WithFontSize(15), ElementBounds.Fixed(SettingsX, 218, SettingsWidth, 55), "integration-availability");
        if (CurrentStepId == "server.diagnostics")
        {
            Text(composer, "Reload settings", 0, 220, Width, 27, true);
            composer.AddSmallButton("Reload from disk", () => { QueueNavigation(() => RequestMaintenance(SetupWizardRequestKind.Reload)); return true; },
                ElementBounds.Fixed(0, 265, 210, 30), key: "reload-disk");
            var enabled = SupportsMaintenance && !IsCaptureOnly && !Draft.IsSavePending && !Draft.HasUnconfirmedSave && _onMaintenance != null;
            composer.GetButton("reload-disk").Enabled = enabled;
            Text(composer, SupportsMaintenance ? "Reload reads the saved server file. Unsaved edits are discarded only after a successful reload." : "This server does not support these actions. Update it to review new settings or reload here.", 0, 310, Width, 72);
        }
        if (CurrentStepId == "server.stats")
            SetStatsControlsEnabled(composer);

    }

    internal void RequestMaintenance(SetupWizardRequestKind kind)
    {
        if (!SupportsMaintenance || IsCaptureOnly || Draft.IsSavePending || Draft.HasUnconfirmedSave || _onMaintenance == null) return;
        if (kind != SetupWizardRequestKind.Reload || !Draft.IsDirty) { _onMaintenance(kind); return; }
        _closeConfirm?.Dispose();
        _closeConfirm = new DiscardConfirmDialog(capi, "Reload from disk and discard your unsaved changes? If reload fails, your edits stay here.", discard =>
        {
            if (discard) _onMaintenance(kind);
        });
        _closeConfirm.TryOpen();
    }

    private bool SettingVisible(string key)
    {
        if (key == _searchFocusKey) return true;
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
        if (_inSettingsForm)
        {
            var font = CairoFont.WhiteSmallText().WithFontSize(14);
            if (CurrentStepId == "server.stats" && (key.StartsWith("PlayerStatToggles.", StringComparison.Ordinal) || key == "PlayerStatDistanceTravelledTimer") &&
                (!Bool("PlayerStatSystem") || key == "PlayerStatDistanceTravelledTimer" && !Bool("PlayerStatToggles.DistanceTravelled"))) font.Color[3] = .35;
            composer.AddInteractiveElement(new SetupWizardViewportRichtext(capi, VtmlUtils.EscapeVtml(label), font,
                ElementBounds.Fixed(SettingsX, y, SettingsWidth, 21)), "setting-label-" + key);
        }
        else if (key is "MapHideOtherPlayers" or "MapPlayerRenderDistance")
            composer.AddStaticText(label, CairoFont.WhiteSmallText().WithFontSize(14),
                ElementBounds.Fixed(SettingsX, y, SettingsWidth, 21), "setting-label-" + key);
        else Text(composer, label, SettingsX, y, SettingsWidth, 21);
        var bounds = ElementBounds.Fixed(SettingsX, y + 23, SettingsWidth, 28);
        var value = Draft.Get(key);
        if (definition.Kind == ConfigAdminSettingKind.Boolean)
        {
            var inverted = key == "DisableRPChat";
            bounds.fixedWidth = 112;
            composer.AddInteractiveElement(new GuiElementSetupToggle(capi, Bool(key) != inverted,
                on =>
                {
                    Changed(key, on != inverted ? "1" : "0", on ? "on" : "off", key is not ("MapHideOtherPlayers" or "ManageMapPlayerVisibility"));
                    if (key is "MapHideOtherPlayers" or "ManageMapPlayerVisibility") RefreshMapControls(composer);
                }, bounds), key);
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
                composer.AddInteractiveElement(new SetupWizardViewportDropDown(capi, options,
                    options.Select((option, index) => SettingOptionName(key, option, definition.OptionNames[index])).ToArray(), selected,
                    (choice, _) => Changed(key, choice, "option" + Array.IndexOf(options, choice), false), bounds, CairoFont.WhiteSmallText()), key);
        }
        else if (key is "EmoteColor" or "OOCColor" or "GlobalOOCColor" or "NametagBackgroundColor" or "NametagBorderColor")
        {
            var mode = key.StartsWith("Nametag", StringComparison.Ordinal) ? ColorPickerMode.RgbaWithTheme : ColorPickerMode.Rgb;
            var updating = false;
            var picker = new GuiElementColorPicker(capi, ElementBounds.Fixed(SettingsX, y + 23, 32, 28), value, choice =>
            {
                updating = true;
                try { composer.GetTextInput(key)?.SetValue(choice); }
                finally { updating = false; }
                Changed(key, choice, "edited", false);
            }, mode);
            bounds.fixedX += 39;
            bounds.fixedWidth -= 39;
            var input = new SetupWizardViewportTextInput(capi, bounds, choice =>
            {
                if (updating) return;
                picker.SetValue(choice);
                Changed(key, choice, "edited", false);
            }, CairoFont.TextInput());
            input.SetMaxLength(definition.Kind == ConfigAdminSettingKind.Text ? Math.Max(2048, value?.Length ?? 0) : 32);
            input.SetValue(value);
            _colorPickers.Add(picker);
            composer.AddInteractiveElement(picker, "color-picker-" + key);
            composer.AddInteractiveElement(input, key);
        }
        else if (definition.IsPrivilege)
        {
            var picker = new GuiElementPrivilegePicker(capi, bounds, value, _privilegeSuggestions,
                choice => Changed(key, choice, "edited", false), CairoFont.TextInput());
            _privilegePickers.Add(picker);
            composer.AddInteractiveElement(picker, key);
        }
        else
        {
            var input = new SetupWizardViewportTextInput(capi, bounds, choice =>
            {
                var requested = choice;
                if (key == "SleepNotificationThreshold" && double.TryParse(choice, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
                    requested = (percentage / 100).ToString(CultureInfo.InvariantCulture);
                Changed(key, requested, "edited", false);
            }, CairoFont.TextInput());
            input.SetMaxLength(definition.Kind == ConfigAdminSettingKind.Text ? Math.Max(2048, value?.Length ?? 0) : 32);
            input.SetValue(key == "SleepNotificationThreshold" ? (Number(key) * 100).ToString(CultureInfo.InvariantCulture) : value);
            composer.AddInteractiveElement(input, key);
        }
        if (key != "ProximityChatPresentationMode")
        {
            var help = key switch
            {
                "SleepNotificationThreshold" => "Percentage of online players who must be in bed to trigger a reminder. Enter 50 for 50%. At 0% or 100%, no reminder is sent.",
                "ChatHistoryFlushIntervalMilliseconds" => "Milliseconds between writes of queued chat history to disk. 1000 milliseconds is 1 second. Changes require a server restart.",
                "PlayerStatDistanceTravelledTimer" => "Milliseconds between movement samples. 1000 milliseconds is 1 second. Longer intervals sample less often. Changes require a server restart.",
                "DisableRPChat" => "Enable roleplay commands, languages and global OOC. Nearby chat still works when this is off.",
                _ => definition.Description
            };
            if (definition.ReloadBehavior == ConfigAdminReloadBehavior.RestartRequired && !help.Contains("restart", StringComparison.OrdinalIgnoreCase))
                help += "\nThis change takes effect after the server restarts.";
            composer.AddHoverText(help, CairoFont.WhiteSmallText(), 400, bounds.FlatCopy(), "help-" + key);
        }
        y += CurrentStepId == "chat.basics" ? (CompactLayout ? 50 : 61) : CompactLayout ? 54 : 65;
    }

    private void SetStatsControlsEnabled(GuiComposer composer)
    {
        foreach (var key in SetupWizardCatalog.Pages.First(page => page.Id == "server.stats").SettingKeys.Where(key => key.StartsWith("PlayerStatToggles.", StringComparison.Ordinal) || key == "PlayerStatDistanceTravelledTimer"))
        {
            var enabled = Bool("PlayerStatSystem") && (key != "PlayerStatDistanceTravelledTimer" || Bool("PlayerStatToggles.DistanceTravelled"));
            var control = (GuiElementControl)composer.GetElement(key);
            control.Enabled = enabled;
            if (control is GuiElementTextBase text) text.Font.Color[3] = enabled ? 1 : .35;
        }
    }

    private void RefreshMapControls(GuiComposer composer)
    {
        var managed = Bool("ManageMapPlayerVisibility");
        var toggle = (GuiElementSetupToggle)composer.GetElement("ManageMapPlayerVisibility");
        toggle.SetValue(managed);
        foreach (var key in new[] { "MapHideOtherPlayers", "MapPlayerRenderDistance" })
        {
            var enabled = managed && (key != "MapPlayerRenderDistance" || !Bool("MapHideOtherPlayers"));
            var control = (GuiElementControl)composer.GetElement(key);
            if (!enabled) control.OnFocusLost();
            control.Enabled = enabled;
            if (control is GuiElementTextBase text) text.Font.Color[3] = enabled ? 1 : 0.35;
            var label = (GuiElementStaticText)composer.GetElement("setting-label-" + key);
            label.Font.Color[3] = enabled ? 1 : 0.35;
        }
        // Refresh native textures in place so the active callback keeps its composer.
        composer.ReCompose();
    }

    private void RefreshPrivilegeSuggestions(IEnumerable<ConfigAdminPrivilegeSuggestion> suggestions) =>
        _privilegeSuggestions = ConfigAdminPrivilegeSuggestions.ForClient(suggestions, Draft.Values
            .Where(value => ConfigAdminSettingRegistry.TryGet(value.Key, out var definition) && definition.IsPrivilege)
            .Select(value => value.Value));

    private void AddAnnouncement(GuiComposer composer, ref double y)
    {
        var started = CurrentStepId == "notifications.savestart";
        var enabled = started ? "SendServerSaveAnnouncement" : "SendServerSaveFinishedAnnouncement";
        var popup = started ? "ServerSaveAnnouncementAsNotification" : "ServerSaveFinishedAsNotification";
        var text = started ? "TEXT_ServerSaveAnnouncement" : "TEXT_ServerSaveFinished";
        Text(composer, "Announcement", SettingsX, y, SettingsWidth, 21);
        composer.AddInteractiveElement(new GuiElementDropDown(capi, new[] { "off", "chat", "popup" }, new[] { "Off", "Chat", "Chat notification" },
            !Bool(enabled) ? 0 : Bool(popup) ? 2 : 1, (choice, _) =>
            {
                Draft.Set(enabled, choice == "off" ? "0" : "1");
                if (choice != "off") Draft.Set(popup, choice == "popup" ? "1" : "0");
                Track("choice", choice);
                RefreshNavigation();
                RefreshPreview();
            }, ElementBounds.Fixed(SettingsX, y + 23, SettingsWidth, 28), CairoFont.WhiteSmallText(), false), "announcement-mode");
        y += CompactLayout ? 54 : 65;
        AddSetting(composer, text, ref y);
    }

    private void Changed(string key, string value, string choice, bool recompose)
    {
        if (Draft.Get(key) == value) return;
        Draft.Set(key, value);
        _timelineValue = -1;
        if (!_fixedTime.HasValue && AnimatedPreview && _elapsed >= PreviewDuration)
        {
            _elapsed = Math.Max(0, PreviewDuration - 0.001);
            _timelineValue = -1;
        }
        if (CurrentStepId == "chat.tabs")
            _chatPreviewTab = Bool("UseGeneralChannelAsProximityChat") || !Bool("ProximityChatAsDefault") ? 0 : 1;
        Track("choice", choice);
        RefreshNavigation();
        RefreshPreview();
        if (recompose)
        {
            // Widget callbacks must finish before their composer is replaced.
            var page = CurrentStepId;
            capi.Event.RegisterCallback(_ =>
            {
                if (_disposing || CurrentStepId != page) return;
                var restoreFocus = SingleComposer.GetElement(key)?.HasFocus == true;
                ComposeDialog();
                if (restoreFocus) RestoreSettingFocus(key);
            }, 0);
        }
    }

    private void RestoreSettingFocus(string key)
    {
        if (!SetupWizardCatalog.TryGetPage(CurrentStepId, out var page)) return;
        var keys = page.SettingKeys.ToArray();
        var editedIndex = Array.IndexOf(keys, key);
        foreach (var candidate in keys.Select((settingKey, index) => (settingKey, index)).OrderBy(candidate => Math.Abs(candidate.index - editedIndex)))
        {
            if (SingleComposer.GetElement(candidate.settingKey) is not GuiElementControl { Enabled: true, Focusable: true } control) continue;
            SingleComposer.FocusElement(control.TabIndex);
            return;
        }
    }

    private void ComposeReview(GuiComposer composer)
    {
        Text(composer, "Review changes", 0, 50, Width, 28, true);
        Text(composer, "Check the changes below, then save. Anything marked After restart will take effect when the server restarts.", 0, 85, Width, 42);
        var rows = Draft.ChangedKeys.Where(key => key != "Languages" && key != "CharacterSheetFields").Select(key =>
        {
            ConfigAdminSettingRegistry.TryGet(key, out var definition);
            return (Title: SettingLabel(definition) + (definition.ReloadBehavior == ConfigAdminReloadBehavior.Live ? "" : " | After restart"),
                Detail: $"{DisplayValue(definition, Draft.GetOriginal(key))}  >  {DisplayValue(definition, Draft.Get(key))}");
        }).Concat(LanguageReviewRows(Draft.OriginalLanguages, Draft.Languages))
            .Concat(SetupWizardSheetReview.Rows(Draft.OriginalSheetFields, Draft.SheetFields, Bool("EnableCharacterSheets") && Bool("CharacterSheetRequireRequiredFieldsForRoleplay"), Bool("DisableNicknames"))).ToArray();
        if (NeedsRestart)
        {
            var names = _restartKeys.Select(key => ConfigAdminSettingRegistry.TryGet(key, out var setting) ? SettingLabel(setting) : key);
            var detail = (_runtimeApplyFailed ? "Some saved changes could not be applied.\n" : "") +
                string.Join("\n", names) + "\n" + RestartInstructions();
            rows = new[] { (Title: "Saved settings needing restart", Detail: detail) }.Concat(rows).ToArray();
        }
        var viewport = ElementBounds.Fixed(0, 140, Width - 20, Math.Max(100, StatusY - 150));
        var content = ElementBounds.Fixed(0, 0, Width - 30, 1);
        composer.BeginClip(viewport).BeginChildElements(content);
        var y = 0d;
        var rowIndex = 0;
        foreach (var row in rows)
        {
            var lines = row.Detail.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / Math.Max(30, (Width - 30) / 8))));
            var detailHeight = Math.Max(36, lines * 22);
            composer.AddInteractiveElement(new SetupWizardViewportRichtext(capi, VtmlUtils.EscapeVtml(row.Title), CairoFont.WhiteSmallText().WithFontSize(16).WithWeight(FontWeight.Bold), ElementBounds.Fixed(0, y, Width - 30, 22)), "review-title-" + rowIndex);
            composer.AddInteractiveElement(new SetupWizardViewportRichtext(capi, VtmlUtils.EscapeVtml(row.Detail).Replace("\n", "<br>"), CairoFont.WhiteSmallText().WithFontSize(14), ElementBounds.Fixed(0, y + 24, Width - 30, detailHeight)), "review-detail-" + rowIndex);
            rowIndex++;
            y += detailHeight + 38;
        }
        if (rows.Length == 0) Text(composer, NeedsRestart ? "No new changes. Restart the server to apply your saved settings." : "No changes to save.", 0, y, Width - 30, 60);
        content.fixedHeight = Math.Max(viewport.fixedHeight, y + 60);
        composer.EndChildElements().EndClip();
        void ScrollReview(float value)
        {
            _reviewScroll = value;
            content.fixedY = -value; content.MarkDirtyRecursive(); content.CalcWorldBounds();
        }
        composer.AddVerticalScrollbar(ScrollReview, ElementBounds.Fixed(Width - 14, 140, 12, viewport.fixedHeight), "review-scroll");
        composer.OnComposed += () =>
        {
            var scroll = composer.GetScrollbar("review-scroll");
            scroll.SetHeights((float)viewport.fixedHeight, (float)content.fixedHeight);
            scroll.CurrentYPosition = Math.Clamp(_reviewScroll, 0, (float)(content.fixedHeight - viewport.fixedHeight));
            ScrollReview(scroll.CurrentYPosition);
        };
    }
    internal static IReadOnlyList<(string Title, string Detail)> LanguageReviewRows(
        IReadOnlyList<LanguageConfigEntryMessage> originals, IReadOnlyList<LanguageConfigEntryMessage> current)
    {
        var rows = new List<(string Title, string Detail)>();
        if (LanguageConfigAdmin.EntriesEqual(originals, current)) return rows;
        var fields = new (string Label, System.Func<LanguageConfigEntryMessage, string> Read)[]
        {
            ("Name", value => value.Name), ("Chat code", value => value.Prefix),
            ("Text color", value => value.Color), ("Description", value => value.Description),
            ("Unfamiliar sounds", value => value.Syllables),
            ("Everyone starts with it", value => value.Default ? "On" : "Off"),
            ("Hide from language lists", value => value.Hidden ? "On" : "Off"),
            ("Classes", value => value.GrantedToClasses), ("Traits", value => value.GrantedToTraits),
            ("Models", value => value.GrantedToModels), ("Model groups", value => value.GrantedToModelGroups)
        };
        foreach (var entry in current)
        {
            var original = originals.FirstOrDefault(value => string.Equals(value.Name, entry.OriginalName, StringComparison.OrdinalIgnoreCase));
            foreach (var field in fields)
            {
                var before = original == null ? "" : field.Read(original) ?? "";
                var after = field.Read(entry) ?? "";
                if (before != after) rows.Add(($"{entry.Name}: {field.Label}", original == null ? "New language: " + after : before + "  >  " + after));
            }
        }
        foreach (var removed in originals.Where(value => !current.Any(entry => string.Equals(entry.OriginalName, value.Name, StringComparison.OrdinalIgnoreCase))))
            rows.Add(("Remove language: " + removed.Name, "This language will be removed from the server configuration."));
        if (!originals.Select(entry => entry.Name).SequenceEqual(current.Select(entry => entry.OriginalName), StringComparer.OrdinalIgnoreCase))
            rows.Add(("Language order", string.Join(" > ", current.Select(entry => entry.Name))));
        return rows;
    }

    private void ComposeFinish(GuiComposer composer)
    {
        Text(composer, IsCaptureOnly ? "QA example: saved settings" : "Settings saved", 0, 50, Width, 28, true);
        if (!NeedsRestart)
            Text(composer, "Your settings are now in use.", 0, 105, Width, 80);
        else
        {
            Text(composer, "Restart needed", 0, 103, Width, 27, true);
            var names = _restartKeys.Select(key => ConfigAdminSettingRegistry.TryGet(key, out var setting) ? SettingLabel(setting) : key);
            Text(composer, _runtimeApplyFailed ? "Restart the server to apply the saved settings.\n" + string.Join(", ", names) : string.Join(", ", names), 0, 139, Width, 126);
            Text(composer, RestartInstructions(), 0, 290, Width, 90);
        }
    }

    private string RestartInstructions() => _isDedicated
        ? "Restart the server from your hosting panel. If you host it yourself, stop and start the server. Players will need to reconnect."
        : "Save and quit to the main menu, then reopen this world.";

    private void AddPreview(GuiComposer composer)
    {
        if (SetupWizardVoicePreview.Supports(CurrentStepId))
        {
            composer.AddRichtext(SetupWizardVoicePreview.Text(Draft, CurrentStepId, IsCaptureOnly ? null : capi.ModLoader.IsModSystemEnabled("RPTTS.RPTTSAPI")),
                CairoFont.WhiteSmallText().WithFontSize(15), ElementBounds.Fixed(0, 138, 310, CompactLayout ? 286 : 360), "preview-audio");
            return;
        }
        if (SetupWizardChatTextEditor.Supports(CurrentStepId))
        {
            AddChatPreview(composer, 138, CompactLayout ? 250 : 300);
            return;
        }
        if (SetupWizardFeaturePreview.IsSettingsOnly(CurrentStepId)) return;
        if (AnimatedPreview) AddPlayback(composer);
        if (CurrentStepId == "roleplay.descriptions")
        {
            _diagram = new GuiElementSetupPreviewDraw(capi, ElementBounds.Fixed(0, 134, 310, 100),
                (context, surface, bounds) =>
                {
                    var reach = Number("MaxEnvironmentPlacementDistance");
                    var scale = surface.Width / 310d;
                    context.Scale(scale, scale);
                    context.SetSourceRGBA(.8, .75, .6, 1);
                    context.LineWidth = 2;
                    context.MoveTo(20, 55); context.LineTo(280, 55); context.Stroke();
                    context.SetSourceRGBA(.35, .7, .45, .6);
                    context.Rectangle(20, 49, Math.Min(260, reach / 20 * 260), 12); context.Fill();
                    context.SetSourceRGBA(reach >= 10 ? .6 : .9, reach >= 10 ? .9 : .4, .3, 1);
                    context.Arc(150, 55, 7, 0, Math.PI * 2); context.Fill();
                });
            composer.AddInteractiveElement(_diagram, "preview-diagram");
            composer.AddRichtext(PlacementRangeText(),
                CairoFont.WhiteSmallText().WithFontSize(14), ElementBounds.Fixed(0, 205, 310, 38), "preview-placement-range");
            AddChatPreview(composer, 245, CompactLayout ? 100 : 145);
            return;
        }
        if (CurrentStepId == "chat.tabs")
        {
            composer.AddRichtext(ChatFocusState.StageText, CairoFont.WhiteSmallText().WithFontSize(14),
                ElementBounds.Fixed(0, 134, 310, 38), "preview-stage");
            _chatFocus = new SetupWizardChatFocusElement(capi, ElementBounds.Fixed(0, 178, 310, CompactLayout ? 216 : 274), index =>
            {
                SetPreviewPaused(true);
                _manualChatTab = index;
                RefreshPreview();
            });
            composer.AddInteractiveElement(_chatFocus, "preview-chat-focus");
            _chatFocus.Refresh(Draft, Time, AutoSelectChat, AutoOpenChat, _manualChatTab);
            return;
        }
        if (SetupWizardAppearancePreview.Supports(CurrentStepId))
        {
            var height = CompactLayout ? 270 : 310;
            _diagram = new GuiElementSetupPreviewDraw(capi, ElementBounds.Fixed(0, 134, 310, height),
                (context, surface, bounds) => SetupWizardAppearancePreview.Draw(context, surface, bounds,
                    CurrentStepId, Draft, Time, capi));
            composer.AddInteractiveElement(_diagram, "preview-diagram");
            if (_preview != null)
                composer.AddInteractiveElement(new PreviewActorElement(capi,
                    ElementBounds.Fixed(SetupWizardAppearancePreview.ActorX,
                        134 + SetupWizardAppearancePreview.ActorY + (height - 270) / 2,
                        SetupWizardAppearancePreview.ActorWidth, SetupWizardAppearancePreview.ActorHeight), this), "pip");
            else Text(composer, "Native seraph omitted", 112, 134 + 135, 100, 30);
            return;
        }
        if (SetupWizardRecordsPreview.Supports(CurrentStepId))
        {
            SetupWizardRecordsPreview.Add(composer, capi, ElementBounds.Fixed(0, 134, 310, CompactLayout ? 270 : 310),
                CurrentStepId, Draft, Time, _actorFactory, () => _previewPaused || _fixedTime.HasValue);
            return;
        }
        if (SetupWizardSpeechPreview.HasScene(CurrentStepId) || SetupWizardTravelPreview.Supports(CurrentStepId))
        {
            var sceneHeight = CompactLayout ? 130 : 180;
            _diagram = new GuiElementSetupPreviewDraw(capi, ElementBounds.Fixed(0, 134, 310, sceneHeight),
                (context, surface, bounds) =>
                {
                    if (CurrentStepId == "chat.occlusion" && SetupWizardTextAppearancePreview.Feedback(Draft, CurrentStepId).Length > 0)
                    {
                        context.SetSourceRGB(.19, .23, .19); context.Paint();
                    }
                    else if (SetupWizardSpeechPreview.HasScene(CurrentStepId))
                        SetupWizardSpeechPreview.Draw(context, surface, bounds, CurrentStepId, Draft, Time);
                    else SetupWizardTravelPreview.Draw(context, surface, bounds, CurrentStepId, Draft, Time);
                });
            composer.AddInteractiveElement(_diagram, "preview-diagram");
            AddSceneActors(composer, ElementBounds.Fixed(0, 134, 310, sceneHeight), () =>
                CurrentStepId == "chat.occlusion" && SetupWizardTextAppearancePreview.Feedback(Draft, CurrentStepId).Length > 0 ? [] : SetupWizardSpeechPreview.HasScene(CurrentStepId)
                    ? SetupWizardSpeechPreview.Actors(CurrentStepId, Draft, Time, 310, sceneHeight)
                    : SetupWizardTravelPreview.Actors(CurrentStepId, Draft, Time, 310, sceneHeight));
            if (CurrentStepId == "chat.occlusion")
            {
                composer.BeginClip(ElementBounds.Fixed(0, 136 + sceneHeight, 310, 34))
                    .AddRichtext(SetupWizardTextAppearancePreview.Sight(Draft), CairoFont.WhiteSmallText().WithFontSize(11),
                        ElementBounds.Fixed(0, 0, 310, 34), "preview-sight").EndClip();
                AddChatPreview(composer, 180 + sceneHeight, CompactLayout ? 38 : 58);
            }
            else AddChatPreview(composer, 146 + sceneHeight, CompactLayout ? 72 : 92);
            return;
        }
        if (SetupWizardFeaturePreview.Supports(CurrentStepId))
        {
            _diagram = new GuiElementSetupPreviewDraw(capi, ElementBounds.Fixed(0, 134, 310, FeaturePreviewHeight),
                (context, surface, bounds) => SetupWizardFeaturePreview.Draw(context, surface, bounds, CurrentStepId, Draft, capi));
            composer.AddInteractiveElement(_diagram, "preview-diagram");
            return;
        }
        if (CurrentStepId == "chat.textappearance")
        {
            composer.BeginClip(ElementBounds.Fixed(0, 134, 310, 44))
                .AddRichtext(SetupWizardTextAppearancePreview.Feedback(Draft, CurrentStepId), CairoFont.WhiteSmallText().WithFontSize(12),
                    ElementBounds.Fixed(0, 0, 310, 44), "preview-validation").EndClip();
            AddChatPreview(composer, CompactLayout ? 190 : 198, CompactLayout ? 158 : 204);
            return;
        }
        if (CurrentStepId is "chat.basics" or "chat.language" or "chat.colors" or "chat.languageaccess" or "notifications.savestart" or "notifications.savefinish" or "notifications.deaths")
        {
            AddChatPreview(composer, CompactLayout ? 164 : 172, CompactLayout ? 184 : 230);
            return;
        }
        var sleep = CurrentStepId == "notifications.sleep";
        if (!sleep) return;
        var pictureHeight = CompactLayout ? 112 : 140;
        _diagram = new GuiElementSetupPreviewDraw(capi, ElementBounds.Fixed(0, 134, 310, pictureHeight), DrawSleepScene);
        composer.AddInteractiveElement(_diagram, "preview-diagram");
        AddSceneActors(composer, ElementBounds.Fixed(0, 134, 310, pictureHeight), () => SleepActors(Time, pictureHeight));
        composer.AddRichtext(SleepCountText(), CairoFont.WhiteSmallText().WithFontSize(14),
            ElementBounds.Fixed(0, CompactLayout ? 249 : 277, 310, 20), "preview-sleep-count");
        AddChatPreview(composer, CompactLayout ? 274 : 307, CompactLayout ? 72 : 92);
    }

    private void AddSceneActors(GuiComposer composer, ElementBounds bounds, Func<SetupWizardActorSample[]> samples)
    {
        if (_actors == null)
        {
            Text(composer, "Native seraphs omitted", bounds.fixedX + 8, bounds.fixedY + 4, bounds.fixedWidth - 16, 20);
            return;
        }
        var layer = new SetupWizardActorLayer(capi, bounds, _actors, samples, () => _previewPaused || _fixedTime.HasValue);
        _actorLayers.Add(layer);
        composer.AddInteractiveElement(layer, "preview-actors");
    }

    internal static SetupWizardActorSample[] SleepActors(double time, double height) => Enumerable.Range(0, 4).Select(index =>
    {
        var x = 12 + index % 2 * 154;
        var y = 12 + index / 2 * 53 + (height - 112) / 2;
        var asleep = index < (int)(time / 2) % 4;
        return new SetupWizardActorSample("sleep-" + index, index % 2 == 0 ? "Pip" : "Mara", asleep ? "sleep" : "idle", time % 60,
            asleep ? x + 5 : x + 111, asleep ? y + 3 : y - 8, asleep ? 108 : 32, asleep ? 35 : 50,
            YawOffset: asleep ? 1.2707963f + MathF.PI : 0);
    }).ToArray();

    private void AddChatPreview(GuiComposer composer, double y, double historyHeight)
    {
        var tabs = CurrentStepId.StartsWith("notifications.", StringComparison.Ordinal) || SetupWizardTravelPreview.Supports(CurrentStepId) || Bool("UseGeneralChannelAsProximityChat")
            ? new[] { new GuiTab { Name = "General", DataInt = 0 } }
            : new[] { new GuiTab { Name = "General", DataInt = 0 }, new GuiTab { Name = "Proximity", DataInt = 1 } };
        AddChatWindow(composer, capi, y, historyHeight, tabs, tabs.Length == 1 ? 0 : _chatPreviewTab,
            index => { _chatPreviewTab = index; RefreshPreview(); }, BuildPreviewText());
    }

    internal static void AddChatWindow(GuiComposer composer, ICoreClientAPI api, double y, double historyHeight,
        GuiTab[] tabs, int selected, Action<int> onSelect, string text)
    {
        const double historyWidth = 288;
        var bounds = ElementBounds.Fixed(0, y, historyWidth + 22, historyHeight + 57);
        composer.BeginChildElements(bounds)
            .AddGameOverlay(ElementBounds.Fixed(0, 23, historyWidth + 22, historyHeight + 34), new[] { 0.25, 0.208, 0.161, 0.75 });
        var font = CairoFont.WhiteDetailText().WithFontSize(14);
        composer.AddHorizontalTabs(tabs, ElementBounds.Fixed(0, 0, historyWidth + 22, 23), onSelect,
            font, font.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "preview-chat-tabs");
        composer.GetHorizontalTabs("preview-chat-tabs").SetValue(selected, false);
        var clip = ElementBounds.Fixed(6, 26, historyWidth, historyHeight);
        composer.BeginClip(clip)
            .AddRichtext(text, font, ElementBounds.Fixed(0, 0, historyWidth, historyHeight), "preview-text")
            .EndClip()
            .AddCompactVerticalScrollbar(value =>
            {
                var history = composer.GetRichtext("preview-text");
                history.Bounds.fixedY = -value;
                history.Bounds.MarkDirtyRecursive();
                history.Bounds.CalcWorldBounds();
            }, ElementBounds.Fixed(historyWidth + 12, 24, 10, historyHeight + 3), "preview-chat-scroll");
        var input = new GuiElementChatInput(api, ElementBounds.Fixed(0, historyHeight + 29, historyWidth + 22, 25), _ => { }) { Enabled = false };
        composer.AddInteractiveElement(input, "preview-chat-input").EndChildElements();
    }

    private void AddPlayback(GuiComposer composer)
    {
        var y = CompactLayout ? 416 : 480;
        composer.AddInset(ElementBounds.Fixed(0, y - 3, 310, 30), 1, 0.6f);
        composer.AddSmallButton("Replay", () => { ResumePreview(); return true; },
            ElementBounds.Fixed(3, y, 64, 24), key: "preview-replay");
        composer.AddToggleButton("Pause", CairoFont.WhiteSmallText().WithFontSize(13), SetPreviewPaused,
            ElementBounds.Fixed(74, y, 62, 24), "preview-pause");
        composer.AddSlider(value =>
        {
            _elapsed = Math.Min(PreviewDuration * value / 1000, Math.Max(0, PreviewDuration - 0.001));
            _fixedTime = null;
            _previewPaused = true;
            _manualChatTab = null;
            _timelineValue = -1;
            _preview?.SetAnimation(AnimationCode(), Time % 60);
            RefreshPreview();
            return true;
        }, ElementBounds.Fixed(146, y + 2, 158, 20), "preview-time");
        var slider = composer.GetSlider("preview-time");
        slider.OnSliderTooltip = _ => PreviewClockText();
        slider.SetValues(0, 0, 1000, 1);
        composer.AddRichtext(PreviewClockText(), CairoFont.WhiteSmallText().WithFontSize(12),
            ElementBounds.Fixed(318, y + 4, 154, 20), "preview-clock");
        composer.AddHoverText("Restart this example.", CairoFont.WhiteSmallText(), 260,
            ElementBounds.Fixed(3, y, 64, 24), "preview-replay-help");
        composer.AddHoverText("Pause or resume this example.", CairoFont.WhiteSmallText(), 260,
            ElementBounds.Fixed(74, y, 62, 24), "preview-pause-help");
    }

    private void SetPreviewPaused(bool paused)
    {
        _elapsed = Time;
        _fixedTime = null;
        _previewPaused = paused;
        if (!paused)
        {
            _manualChatTab = null;
            _preview?.PlayAnimation(AnimationCode());
        }
        RefreshPreview();
    }

    private void RefreshPlayback()
    {
        if (_timeline == null) return;
        SingleComposer.GetToggleButton("preview-pause").SetValue(_previewPaused || _fixedTime.HasValue);
        var second = (int)Math.Floor(Time);
        if (_timelineValue == second) return;
        _timelineValue = second;
        _timeline.SetValues((int)Math.Clamp(Time / PreviewDuration * 1000, 0, 1000), 0, 1000, 1);
        SingleComposer.GetRichtext("preview-clock").SetNewText(PreviewClockText(), CairoFont.WhiteSmallText().WithFontSize(12));
    }

    private string PreviewClockText() => Clock(Time) + " / " + Clock(PreviewDuration);
    private static string Clock(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return (seconds >= 3600 ? ((long)time.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + time.Minutes.ToString("00")
            : ((long)time.TotalMinutes).ToString(CultureInfo.InvariantCulture)) + ":" + time.Seconds.ToString("00");
    }

    private string PlacementRangeText() => Number("MaxEnvironmentPlacementDistance") >= 10
        ? "Target at 10 blocks: within placement range." : "Target at 10 blocks: out of placement range.";

    private void RefreshPreview()
    {
        SingleComposer.GetRichtext("preview-placement-range")?.SetNewText(PlacementRangeText(), CairoFont.WhiteSmallText().WithFontSize(14));
        if (CurrentStepId == "chat.languageeditor") _languageEditor.RefreshPreview();
        SingleComposer.GetRichtext("preview-sight")?.SetNewText(SetupWizardTextAppearancePreview.Sight(Draft), CairoFont.WhiteSmallText().WithFontSize(11));
        SingleComposer.GetRichtext("preview-validation")?.SetNewText(SetupWizardTextAppearancePreview.Feedback(Draft, "chat.textappearance"), CairoFont.WhiteSmallText().WithFontSize(12));
        if (SetupWizardVoicePreview.Supports(CurrentStepId)) SingleComposer.GetRichtext("preview-audio")?.SetNewText(SetupWizardVoicePreview.Text(Draft, CurrentStepId, IsCaptureOnly ? null : capi.ModLoader.IsModSystemEnabled("RPTTS.RPTTSAPI")), CairoFont.WhiteSmallText().WithFontSize(15));
        _diagram?.Redraw();
        if (CurrentStepId == "chat.textappearance") _sample?.SetNewText(SetupWizardTextAppearancePreview.Components(capi, Draft));
        else _sample?.SetNewText(BuildPreviewText(), CairoFont.WhiteSmallText().WithFontSize(14), null);
        _sleepCount?.SetNewText(SleepCountText(), CairoFont.WhiteSmallText().WithFontSize(14), null);
        _chatFocus?.Refresh(Draft, Time, AutoSelectChat, AutoOpenChat, _manualChatTab);
        _previewStage?.SetNewText(ChatFocusState.StageText, CairoFont.WhiteSmallText().WithFontSize(14));
        SetupWizardRecordsPreview.Refresh(SingleComposer, CurrentStepId, Draft, Time);
        RefreshPlayback();
        var scroll = SingleComposer.GetCompactScrollbar("preview-chat-scroll");
        if (scroll != null && _sample != null)
            scroll.SetHeights((float)_sample.InsideClipBounds.fixedHeight, (float)_sample.Bounds.fixedHeight);
    }

    private string BuildPreviewText()
    {
        if (CurrentStepId == "chat.textappearance") return string.Join("<br>", SetupWizardTextAppearancePreview.Messages(Draft));
        if (CurrentStepId == "chat.occlusion") return SetupWizardTextAppearancePreview.Feedback(Draft, CurrentStepId).Length > 0 ? "" : SetupWizardSpeechPreview.Message(CurrentStepId, Draft, Time);
        if (SetupWizardChatTextEditor.Supports(CurrentStepId)) return SetupWizardChatTextEditor.Preview(CurrentStepId, Draft);
        if (CurrentStepId == "roleplay.descriptions") return "The old gate creaks in the wind.";
        if (CurrentStepId == "notifications.deaths") return Bool("EnableNearbyDeathMessagesInProximityChat") ? "PipPlayer died nearby." : "Nearby death messages are off.";
        if (CurrentStepId == "chat.colors")
            return ChatHelper.Color("Pip waves.", PreviewColor("EmoteColor")) + "<br>" +
                ChatHelper.Color("(OOC) PipPlayer: One moment.", PreviewColor("OOCColor")) + "<br>" +
                ChatHelper.Color("(GOOC) MaraPlayer: Back in a minute.", PreviewColor("GlobalOOCColor"));
        if (CurrentStepId == "chat.tabs") return ChatFocusState.Messages;
        if (SetupWizardTravelPreview.Supports(CurrentStepId))
            return SetupWizardTravelPreview.Message(CurrentStepId, Draft, Time);
        if (CurrentStepId.StartsWith("chat.", StringComparison.Ordinal))
        {
            if (CurrentStepId == "chat.languageaccess")
                return Bool("EnableLanguageSystem") && !Bool("DisableRPChat") ? "Pip says in Common, \"Hello, traveler!\"" : "";
            var replacement = Bool("UseGeneralChannelAsProximityChat");
            if (!replacement && _chatPreviewTab == 0)
                return "MaraPlayer: Anyone heading north?<br>PipPlayer: Meet you by the gate.";
            if (SetupWizardSpeechPreview.HasScene(CurrentStepId))
                return SetupWizardSpeechPreview.Message(CurrentStepId, Draft, Time);
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
            var speech = CurrentStepId == "chat.language" ? SetupWizardSpeechPreview.Message(CurrentStepId, Draft, Time) : example;
            var lines = speech + "<br><font color=\"#eaf188\">(OOC) " + oocName + ": One moment.</font>";
            if (Bool("EnableGlobalOOC") && !Bool("DisableRPChat"))
                lines += "<br><font color=\"#f1b288\">(GOOC) " + (Bool("UseNicknameInGlobalOOC") ? "Mara" : "MaraPlayer") + ": Back in a minute.</font>";
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
        return "";
    }

    private double Time => _fixedTime ?? _elapsed;
    private bool AutoSelectChat => capi.Settings.Bool.Get("autoChatOpenSelected", true);
    private bool AutoOpenChat => capi.Settings.Bool.Get("autoChat", true);
    private SetupWizardChatFocusPreview.State ChatFocusState => SetupWizardChatFocusPreview.At(Draft, Time, AutoSelectChat, AutoOpenChat, _manualChatTab);
    private bool AnimatedPreview => SetupWizardAppearancePreview.Supports(CurrentStepId) ||
        SetupWizardSpeechPreview.HasScene(CurrentStepId) || SetupWizardTravelPreview.Supports(CurrentStepId) ||
        CurrentStepId is "chat.tabs" or "characters.slots" or "notifications.sleep";
    private double PreviewDuration => SetupWizardAppearancePreview.Supports(CurrentStepId)
        ? SetupWizardAppearancePreview.DurationSeconds(CurrentStepId, Draft)
        : SetupWizardTravelPreview.Supports(CurrentStepId) ? SetupWizardTravelPreview.DurationSeconds(CurrentStepId, Draft)
        : CurrentStepId == "chat.tabs" ? SetupWizardChatFocusPreview.DurationSeconds
        : CurrentStepId is "characters.slots" or "notifications.sleep" ? 8 : 12;
    private int Sleepers => (int)(Time / 2) % 4;
    private string SleepCountText() => $"{Sleepers} of 4 players are asleep.";
    private bool NeedsRestart => _runtimeApplyFailed || _restartKeys.Count > 0;
    private bool CompactLayout => capi.Render.FrameHeight / Math.Max(RuntimeEnv.GUIScale, 0.1f) < 660;
    private double SettingsWidth => _inSettingsForm ? _settingsForm.Width : SetupWizardFeaturePreview.IsSettingsOnly(CurrentStepId) ? Math.Min(546, Width - 32) : Math.Min(546, Width - 334);
    private double SettingsX => _inSettingsForm ? 0 : SetupWizardFeaturePreview.IsSettingsOnly(CurrentStepId) ? (Width - SettingsWidth) / 2 : 334;
    private double FeaturePreviewHeight => CurrentStepId == "map.visibility" ? 270 : 220;
    private double BodyHeight => CompactLayout ? 528 : 608;
    private double FooterY => BodyHeight - 38;
    private double StatusY => FooterY - (CompactLayout ? 42 : 54);
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
    private static string SettingLabel(ConfigAdminSettingDefinition definition) => SetupWizardVoicePreview.Label(definition.Key) ?? (definition.Key switch
    {
        "DisableRPChat" => "Roleplay features",
        "EnableDistanceFontSizeSystem" => "Resize speech with distance",
        "IsolateChatMessageFormatting" => "Keep each message's formatting separate",
        "ProximityChatDefaultFontSize.Whisper" => "Whisper size near speaker",
        "ProximityChatDefaultFontSize.Normal" => "Talk size near speaker",
        "ProximityChatDefaultFontSize.Yell" => "Yell size near speaker",
        "ProximityChatClampFontSizes" => "Allowed sizes (comma-separated)",
        "RequireClearSoundPathForSpeech.Whisper" => "Whisper needs a clear sound path",
        "RequireClearSoundPathForSpeech.Normal" => "Talk needs a clear sound path",
        "RequireClearSoundPathForSpeech.Yell" => "Yell needs a clear sound path",
        "SpeechOcclusionWallPenaltyBlocks" => "Extra distance per sound-blocking block",
        "SightPassThroughBlockCodePatterns" => "See through blocks (domain:pattern, commas)",
        "SightBlockingBlockCodePatterns" => "Sight-blocking blocks (domain:pattern, commas)",
        "SleepNotificationThreshold" => "Sleeping players needed (%)",
        "ProximityChatName" => "Proximity tab name",
        "ProximityChatTabPosition" => "Proximity tab position",
        "PlayerStatSystem" => "Track player activity",
        "PlayerStatClearPermission" => "Who can clear saved statistics",
        "PlayerStatToggles.BlockBreaks" => "Count blocks broken",
        "PlayerStatToggles.Deaths" => "Count deaths",
        "PlayerStatToggles.DistanceTravelled" => "Track distance travelled",
        "PlayerStatToggles.NpcKills" => "Count creature kills",
        "PlayerStatToggles.PlayerKills" => "Count player kills",
        "EnableTh3EssentialsDiscordRelay" => "Allow th3essentials Discord relay",
        "EnableNearbyDeathMessagesInProximityChat" => "Show nearby deaths in Proximity chat",
        "DebugMode" => "Extra diagnostic logging",
        "UseNicknameInGlobalOOC" => "Use character names in global OOC",
        "SetHomeCommandPrivilege" => "Who can set or delete homes",
        "SetSpawnCommandPrivilege" => "Who can set world spawn",
        "ChatHistoryFlushIntervalMilliseconds" => "Write queued history every (milliseconds)",
        "PlayerStatDistanceTravelledTimer" => "Sample movement every (milliseconds)",
        "MaxEnvironmentPlacementDistance" => "Maximum placement distance (blocks)",
        _ => definition.Label
    });
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

    private void DrawSleepScene(Context context, ImageSurface surface, ElementBounds bounds)
    {
        context.Save();
        var scale = Math.Min(surface.Width / 310d, surface.Height / 112d);
        context.Translate((surface.Width - 310 * scale) / 2, (surface.Height - 112 * scale) / 2);
        context.Scale(scale, scale);
        for (var index = 0; index < 4; index++)
        {
            var x = 12 + index % 2 * 154;
            var y = 12 + index / 2 * 53;
            var asleep = index < Sleepers;
            context.SetSourceRGB(0.29, 0.21, 0.14);
            context.Rectangle(x, y + 5, 115, 32); context.Fill();
            context.Rectangle(x + 3, y + 37, 6, 7);
            context.Rectangle(x + 106, y + 37, 6, 7); context.Fill();
            context.SetSourceRGB(0.84, 0.78, 0.63);
            context.Rectangle(x + 4, y + 8, 107, 24); context.Fill();
            context.SetSourceRGB(0.94, 0.89, 0.74);
            context.Rectangle(x + 7, y + 10, 22, 20); context.Fill();
            context.SetSourceRGB(0.25, 0.39, 0.43);
            context.Rectangle(x + 34, y + 9, 75, 23); context.Fill();
            context.SetSourceRGBA(0.78, 0.84, 0.77, 0.5);
            context.Rectangle(x + 36, y + 10, 3, 21); context.Fill();
            if (asleep)
            {
                context.SetSourceRGBA(0.94, 0.89, 0.74, 0.7);
                context.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Normal);
                context.SetFontSize(11); context.MoveTo(x + 25, y + 2); context.ShowText("z");
            }
        }
        context.Restore();
    }

    public override void OnRenderGUI(float deltaTime)
    {
        if (Math.Abs(Width - _composedWidth) > 0.1 || Math.Abs(BodyHeight - _composedHeight) > 0.1)
            ComposeDialog();
        if (!_previewPaused && !_fixedTime.HasValue)
        {
            _elapsed += deltaTime;
            if (AnimatedPreview && _elapsed >= PreviewDuration) _elapsed %= PreviewDuration;
        }
        var beat = (int)(Time / (AnimatedPreview ? 0.1 : 1.5));
        if (beat != _lastBeat)
        {
            _lastBeat = beat;
            RefreshPreview();
        }
        var colorPicker = OpenColorPicker();
        GuiPopupHoverGuard.RenderBackground(SingleComposer, colorPicker != null, () => base.OnRenderGUI(deltaTime));
        if (_searchHighlightSeconds > 0 && SingleComposer.GetElement(_searchHighlight) is { } target)
        {
            _searchHighlightSeconds -= deltaTime;
            if (_searchHighlightSeconds <= 0)
                SingleComposer.GetRichtext("setup-message")?.SetNewText(VtmlUtils.EscapeVtml(_message ?? ""), CairoFont.WhiteSmallText().WithFontSize(14));
            SetupWizardViewportClip.Render(capi, target.InsideClipBounds, () => target.RenderFocusOverlay(deltaTime));
        }
        colorPicker?.RenderPopup(deltaTime);
        if (colorPicker?.IsPositionInside(capi.Input.MouseX, capi.Input.MouseY) == true)
            MouseOverCursor = colorPicker.MouseOverCursor;
        var picker = OpenPrivilegePicker();
        if (picker?.IsPositionInside(capi.Input.MouseX, capi.Input.MouseY) == true)
            MouseOverCursor = picker.MouseOverCursor;
    }

    private GuiElementPrivilegePicker OpenPrivilegePicker() => _privilegePickers.FirstOrDefault(picker => picker.IsSuggestionsOpen);

    private GuiElementColorPicker OpenColorPicker() => _colorPickers.FirstOrDefault(picker => picker.IsOpen) ??
        (SingleComposer?.GetElement("language-editor-color-picker") is GuiElementColorPicker { IsOpen: true } picker ? picker : null);

    private string PreviewColor(string key)
    {
        var value = Draft.Get(key);
        return string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '"', '\'', '<', '>' }) >= 0 ? "#FFFFFF" : value;
    }

    public override bool OnEscapePressed()
    {
        var colorPicker = OpenColorPicker();
        if (colorPicker != null)
        {
            colorPicker.Close();
            SingleComposer.FocusElement(colorPicker.TabIndex);
            return true;
        }
        var picker = OpenPrivilegePicker();
        if (picker == null) return base.OnEscapePressed();
        picker.CloseSuggestions();
        return true;
    }

    public override void OnMouseDown(MouseEvent args)
    {
        OpenColorPicker()?.OnMouseDown(capi, args);
        if (args.Handled) return;
        if (CurrentStepId == "map.visibility")
            foreach (var key in new[] { "MapHideOtherPlayers", "MapPlayerRenderDistance" })
            {
                var control = SingleComposer.GetElement(key);
                if (control is GuiElementControl { Enabled: false } && control.IsPositionInside(args.X, args.Y))
                {
                    // Native text inputs can select text on mouse-down even when disabled.
                    SingleComposer.UnfocusOwnElements();
                    args.Handled = true;
                    return;
                }
            }
        OpenPrivilegePicker()?.OnMouseDown(capi, args);
        if (!args.Handled) base.OnMouseDown(args);
    }

    public override void OnMouseMove(MouseEvent args)
    {
        OpenColorPicker()?.OnMouseMove(capi, args);
        if (args.Handled) return;
        OpenPrivilegePicker()?.OnMouseMove(capi, args);
        if (!args.Handled) base.OnMouseMove(args);
    }

    public override void OnMouseUp(MouseEvent args)
    {
        OpenColorPicker()?.OnMouseUp(capi, args);
        if (!args.Handled) base.OnMouseUp(args);
        FlushNavigation();
    }

    public override void OnKeyDown(KeyEvent args)
    {
        OpenColorPicker()?.OnKeyDown(capi, args);
        if (args.Handled) return;
        if (args.KeyCode == (int)GlKeys.Escape && _returnToSearch && !_searching)
        {
            ShowSearch(); _search.FocusSelection(SingleComposer); args.Handled = true; return;
        }
        var searchKeyboard = _searching && (SingleComposer.GetTextInput("setup-search").HasFocus || _search.HasResultFocus(SingleComposer));
        if (args.KeyCode == (int)GlKeys.Escape && _searching)
        {
            args.Handled = true;
            if (!string.IsNullOrEmpty(_search.Query)) ShowSearch(""); else ShowPage(_searchReturnPage);
            return;
        }
        if (searchKeyboard && args.KeyCode is (int)GlKeys.Down or (int)GlKeys.Up)
        {
            _search.Move(SingleComposer, args.KeyCode == (int)GlKeys.Down ? 1 : -1);
            args.Handled = true; return;
        }
        if (searchKeyboard && args.KeyCode == (int)GlKeys.Enter && _search.Results.Count > 0)
        {
            _search.RememberFocus(SingleComposer);
            FocusSearchResult(_search.Results[_search.Selection]); args.Handled = true; return;
        }
        if (_chatFocus?.SkipClosedTabNavigation(SingleComposer, args) == true) return;
        base.OnKeyDown(args);
        FlushNavigation();
    }

    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        OpenColorPicker()?.OnMouseWheel(capi, args);
        if (args.IsHandled) return;
        OpenPrivilegePicker()?.OnMouseWheel(capi, args);
        if (!args.IsHandled) base.OnMouseWheel(args);
    }

    public override void OnKeyPress(KeyEvent args)
    {
        OpenColorPicker()?.OnKeyPress(capi, args);
        if (!args.Handled) base.OnKeyPress(args);
        FlushNavigation();
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
        _pendingNavigation = null;
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
        _sheetEditor?.Dispose();
        try { _actors?.Dispose(); }
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
            _dialog._preview.Render(_dialog._previewPaused ? 0 : deltaTime, Bounds);
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
