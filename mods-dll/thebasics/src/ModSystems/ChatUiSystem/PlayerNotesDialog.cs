#pragma warning disable S1450 // Deferred input values bridge GUI element creation and post-compose value application.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using thebasics.ModSystems.Notes.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public class PlayerNotesDialog : GuiDialog
{
    private const double DialogWidth = 860;
    private const double ContentWidth = DialogWidth - 48;
    private const double EntryPanelHeight = 330;
    private const double EntryBodyHeight = 205;
    private const double FreeformChromeHeight = 48;
    private const double FreeformViewportHeight = 310;
    private const double FreeformLineHeight = 16;
    private const double FreeformContentPadding = 14;
    private const double FieldHeight = 28;
    private const int MaxEntryTitleLength = 120;
    private const int DefaultMaxNoteLength = 20000;
    private const int DefaultMaxFreeformNoteLength = 20000;
    private const int ExpandedListEntryCount = 5;
    private const string FreeformScrollbarKey = "notes-freeform-scrollbar";
    private const string ScopeAdmin = PlayerNotesConstants.ScopeAdmin;

    private readonly Action<TheBasicsNotesSaveMessage> _onSave;
    private readonly Action<TheBasicsNotesSaveMessage> _onReload;
    private readonly Action _onClose;

    private TheBasicsNotesViewMessage _view;
    private List<PlayerNoteEntryMessage> _adminNotes = new();
    private List<PlayerNoteEntryMessage> _personalNotes = new();
    private AdminNoteLedgerMessage _adminLedger = new();
    private PersonalNoteLedgerMessage _personalLedger = new();
    private int _selectedIndex;
    private int _listOffset;
    private string _lastLoadedSnapshot;
    private string _localMessage;
    private GuiElementTextInput _titleInput;
    private GuiElementTextArea _bodyInput;
    private GuiElementTextArea _freeformInput;
    private double _freeformScrollViewportHeight;
    private double _freeformScrollY;
    private string _deferredTitleValue;
    private string _deferredBodyValue;
    private string _deferredFreeformValue;
    private bool _scrollFreeformToBottomAfterCompose;
    private bool _suppressFreeformCaretScroll;
    private bool _suppressFreeformScrollCapture;
    private bool _forceClose;
    private bool _closing;
    private GuiDialogConfirm _unsavedCloseConfirm;
    private GuiDialogConfirm _unsavedReloadConfirm;

    public PlayerNotesDialog(ICoreClientAPI capi, TheBasicsNotesViewMessage view, Action<TheBasicsNotesSaveMessage> onSave, Action<TheBasicsNotesSaveMessage> onReload, Action onClose)
        : base(capi)
    {
        _onSave = onSave;
        _onReload = onReload;
        _onClose = onClose;
        SetDraft(view, updateBaseline: true);
        _scrollFreeformToBottomAfterCompose = false;
        ComposeDialog();
    }

    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override string ToggleKeyCombinationCode => null;
    public override double DrawOrder => 0.28;

    public void SetView(TheBasicsNotesViewMessage view)
    {
        var sameView = IsSameNotesView(view);
        if (sameView)
        {
            _freeformScrollY = GetFreeformScrollY();
        }
        else
        {
            _freeformScrollY = 0;
            _scrollFreeformToBottomAfterCompose = false;
        }

        if (sameView && view?.Success == false && _view?.Success == true)
        {
            CaptureCurrentInputs();
            _localMessage = view.Message;
            ComposeDialog();
            return;
        }

        SetDraft(view, updateBaseline: view?.Success == true);
        ComposeDialog();
    }

    public override bool TryClose()
    {
        if (!_forceClose)
        {
            CaptureCurrentInputs();
            if (HasUnsavedChanges())
            {
                ConfirmCloseWithUnsavedChanges();
                return false;
            }
        }

        var closed = base.TryClose();
        if (closed && !_closing)
        {
            _closing = true;
            _onClose?.Invoke();
        }

        return closed;
    }

    private void SetDraft(TheBasicsNotesViewMessage view, bool updateBaseline)
    {
        _view = view ?? new TheBasicsNotesViewMessage { Success = false };
        _adminNotes = (_view.AdminNotes ?? new List<PlayerNoteEntryMessage>()).Select(CloneNote).ToList();
        _personalNotes = (_view.PersonalNotes ?? new List<PlayerNoteEntryMessage>()).Select(CloneNote).ToList();
        _adminLedger = CloneLedger(_view.AdminLedger);
        _personalLedger = ClonePersonalLedger(_view.PersonalLedger);
        _selectedIndex = Clamp(_selectedIndex, CurrentNotes().Count);
        NormalizeListOffset();
        if (updateBaseline)
        {
            _localMessage = null;
            _lastLoadedSnapshot = SnapshotDraft();
        }
    }

    private bool IsSameNotesView(TheBasicsNotesViewMessage next)
    {
        return next != null && _view != null &&
            string.Equals(_view.Scope, next.Scope, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_view.TargetPlayerUid, next.TargetPlayerUid, StringComparison.OrdinalIgnoreCase);
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();
        ResetDeferredInputs();

        var top = GuiStyle.TitleBarHeight + 12;
        var leftWidth = 275;
        var rightX = leftWidth + 15;
        var rightWidth = ContentWidth - rightX;
        var entriesY = top + 20;
        var freeformHeight = FreeformViewportHeight + FreeformChromeHeight;
        var freeformY = entriesY + EntryPanelHeight + 22;
        var statusMessage = StatusMessage();
        var statusHeight = string.IsNullOrWhiteSpace(statusMessage) ? 0 : 24;
        var statusY = freeformY + freeformHeight + 8;
        var buttonY = statusY + statusHeight + 6;
        var bodyHeight = buttonY + 38;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        var composer = capi.Gui.CreateCompo("thebasics-player-notes", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(GetTitle(), OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddInset(ElementBounds.Fixed(0, entriesY, leftWidth, EntryPanelHeight).FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddInset(ElementBounds.Fixed(rightX, entriesY, rightWidth, EntryPanelHeight).FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddInset(ElementBounds.Fixed(0, freeformY, ContentWidth, freeformHeight).FixedGrow(3).WithFixedOffset(-3, -3), 3);

        AddDialogContent(composer, entriesY, leftWidth, rightX, rightWidth, freeformY);

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            composer.AddStaticText(statusMessage, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, statusY, ContentWidth, 20));
        }

        composer
            .AddSmallButton(Lang.Get("thebasics:notes-reload"), OnReload, ElementBounds.Fixed(0, buttonY, 110, 30))
            .AddSmallButton(Lang.Get("thebasics:guide-button"), OnGuide, ElementBounds.Fixed(120, buttonY, 110, 30))
            .AddSmallButton(Lang.Get("thebasics:notes-save"), OnSave, ElementBounds.Fixed(ContentWidth - 250, buttonY, 110, 30))
            .AddSmallButton(Lang.Get("thebasics:notes-close"), OnCancel, ElementBounds.Fixed(ContentWidth - 125, buttonY, 110, 30));

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
        ApplyDeferredInputValues();
    }

    private void ResetDeferredInputs()
    {
        _titleInput = null;
        _bodyInput = null;
        _freeformInput = null;
        _deferredTitleValue = null;
        _deferredBodyValue = null;
        _deferredFreeformValue = null;
    }

    private void AddDialogContent(GuiComposer composer, double entriesY, double leftWidth, double rightX, double rightWidth, double freeformY)
    {
        if (!_view.Success)
        {
            AddRichtext(composer, VtmlUtils.EscapeVtml(_view.Message), ElementBounds.Fixed(10, entriesY + 12, ContentWidth - 20, 80));
            return;
        }

        AddNoteList(composer, 0, entriesY, leftWidth);
        AddNoteEditor(composer, rightX, entriesY, rightWidth);
        AddFreeformEditor(composer, 0, freeformY, ContentWidth);
    }

    private void ApplyDeferredInputValues()
    {
        if (_titleInput != null && _deferredTitleValue != null)
        {
            _titleInput.SetValue(_deferredTitleValue);
        }

        if (_bodyInput != null && _deferredBodyValue != null)
        {
            _bodyInput.SetValue(_deferredBodyValue);
        }

        if (_freeformInput != null && _deferredFreeformValue != null)
        {
            var scrollY = _freeformScrollY;
            _suppressFreeformCaretScroll = true;
            _suppressFreeformScrollCapture = true;
            try
            {
                _freeformInput.SetValue(_deferredFreeformValue, setCaretPosToEnd: false);
                UpdateFreeformContentHeight();
                RefreshFreeformScrollHeight();
            }
            finally
            {
                _suppressFreeformCaretScroll = false;
                _suppressFreeformScrollCapture = false;
            }

            _freeformScrollY = scrollY;
            ApplyFreeformScrollAfterCompose();
        }
    }

    private string GetTitle()
    {
        var target = string.IsNullOrWhiteSpace(_view.TargetPlayerName) ? Lang.Get("thebasics:notes-self-target") : _view.TargetPlayerName;
        return IsAdminScope()
            ? Lang.Get("thebasics:notes-title-admin", target)
            : Lang.Get("thebasics:notes-title-personal", target);
    }

    private void AddNoteList(GuiComposer composer, double x, double y, double width)
    {
        var notes = CurrentNotes();
        composer.AddStaticText(Lang.Get("thebasics:notes-entries"), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), ElementBounds.Fixed(x + 8, y + 8, width - 16, 22));

        if (CanEditEntries())
        {
            composer
                .AddSmallButton(Lang.Get("thebasics:notes-add"), OnAddNote, ElementBounds.Fixed(x + 8, y + 36, 120, 28))
                .AddSmallButton(Lang.Get("thebasics:notes-delete"), OnDeleteNote, ElementBounds.Fixed(x + 138, y + 36, 120, 28));
        }

        if (notes.Count == 0)
        {
            AddRichtext(composer, VtmlUtils.EscapeVtml(Lang.Get("thebasics:notes-list-empty-short")), ElementBounds.Fixed(x + 10, y + 82, width - 20, 45));
            return;
        }

        NormalizeListOffset();
        var visibleCount = VisibleEntryCount(notes.Count);
        var lastVisibleIndex = Math.Min(notes.Count, _listOffset + visibleCount);
        var rowY = y + 76;
        for (var index = _listOffset; index < lastVisibleIndex; index++)
        {
            var selected = index == _selectedIndex ? "> " : string.Empty;
            composer.AddSmallButton(selected + TrimText(NoteSummary(notes[index], index), 34), () => SelectNote(index), ElementBounds.Fixed(x + 8, rowY, width - 16, 25));
            rowY += 28;
        }

        if (notes.Count > visibleCount)
        {
            var rangeText = Lang.Get("thebasics:notes-entry-range", _listOffset + 1, lastVisibleIndex, notes.Count);
            composer
                .AddSmallButton(Lang.Get("thebasics:notes-previous"), OnPreviousNotesPage, ElementBounds.Fixed(x + 8, rowY + 4, 70, 24))
                .AddStaticText(rangeText, CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + 86, rowY + 8, width - 172, 18))
                .AddSmallButton(Lang.Get("thebasics:notes-next"), OnNextNotesPage, ElementBounds.Fixed(x + width - 78, rowY + 4, 70, 24));
        }
    }

    private void AddNoteEditor(GuiComposer composer, double x, double y, double width)
    {
        var note = CurrentNote();
        if (note == null)
        {
            AddRichtext(composer, VtmlUtils.EscapeVtml(Lang.Get("thebasics:notes-no-note-selected")), ElementBounds.Fixed(x + 10, y + 12, width - 20, 80));
            return;
        }

        composer.AddStaticText(EntryMeta(note), CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + 10, y + 10, width - 20, 24));
        composer.AddStaticText(Lang.Get("thebasics:notes-entry-title"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + 10, y + 36, width - 20, 20));
        _titleInput = new GuiElementTextInput(capi, ElementBounds.Fixed(x + 10, y + 56, width - 20, FieldHeight), null, CairoFont.TextInput());
        _titleInput.SetMaxLength(MaxEntryTitleLength);
        _deferredTitleValue = note.Title ?? string.Empty;
        composer.AddInteractiveElement(_titleInput, "notes-title");

        composer.AddStaticText(Lang.Get("thebasics:notes-entry-content"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + 10, y + 90, width - 20, 20));
        _bodyInput = new GuiElementTextArea(capi, ElementBounds.Fixed(x + 10, y + 110, width - 20, EntryBodyHeight), null, CairoFont.TextInput())
        {
            Autoheight = false
        };
        _bodyInput.SetMaxLines(8);
        _bodyInput.SetMaxLength(EffectiveMaxNoteLength());
        _deferredBodyValue = note.Text ?? string.Empty;
        composer.AddInteractiveElement(_bodyInput, "notes-body");
    }

    private void AddFreeformEditor(GuiComposer composer, double x, double y, double width)
    {
        composer.AddStaticText(FreeformLabel(), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), ElementBounds.Fixed(x + 10, y + 8, width - 20, 22));
        _freeformScrollViewportHeight = FreeformViewportHeight;
        var clipBounds = ElementBounds.Fixed(x + 10, y + 36, width - 36, _freeformScrollViewportHeight);
        _freeformInput = new GuiElementTextArea(capi, ElementBounds.Fixed(0, 0, clipBounds.fixedWidth - 6, _freeformScrollViewportHeight), OnFreeformTextChanged, CairoFont.TextInput())
        {
            Autoheight = false
        };
        _freeformInput.SetMaxLines(9999);
        _freeformInput.SetMaxLength(EffectiveMaxFreeformNoteLength());
        _freeformInput.OnCaretPositionChanged = OnFreeformCaretMoved;
        _deferredFreeformValue = CurrentFreeformText();

        composer
            .BeginClip(clipBounds)
            .AddInteractiveElement(_freeformInput, "notes-freeform")
            .EndClip()
            .AddVerticalScrollbar(OnFreeformScrollbarValue, ElementStdBounds.VerticalScrollbar(clipBounds), FreeformScrollbarKey);
    }

    private string EntryMeta(PlayerNoteEntryMessage note)
    {
        if (string.IsNullOrWhiteSpace(note.Id))
        {
            return Lang.Get("thebasics:notes-new-entry");
        }

        return IsAdminScope()
            ? Lang.Get("thebasics:notes-meta-admin", note.AuthorName, ShortDate(note.CreatedUtc), ShortDate(note.UpdatedUtc))
            : Lang.Get("thebasics:notes-meta-personal", ShortDate(note.CreatedUtc), ShortDate(note.UpdatedUtc));
    }

    private string FreeformLabel()
    {
        return IsAdminScope()
            ? Lang.Get("thebasics:notes-freeform-admin")
            : Lang.Get("thebasics:notes-freeform-personal");
    }

    private string StatusMessage()
    {
        if (!string.IsNullOrWhiteSpace(_localMessage))
        {
            return _localMessage;
        }

        var message = _view?.Message ?? string.Empty;
        return string.IsNullOrWhiteSpace(message) || string.Equals(message, Lang.Get("thebasics:notes-status-opened"), StringComparison.Ordinal)
            ? null
            : message;
    }

    private void OnFreeformTextChanged(string text)
    {
        SetCurrentFreeformText(text ?? string.Empty);
        UpdateFreeformContentHeight();
        RefreshFreeformScrollHeight();
    }

    private void UpdateFreeformContentHeight()
    {
        if (_freeformInput == null)
        {
            return;
        }

        var lineCount = Math.Max(1, _freeformInput.GetLines().Count);
        var contentHeight = Math.Max(_freeformScrollViewportHeight, 2 * FreeformContentPadding + lineCount * GetFreeformLineHeight(_freeformInput.Font));
        _freeformInput.Bounds.fixedHeight = Math.Ceiling(contentHeight);
        _freeformInput.Bounds.CalcWorldBounds();
    }

    private void RefreshFreeformScrollHeight()
    {
        if (_freeformInput == null || SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetScrollbar(FreeformScrollbarKey).SetHeights((float)_freeformScrollViewportHeight, (float)Math.Max(_freeformScrollViewportHeight, _freeformInput.Bounds.fixedHeight));
    }

    private void ApplyFreeformScrollAfterCompose()
    {
        if (_freeformInput == null || SingleComposer == null)
        {
            return;
        }

        if (_scrollFreeformToBottomAfterCompose)
        {
            SingleComposer.GetScrollbar(FreeformScrollbarKey).ScrollToBottom();
            _freeformScrollY = GetFreeformScrollY();
            _scrollFreeformToBottomAfterCompose = false;
            return;
        }

        RestoreFreeformScroll(_freeformScrollY);
    }

    private void RestoreFreeformScroll(double scrollY)
    {
        if (_freeformInput == null || SingleComposer == null)
        {
            return;
        }

        var maxScroll = Math.Max(0, _freeformInput.Bounds.fixedHeight - _freeformScrollViewportHeight);
        var clamped = Math.Clamp(scrollY, 0, maxScroll);
        var scrollbar = SingleComposer.GetScrollbar(FreeformScrollbarKey);
        scrollbar.CurrentYPosition = (float)clamped;
        scrollbar.TriggerChanged();
    }

    private double GetFreeformScrollY()
    {
        if (_freeformInput != null && SingleComposer != null)
        {
            return Math.Max(0, SingleComposer.GetScrollbar(FreeformScrollbarKey).CurrentYPosition);
        }

        return _freeformInput == null ? _freeformScrollY : Math.Max(0, 1 - _freeformInput.Bounds.fixedY);
    }

    private void OnFreeformCaretMoved(int posLine, int posInLine)
    {
        if (_suppressFreeformCaretScroll || _freeformInput == null || SingleComposer == null)
        {
            return;
        }

        var lineHeight = GetFreeformLineHeight(_freeformInput.Font);
        var y = posLine * lineHeight;
        var scrollbar = SingleComposer.GetScrollbar(FreeformScrollbarKey);
        scrollbar.EnsureVisible(0, y);
        scrollbar.EnsureVisible(0, y + lineHeight + 5);
    }

    private static double GetFreeformLineHeight(CairoFont font)
    {
        if (font == null || RuntimeEnv.GUIScale <= 0)
        {
            return FreeformLineHeight;
        }

        return Math.Max(12, font.GetFontExtents().Height * font.LineHeightMultiplier / RuntimeEnv.GUIScale);
    }

    private void OnFreeformScrollbarValue(float value)
    {
        if (_freeformInput == null)
        {
            return;
        }

        if (!_suppressFreeformScrollCapture)
        {
            _freeformScrollY = Math.Max(0, value);
        }

        _freeformInput.Bounds.fixedY = 1 - _freeformScrollY;
        _freeformInput.Bounds.CalcWorldBounds();
    }

    private void CaptureCurrentInputs()
    {
        var note = CurrentNote();
        if (note != null && _titleInput != null)
        {
            note.Title = (_titleInput.GetText() ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        if (note != null && _bodyInput != null)
        {
            note.Text = _bodyInput.GetText() ?? string.Empty;
        }

        if (_freeformInput != null)
        {
            SetCurrentFreeformText(_freeformInput.GetText() ?? string.Empty);
            _freeformScrollY = GetFreeformScrollY();
        }
    }

    private bool OnAddNote()
    {
        CaptureCurrentInputs();
        _localMessage = null;
        CurrentNotes().Insert(0, new PlayerNoteEntryMessage
        {
            Kind = IsAdminScope() ? PlayerNotesConstants.KindAdmin : PlayerNotesConstants.KindPersonal,
            TargetPlayerUid = _view.TargetPlayerUid,
            TargetPlayerName = _view.TargetPlayerName,
            AuthorName = Lang.Get("thebasics:notes-unsaved-author"),
            Title = string.Empty,
            Text = string.Empty
        });
        _selectedIndex = 0;
        _listOffset = 0;
        ComposeDialog();
        return true;
    }

    private bool OnDeleteNote()
    {
        CaptureCurrentInputs();
        var notes = CurrentNotes();
        if (_selectedIndex >= 0 && _selectedIndex < notes.Count)
        {
            _localMessage = null;
            notes.RemoveAt(_selectedIndex);
            _selectedIndex = Clamp(_selectedIndex, notes.Count);
            NormalizeListOffset();
            ComposeDialog();
        }

        return true;
    }

    private bool SelectNote(int index)
    {
        CaptureCurrentInputs();
        _localMessage = null;
        _selectedIndex = Clamp(index, CurrentNotes().Count);
        NormalizeListOffset();
        ComposeDialog();
        return true;
    }

    private bool OnPreviousNotesPage()
    {
        CaptureCurrentInputs();
        PageNotes(-1);
        return true;
    }

    private bool OnNextNotesPage()
    {
        CaptureCurrentInputs();
        PageNotes(1);
        return true;
    }

    private void PageNotes(int direction)
    {
        var notes = CurrentNotes();
        var visibleCount = VisibleEntryCount(notes.Count);
        _listOffset = Math.Clamp(_listOffset + direction * visibleCount, 0, MaxListOffset(notes.Count, visibleCount));
        _selectedIndex = Clamp(_listOffset, notes.Count);
        _localMessage = null;
        ComposeDialog();
    }

    private bool OnSave()
    {
        CaptureCurrentInputs();
        if (CurrentNotes().Any(IsBlankEntry))
        {
            _localMessage = Lang.Get("thebasics:notes-empty-entry-warning");
            ComposeDialog();
            return true;
        }

        _localMessage = null;
        _onSave?.Invoke(new TheBasicsNotesSaveMessage
        {
            Scope = _view.Scope,
            TargetPlayerUid = _view.TargetPlayerUid,
            AdminNotes = _adminNotes.Select(CloneNote).ToList(),
            AdminLedger = CloneLedger(_adminLedger),
            PersonalNotes = _personalNotes.Select(CloneNote).ToList(),
            PersonalLedger = ClonePersonalLedger(_personalLedger)
        });
        return true;
    }

    private bool OnReload()
    {
        CaptureCurrentInputs();
        if (HasUnsavedChanges())
        {
            ConfirmReloadWithUnsavedChanges();
            return true;
        }

        SendReload();
        return true;
    }

    private bool OnGuide()
    {
        return HandbookGuide.Open(capi, HandbookGuide.NotesPage);
    }

    private void SendReload()
    {
        _onReload?.Invoke(new TheBasicsNotesSaveMessage
        {
            Scope = _view.Scope,
            TargetPlayerUid = _view.TargetPlayerUid,
            Reload = true
        });
    }

    private bool OnCancel()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarCloseClicked()
    {
        TryClose();
    }

    private bool HasUnsavedChanges()
    {
        return !string.Equals(SnapshotDraft(), _lastLoadedSnapshot ?? string.Empty, StringComparison.Ordinal);
    }

    private void ConfirmCloseWithUnsavedChanges()
    {
        if (_unsavedCloseConfirm?.IsOpened() == true)
        {
            return;
        }

        _unsavedCloseConfirm = new GuiDialogConfirm(capi, Lang.Get("thebasics:notes-close-unsaved-confirm"), ok =>
        {
            if (!ok) return;
            _forceClose = true;
            try
            {
                TryClose();
            }
            finally
            {
                _forceClose = false;
            }
        });
        _unsavedCloseConfirm.TryOpen();
    }

    private void ConfirmReloadWithUnsavedChanges()
    {
        if (_unsavedReloadConfirm?.IsOpened() == true)
        {
            return;
        }

        _unsavedReloadConfirm = new GuiDialogConfirm(capi, Lang.Get("thebasics:notes-reload-unsaved-confirm"), ok =>
        {
            if (ok)
            {
                SendReload();
            }
        });
        _unsavedReloadConfirm.TryOpen();
    }

    private string SnapshotDraft()
    {
        return JsonConvert.SerializeObject(new
        {
            Admin = _adminNotes.Select(NormalizeForSnapshot),
            AdminFreeform = _adminLedger.Text ?? string.Empty,
            Personal = _personalNotes.Select(NormalizeForSnapshot),
            PersonalFreeform = _personalLedger.Text ?? string.Empty
        });
    }

    private static object NormalizeForSnapshot(PlayerNoteEntryMessage note)
    {
        return new
        {
            Id = note.Id ?? string.Empty,
            Title = note.Title ?? string.Empty,
            Text = note.Text ?? string.Empty
        };
    }

    private static bool IsBlankEntry(PlayerNoteEntryMessage note)
    {
        return note != null && string.IsNullOrWhiteSpace(note.Title) && string.IsNullOrWhiteSpace(note.Text);
    }

    private bool IsAdminScope()
    {
        return string.Equals(_view.Scope, ScopeAdmin, StringComparison.OrdinalIgnoreCase);
    }

    private bool CanEditEntries()
    {
        return IsAdminScope() ? _view.CanEditAdminNotes : _view.CanEditPersonalNotes;
    }

    private List<PlayerNoteEntryMessage> CurrentNotes()
    {
        return IsAdminScope() ? _adminNotes : _personalNotes;
    }

    private PlayerNoteEntryMessage CurrentNote()
    {
        var notes = CurrentNotes();
        return _selectedIndex >= 0 && _selectedIndex < notes.Count ? notes[_selectedIndex] : null;
    }

    private string CurrentFreeformText()
    {
        return IsAdminScope() ? _adminLedger.Text ?? string.Empty : _personalLedger.Text ?? string.Empty;
    }

    private void SetCurrentFreeformText(string text)
    {
        if (IsAdminScope())
        {
            _adminLedger.Text = text ?? string.Empty;
        }
        else
        {
            _personalLedger.Text = text ?? string.Empty;
        }
    }

    private int EffectiveMaxNoteLength()
    {
        return _view?.MaxNoteLength > 0 ? _view.MaxNoteLength : DefaultMaxNoteLength;
    }

    private int EffectiveMaxFreeformNoteLength()
    {
        return _view?.MaxFreeformNoteLength > 0 ? _view.MaxFreeformNoteLength : DefaultMaxFreeformNoteLength;
    }

    private void NormalizeListOffset()
    {
        var count = CurrentNotes().Count;
        if (count == 0)
        {
            _listOffset = 0;
            return;
        }

        var visibleCount = VisibleEntryCount(count);
        _listOffset = Math.Clamp(_listOffset, 0, MaxListOffset(count, visibleCount));
        if (_selectedIndex < _listOffset)
        {
            _listOffset = Math.Clamp(_selectedIndex, 0, MaxListOffset(count, visibleCount));
        }
        else if (_selectedIndex >= _listOffset + visibleCount)
        {
            _listOffset = Math.Clamp(_selectedIndex - visibleCount + 1, 0, MaxListOffset(count, visibleCount));
        }
    }

    private static int VisibleEntryCount(int count)
    {
        return count > 6 ? ExpandedListEntryCount : Math.Min(count, 6);
    }

    private static int MaxListOffset(int count, int visibleCount)
    {
        return Math.Max(0, count - visibleCount);
    }

    private static int Clamp(int index, int count)
    {
        return count <= 0 ? 0 : Math.Clamp(index, 0, count - 1);
    }

    private static string NoteSummary(PlayerNoteEntryMessage note, int index)
    {
        var text = string.IsNullOrWhiteSpace(note.Title)
            ? (note.Text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim()
            : note.Title.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = Lang.Get("thebasics:notes-empty-draft");
        }

        return $"{index + 1}. {text}";
    }

    private static string ShortDate(string utc)
    {
        return DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : Lang.Get("thebasics:notes-unsaved-date");
    }

    private static string TrimText(string text, int max)
    {
        text ??= string.Empty;
        return text.Length <= max ? text : text.Substring(0, max - 3) + "...";
    }

    private static PlayerNoteEntryMessage CloneNote(PlayerNoteEntryMessage note)
    {
        if (note == null)
        {
            return new PlayerNoteEntryMessage();
        }

        return new PlayerNoteEntryMessage
        {
            Id = EmptyIfNull(note.Id),
            Kind = EmptyIfNull(note.Kind),
            AuthorPlayerUid = EmptyIfNull(note.AuthorPlayerUid),
            AuthorName = EmptyIfNull(note.AuthorName),
            TargetPlayerUid = EmptyIfNull(note.TargetPlayerUid),
            TargetPlayerName = EmptyIfNull(note.TargetPlayerName),
            TargetCharacterId = EmptyIfNull(note.TargetCharacterId),
            TargetCharacterName = EmptyIfNull(note.TargetCharacterName),
            CreatedUtc = EmptyIfNull(note.CreatedUtc),
            UpdatedUtc = EmptyIfNull(note.UpdatedUtc),
            Title = EmptyIfNull(note.Title),
            Text = EmptyIfNull(note.Text)
        };
    }

    private static string EmptyIfNull(string value)
    {
        return value ?? string.Empty;
    }

    private static AdminNoteLedgerMessage CloneLedger(AdminNoteLedgerMessage ledger)
    {
        return ledger == null ? new AdminNoteLedgerMessage() : new AdminNoteLedgerMessage
        {
            TargetPlayerUid = ledger.TargetPlayerUid ?? string.Empty,
            TargetPlayerName = ledger.TargetPlayerName ?? string.Empty,
            TargetCharacterId = ledger.TargetCharacterId ?? string.Empty,
            TargetCharacterName = ledger.TargetCharacterName ?? string.Empty,
            UpdatedUtc = ledger.UpdatedUtc ?? string.Empty,
            UpdatedByPlayerUid = ledger.UpdatedByPlayerUid ?? string.Empty,
            UpdatedByName = ledger.UpdatedByName ?? string.Empty,
            Text = ledger.Text ?? string.Empty
        };
    }

    private static PersonalNoteLedgerMessage ClonePersonalLedger(PersonalNoteLedgerMessage ledger)
    {
        return ledger == null ? new PersonalNoteLedgerMessage() : new PersonalNoteLedgerMessage
        {
            AuthorPlayerUid = ledger.AuthorPlayerUid ?? string.Empty,
            TargetPlayerUid = ledger.TargetPlayerUid ?? string.Empty,
            TargetPlayerName = ledger.TargetPlayerName ?? string.Empty,
            TargetCharacterId = ledger.TargetCharacterId ?? string.Empty,
            TargetCharacterName = ledger.TargetCharacterName ?? string.Empty,
            UpdatedUtc = ledger.UpdatedUtc ?? string.Empty,
            Text = ledger.Text ?? string.Empty
        };
    }

    private void AddRichtext(GuiComposer composer, string vtmlCode, ElementBounds bounds)
    {
        composer.AddRichtext(VtmlUtil.Richtextify(capi, vtmlCode, CairoFont.WhiteSmallText()), bounds);
    }
}
