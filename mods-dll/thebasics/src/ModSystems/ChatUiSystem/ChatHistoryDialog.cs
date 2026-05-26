#pragma warning disable S1541 // Dialog composition is UI layout glue.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using thebasics.ModSystems.ChatHistory.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public class ChatHistoryDialog : GuiDialog
{
    private const double DialogWidth = 980;
    private const double ContentWidth = DialogWidth - 48;
    private const double FieldHeight = 28;
    private const double ResultsPanelHeight = 328;
    private const double ResultsHeaderHeight = 36;
    private const double ResultRowHeight = 24;
    private const double ResultsViewportHeight = ResultsPanelHeight - ResultsHeaderHeight - 8;
    private const double DetailViewportHeight = ResultsPanelHeight - 20;
    private const double ListWidth = 475;
    private const double PanelGap = 14;
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private const string SearchInput = "chat-history-search";
    private const string PlayerInput = "chat-history-player";
    private const string KindInput = "chat-history-kind";
    private const string LanguageInput = "chat-history-language";
    private const string FromInput = "chat-history-from";
    private const string ToInput = "chat-history-to";
    private const string LimitInput = "chat-history-limit";

    private readonly Action<TheBasicsChatHistoryQueryRequest> _onQuery;
    private readonly Action<TheBasicsChatHistoryQueryRequest> _onExport;
    private readonly Action _onClose;

    private TheBasicsChatHistoryResultMessage _view;
    private TheBasicsChatHistoryQueryRequest _query;
    private int _selectedIndex;
    private int _liveSearchGeneration;
    private double _resultsScrollY;
    private double _detailsScrollY;
    private SafeGuiElementContainer _resultsScrollContainer;
    private SafeGuiElementContainer _detailsScrollContainer;
    private string _focusedInputCode;
    private int? _focusedInputCaret;
    private string _lastFilterFingerprint = string.Empty;
    private bool _composing;
    private bool _closing;

    public ChatHistoryDialog(
        ICoreClientAPI capi,
        TheBasicsChatHistoryResultMessage view,
        Action<TheBasicsChatHistoryQueryRequest> onQuery,
        Action<TheBasicsChatHistoryQueryRequest> onExport,
        Action onClose)
        : base(capi)
    {
        _onQuery = onQuery;
        _onExport = onExport;
        _onClose = onClose;
        SetViewInternal(view);
        ComposeDialog();
    }

    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override string ToggleKeyCombinationCode => null;
    public override double DrawOrder => 0.29;

    public void SetView(TheBasicsChatHistoryResultMessage view)
    {
        SetViewInternal(view);
        ComposeDialog();
    }

    public override bool TryClose()
    {
        var closed = base.TryClose();
        if (closed && !_closing)
        {
            _closing = true;
            _onClose?.Invoke();
        }

        return closed;
    }

    private void SetViewInternal(TheBasicsChatHistoryResultMessage view)
    {
        var nextView = view ?? new TheBasicsChatHistoryResultMessage { Success = false };
        var nextQuery = CloneQuery(nextView.Query);
        var sameFilters = SameFilters(_query, nextQuery);
        var appendResults = ShouldAppendResults(nextView, nextQuery);
        if (appendResults)
        {
            nextView.Entries = MergeEntries(_view.Entries, nextView.Entries);
            nextView.Offset = 0;
            nextQuery.Offset = 0;
        }
        else if (!sameFilters)
        {
            _resultsScrollY = 0;
            _detailsScrollY = 0;
        }

        _view = nextView;
        _query = nextQuery;
        if (_query.Limit <= 0)
        {
            _query.Limit = DefaultPageSize;
        }

        _lastFilterFingerprint = FilterFingerprint(_query);

        var count = _view.Entries?.Count ?? 0;
        _selectedIndex = count == 0 ? 0 : Math.Clamp(_selectedIndex, 0, count - 1);
    }

    private void ComposeDialog()
    {
        CaptureFocusedInput();
        SingleComposer?.Dispose();
        _composing = true;
        _resultsScrollContainer = null;
        _detailsScrollContainer = null;

        try
        {
            var top = GuiStyle.TitleBarHeight + 10;
            var helpHeight = 24;
            var filterY = top + helpHeight + 8;
            var resultsY = filterY + 132;
            var detailX = ListWidth + PanelGap;
            var detailWidth = ContentWidth - detailX;
            var buttonY = resultsY + ResultsPanelHeight + 12;
            var bodyHeight = buttonY + 38;
            var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
            var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            var composer = capi.Gui.CreateCompo("thebasics-chat-history", dialogBounds)
                .AddShadedDialogBG(bodyBounds)
                .AddDialogTitleBar(Lang.Get("thebasics:chat-history-gui-title"), OnTitleBarCloseClicked)
                .BeginChildElements(bodyBounds)
                .AddInset(ElementBounds.Fixed(0, resultsY, ListWidth, ResultsPanelHeight).FixedGrow(3).WithFixedOffset(-3, -3), 3)
                .AddInset(ElementBounds.Fixed(detailX, resultsY, detailWidth, ResultsPanelHeight).FixedGrow(3).WithFixedOffset(-3, -3), 3);

            AddRichtext(composer, VtmlUtils.EscapeVtml(Lang.Get("thebasics:chat-history-gui-help")), ElementBounds.Fixed(0, top, ContentWidth, helpHeight));
            AddFilters(composer, filterY);
            AddResults(composer, 0, resultsY, ListWidth);
            AddDetails(composer, detailX, resultsY, detailWidth);
            AddButtons(composer, buttonY);

            SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
            SetupResultsScrollbar();
            SetupDetailsScrollbar();
            RestoreInputFocus();
        }
        finally
        {
            _composing = false;
        }
    }

    private void AddFilters(GuiComposer composer, double y)
    {
        AddTextInput(composer, SearchInput, Lang.Get("thebasics:chat-history-gui-search"), _query.SearchText, ElementBounds.Fixed(0, y, 315, FieldHeight), 256);
        AddTextInput(composer, PlayerInput, Lang.Get("thebasics:chat-history-gui-player"), _query.Player, ElementBounds.Fixed(330, y, 150, FieldHeight), 80);
        AddTextInput(composer, KindInput, Lang.Get("thebasics:chat-history-gui-kind"), _query.ChatKind, ElementBounds.Fixed(495, y, 125, FieldHeight), 40);
        AddTextInput(composer, LanguageInput, Lang.Get("thebasics:chat-history-gui-language"), _query.Language, ElementBounds.Fixed(635, y, 140, FieldHeight), 80);

        var secondRowY = y + 54;
        AddTextInput(composer, FromInput, Lang.Get("thebasics:chat-history-gui-from"), _query.FromUtc, ElementBounds.Fixed(0, secondRowY, 210, FieldHeight), 40);
        AddTextInput(composer, ToInput, Lang.Get("thebasics:chat-history-gui-to"), _query.ToUtc, ElementBounds.Fixed(225, secondRowY, 210, FieldHeight), 40);
        AddTextInput(composer, LimitInput, Lang.Get("thebasics:chat-history-gui-limit"), GetLimitText(), ElementBounds.Fixed(450, secondRowY, 60, FieldHeight), 3);

        composer.AddSmallButton(Lang.Get("thebasics:chat-history-gui-reset"), OnReset, ElementBounds.Fixed(530, secondRowY + 20, 120, FieldHeight));
    }

    private void AddResults(GuiComposer composer, double x, double y, double width)
    {
        var header = GetResultsHeader();
        composer.AddStaticText(header, CairoFont.WhiteSmallText(), ElementBounds.Fixed(x + 8, y + 8, width - 16, 22));

        if (!_view.Success)
        {
            AddRichtext(composer, VtmlUtils.EscapeVtml(_view.Message), ElementBounds.Fixed(x + 10, y + 42, width - 20, 90));
            return;
        }

        var entries = _view.Entries ?? new();
        if (entries.Count == 0)
        {
            return;
        }

        var clipBounds = ElementBounds.Fixed(x + 8, y + ResultsHeaderHeight, width - 34, ResultsViewportHeight);
        var contentHeight = Math.Max(ResultsViewportHeight, entries.Count * ResultRowHeight + 6);
        _resultsScrollContainer = new SafeGuiElementContainer(capi, ElementBounds.Fixed(0, -_resultsScrollY, width - 40, contentHeight))
        {
            Tabbable = true,
            unscaledCellSpacing = 0
        };

        composer
            .BeginClip(clipBounds)
            .AddInteractiveElement(_resultsScrollContainer, "chat-history-results-scroll")
            .EndClip()
            .AddVerticalScrollbar(OnResultsScrollbarValue, ElementStdBounds.VerticalScrollbar(clipBounds), "chat-history-results-scrollbar");

        var rowY = 0.0;
        for (var index = 0; index < entries.Count; index++)
        {
            var selected = index == _selectedIndex ? "> " : string.Empty;
            var rowIndex = index;
            AddResultRowButton(
                _resultsScrollContainer,
                selected + TrimText(BuildSummary(entries[index]), 78),
                () => SelectEntry(rowIndex),
                ElementBounds.Fixed(0, rowY, width - 40, 23));
            rowY += ResultRowHeight;
        }
    }

    private void AddDetails(GuiComposer composer, double x, double y, double width)
    {
        var entry = GetSelectedEntry();
        if (entry == null)
        {
            AddRichtext(composer, VtmlUtils.EscapeVtml(Lang.Get("thebasics:chat-history-gui-no-selection")), ElementBounds.Fixed(x + 10, y + 10, width - 20, 70));
            return;
        }

        var clipBounds = ElementBounds.Fixed(x + 10, y + 10, width - 36, DetailViewportHeight);
        var detailVtml = BuildDetailsVtml(entry);
        var contentHeight = Math.Max(DetailViewportHeight, EstimateDetailContentHeight(entry, width - 46));
        _detailsScrollContainer = new SafeGuiElementContainer(capi, ElementBounds.Fixed(0, -_detailsScrollY, clipBounds.fixedWidth - 6, contentHeight))
        {
            Tabbable = false,
            unscaledCellSpacing = 0
        };
        AddRichtext(_detailsScrollContainer, detailVtml, ElementBounds.Fixed(0, 0, clipBounds.fixedWidth - 8, contentHeight));

        composer
            .BeginClip(clipBounds)
            .AddInteractiveElement(_detailsScrollContainer, "chat-history-details-scroll")
            .EndClip()
            .AddVerticalScrollbar(OnDetailsScrollbarValue, ElementStdBounds.VerticalScrollbar(clipBounds), "chat-history-details-scrollbar");
    }

    private void AddButtons(GuiComposer composer, double y)
    {
        var buttonX = 0.0;
        if ((_view.Entries?.Count ?? 0) < _view.TotalMatches)
        {
            composer.AddSmallButton(Lang.Get("thebasics:chat-history-gui-load-more"), OnLoadMore, ElementBounds.Fixed(buttonX, y, 130, 30));
            buttonX += 150;
        }

        if (_view.CanManage)
        {
            composer.AddSmallButton(Lang.Get("thebasics:chat-history-gui-export"), OnExport, ElementBounds.Fixed(buttonX, y, 120, 30));
            buttonX += 140;
        }

        if (!string.IsNullOrWhiteSpace(_view.Message))
        {
            composer.AddStaticText(_view.Message, CairoFont.WhiteSmallText(), ElementBounds.Fixed(buttonX, y + 5, ContentWidth - buttonX - 140, 22));
        }

        composer.AddSmallButton(Lang.Get("thebasics:chat-history-gui-close"), OnCancel, ElementBounds.Fixed(ContentWidth - 120, y, 110, 30));
    }

    private string GetResultsHeader()
    {
        if (!_view.Success)
        {
            return Lang.Get("thebasics:chat-history-gui-error");
        }

        if (_view.TotalMatches == 0)
        {
            return Lang.Get("thebasics:chat-history-no-results");
        }

        return Lang.Get("thebasics:chat-history-gui-result-count", _view.Entries?.Count ?? 0, _view.TotalMatches);
    }

    private bool SelectEntry(int index)
    {
        _query = BuildQueryFromInputs(_view.Offset);
        _selectedIndex = index;
        _detailsScrollY = 0;
        ComposeDialog();
        return true;
    }

    private bool OnReset()
    {
        _onQuery?.Invoke(new TheBasicsChatHistoryQueryRequest { Limit = DefaultPageSize });
        return true;
    }

    private bool OnLoadMore()
    {
        var loaded = _view.Entries?.Count ?? 0;
        if (loaded >= _view.TotalMatches)
        {
            return true;
        }

        _onQuery?.Invoke(BuildQueryFromInputs(loaded));
        return true;
    }

    private bool OnExport()
    {
        if (!_view.CanManage)
        {
            return true;
        }

        var request = BuildQueryFromInputs(_view.Offset);
        request.Offset = 0;
        request.Limit = Math.Max(request.Limit, _view.Entries?.Count ?? 0);
        request.Export = true;
        _onExport?.Invoke(request);
        return true;
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

    private void OnFilterTextChanged(string code)
    {
        if (_composing || _closing)
        {
            return;
        }

        _focusedInputCode = code;
        var generation = ++_liveSearchGeneration;
        capi.Event.RegisterCallback(_ =>
        {
            if (_closing || generation != _liveSearchGeneration || SingleComposer == null)
            {
                return;
            }

            var request = BuildQueryFromInputs(0);
            var fingerprint = FilterFingerprint(request);
            if (string.Equals(fingerprint, _lastFilterFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            _lastFilterFingerprint = fingerprint;
            _onQuery?.Invoke(request);
        }, 450);
    }

    private TheBasicsChatHistoryQueryRequest BuildQueryFromInputs(int offset)
    {
        return new TheBasicsChatHistoryQueryRequest
        {
            SearchText = GetInput(SearchInput),
            Player = GetInput(PlayerInput),
            ChatKind = GetInput(KindInput),
            Language = GetInput(LanguageInput),
            FromUtc = GetInput(FromInput),
            ToUtc = GetInput(ToInput),
            Offset = Math.Max(0, offset),
            Limit = ParsePageSize(GetInput(LimitInput))
        };
    }

    private string GetInput(string code)
    {
        return (SingleComposer?.GetTextInput(code)?.GetText() ?? string.Empty).Trim();
    }

    private string GetLimitText()
    {
        var limit = _query.Limit <= 0 ? DefaultPageSize : Math.Clamp(_query.Limit, 1, MaxPageSize);
        return limit.ToString(CultureInfo.InvariantCulture);
    }

    private static int ParsePageSize(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 1, MaxPageSize)
            : DefaultPageSize;
    }

    private TheBasicsChatHistoryEntryMessage GetSelectedEntry()
    {
        var entries = _view.Entries ?? new();
        return _selectedIndex >= 0 && _selectedIndex < entries.Count ? entries[_selectedIndex] : null;
    }

    private static string BuildSummary(TheBasicsChatHistoryEntryMessage entry)
    {
        var sender = string.IsNullOrWhiteSpace(entry.SenderPlayerName) ? "unknown" : entry.SenderPlayerName;
        var kind = DisplayKind(entry.ChatKind);
        return $"[{ShortDate(entry.TimestampUtc)}] [{kind}] {sender}: {Preview(entry.MessageText)}";
    }

    private static string BuildDetailsVtml(TheBasicsChatHistoryEntryMessage entry)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<font color=\"#f1d18a\" weight=\"bold\">Message Details</font><br>");
        AppendDetailLine(builder, "Time", entry.TimestampUtc);
        AppendDetailLine(builder, "Type", DisplayKind(entry.ChatKind));
        AppendDetailLine(builder, "Source", DisplaySource(entry.Source));
        AppendDetailLine(builder, "Sender", entry.SenderPlayerName);
        if (!string.IsNullOrWhiteSpace(entry.SenderPlayerUid)) AppendDetailLine(builder, "Sender UID", entry.SenderPlayerUid);
        if (!string.IsNullOrWhiteSpace(entry.SenderNickname)) AppendDetailLine(builder, "Nickname", entry.SenderNickname);
        AppendDetailLine(builder, "Channel", $"{entry.ChannelName} ({entry.ChannelId})");
        if (!string.IsNullOrWhiteSpace(entry.ProximityMode)) AppendDetailLine(builder, "Mode", entry.ProximityMode);
        if (!string.IsNullOrWhiteSpace(entry.Language)) AppendDetailLine(builder, "Language", entry.Language);
        AppendDetailLine(builder, "Recipients", $"{entry.RecipientCount} reached, {entry.PendingRecipientCount} pending");
        if (!string.IsNullOrWhiteSpace(entry.SenderPosition)) AppendDetailLine(builder, "Sender position", entry.SenderPosition);
        if (!string.IsNullOrWhiteSpace(entry.PlacedPosition)) AppendDetailLine(builder, "Placed position", entry.PlacedPosition);
        builder.AppendLine("<br><font color=\"#f1d18a\" weight=\"bold\">Message</font><br>");
        builder.AppendLine(VtmlUtils.EscapeVtml(entry.MessageText ?? string.Empty).Replace("\r\n", "<br>").Replace("\n", "<br>").Replace("\r", "<br>"));
        if (!string.IsNullOrWhiteSpace(entry.FormattedMessage))
        {
            builder.AppendLine("<br><font color=\"#f1d18a\" weight=\"bold\">Formatted</font><br>");
            builder.AppendLine(VtmlUtils.EscapeVtml(entry.FormattedMessage).Replace("\r\n", "<br>").Replace("\n", "<br>").Replace("\r", "<br>"));
        }

        if (!string.IsNullOrWhiteSpace(entry.Id))
        {
            builder.AppendLine("<br>");
            AppendDetailLine(builder, "Entry ID", entry.Id);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendDetailLine(StringBuilder builder, string label, string value)
    {
        builder
            .Append("<strong>")
            .Append(VtmlUtils.EscapeVtml(label))
            .Append(":</strong> ")
            .Append(VtmlUtils.EscapeVtml(value ?? string.Empty))
            .Append("<br>");
    }

    private void AddTextInput(GuiComposer composer, string code, string label, string value, ElementBounds bounds, int maxLength)
    {
        composer.AddStaticText(label, CairoFont.WhiteSmallText(), ElementBounds.Fixed(bounds.fixedX, bounds.fixedY, bounds.fixedWidth, 18));
        var inputBounds = ElementBounds.Fixed(bounds.fixedX, bounds.fixedY + 20, bounds.fixedWidth, bounds.fixedHeight);
        var input = new GuiElementTextInput(capi, inputBounds, _ => OnFilterTextChanged(code), CairoFont.TextInput());
        input.SetValue(value ?? string.Empty);
        if (maxLength > 0)
        {
            input.SetMaxLength(maxLength);
        }

        composer.AddInteractiveElement(input, code);
    }

    private void AddResultRowButton(GuiElementContainer container, string text, ActionConsumable onClick, ElementBounds bounds)
    {
        var font = CairoFont.SmallButtonText(EnumButtonStyle.Small).WithOrientation(EnumTextOrientation.Left);
        var hoverFont = CairoFont.SmallButtonText(EnumButtonStyle.Small).WithOrientation(EnumTextOrientation.Left);
        hoverFont.Color = (double[])GuiStyle.ActiveButtonTextColor.Clone();

        var button = new GuiElementTextButton(capi, text, font, hoverFont, onClick, bounds, EnumButtonStyle.Small);
        button.SetOrientation(EnumTextOrientation.Left);
        container.Add(button);
    }

    private void SetupResultsScrollbar()
    {
        var scrollbar = SingleComposer?.GetScrollbar("chat-history-results-scrollbar");
        if (scrollbar == null || _resultsScrollContainer == null)
        {
            return;
        }

        var totalHeight = Math.Max(ResultsViewportHeight, _resultsScrollContainer.Bounds.fixedHeight);
        _resultsScrollY = Math.Clamp(_resultsScrollY, 0, Math.Max(0, totalHeight - ResultsViewportHeight));
        _resultsScrollContainer.Bounds.fixedY = -_resultsScrollY;
        scrollbar.SetHeights((float)ResultsViewportHeight, (float)totalHeight);
        scrollbar.CurrentYPosition = (float)_resultsScrollY;
        RecalculateScrolledBounds(_resultsScrollContainer.Bounds);
    }

    private void SetupDetailsScrollbar()
    {
        var scrollbar = SingleComposer?.GetScrollbar("chat-history-details-scrollbar");
        if (scrollbar == null || _detailsScrollContainer == null)
        {
            return;
        }

        var totalHeight = Math.Max(DetailViewportHeight, _detailsScrollContainer.Bounds.fixedHeight);
        _detailsScrollY = Math.Clamp(_detailsScrollY, 0, Math.Max(0, totalHeight - DetailViewportHeight));
        _detailsScrollContainer.Bounds.fixedY = -_detailsScrollY;
        scrollbar.SetHeights((float)DetailViewportHeight, (float)totalHeight);
        scrollbar.CurrentYPosition = (float)_detailsScrollY;
        RecalculateScrolledBounds(_detailsScrollContainer.Bounds);
    }

    private void OnResultsScrollbarValue(float value)
    {
        if (_resultsScrollContainer == null)
        {
            return;
        }

        _resultsScrollY = Math.Max(0, value);
        _resultsScrollContainer.Bounds.fixedY = -_resultsScrollY;
        RecalculateScrolledBounds(_resultsScrollContainer.Bounds);
    }

    private void OnDetailsScrollbarValue(float value)
    {
        if (_detailsScrollContainer == null)
        {
            return;
        }

        _detailsScrollY = Math.Max(0, value);
        _detailsScrollContainer.Bounds.fixedY = -_detailsScrollY;
        RecalculateScrolledBounds(_detailsScrollContainer.Bounds);
    }

    private void CaptureFocusedInput()
    {
        if (SingleComposer == null)
        {
            return;
        }

        foreach (var code in InputCodes())
        {
            if (SingleComposer.GetTextInput(code) is { HasFocus: true } input)
            {
                _focusedInputCode = code;
                _focusedInputCaret = input.CaretPosWithoutLineBreaks;
                return;
            }
        }
    }

    private void RestoreInputFocus()
    {
        if (string.IsNullOrWhiteSpace(_focusedInputCode) || SingleComposer == null)
        {
            return;
        }

        var tabIndex = GetInputTabIndex(_focusedInputCode);
        if (tabIndex < 0 || !SingleComposer.FocusElement(tabIndex))
        {
            return;
        }

        if (_focusedInputCaret != null && SingleComposer.GetTextInput(_focusedInputCode) is { } input)
        {
            input.CaretPosWithoutLineBreaks = Math.Clamp(_focusedInputCaret.Value, 0, input.TextLengthWithoutLineBreaks);
        }
    }

    private static int GetInputTabIndex(string code)
    {
        return code switch
        {
            SearchInput => 0,
            PlayerInput => 1,
            KindInput => 2,
            LanguageInput => 3,
            FromInput => 4,
            ToInput => 5,
            LimitInput => 6,
            _ => -1
        };
    }

    private static IEnumerable<string> InputCodes()
    {
        yield return SearchInput;
        yield return PlayerInput;
        yield return KindInput;
        yield return LanguageInput;
        yield return FromInput;
        yield return ToInput;
        yield return LimitInput;
    }

    private static void RecalculateScrolledBounds(ElementBounds bounds)
    {
        bounds.MarkDirtyRecursive();
        bounds.CalcWorldBounds();
    }

    private void AddRichtext(GuiComposer composer, string vtmlCode, ElementBounds bounds)
    {
        composer.AddRichtext(VtmlUtil.Richtextify(capi, vtmlCode, CairoFont.WhiteSmallText()), bounds);
    }

    private void AddRichtext(GuiElementContainer container, string vtmlCode, ElementBounds bounds)
    {
        container.Add(new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, vtmlCode, CairoFont.WhiteSmallText()), bounds));
    }

    private static TheBasicsChatHistoryQueryRequest CloneQuery(TheBasicsChatHistoryQueryRequest query)
    {
        query ??= new TheBasicsChatHistoryQueryRequest { Limit = DefaultPageSize };
        return new TheBasicsChatHistoryQueryRequest
        {
            SearchText = query.SearchText ?? string.Empty,
            Player = query.Player ?? string.Empty,
            ChannelId = query.ChannelId,
            HasChannelId = query.HasChannelId,
            ChatKind = query.ChatKind ?? string.Empty,
            ProximityMode = query.ProximityMode ?? string.Empty,
            Language = query.Language ?? string.Empty,
            FromUtc = query.FromUtc ?? string.Empty,
            ToUtc = query.ToUtc ?? string.Empty,
            Offset = Math.Max(0, query.Offset),
            Limit = Math.Clamp(query.Limit <= 0 ? DefaultPageSize : query.Limit, 1, MaxPageSize),
            Export = query.Export
        };
    }

    private bool ShouldAppendResults(TheBasicsChatHistoryResultMessage nextView, TheBasicsChatHistoryQueryRequest nextQuery)
    {
        return nextView.Success &&
               _view?.Success == true &&
               nextQuery.Offset > 0 &&
               SameFilters(_query, nextQuery);
    }

    private static List<TheBasicsChatHistoryEntryMessage> MergeEntries(
        IEnumerable<TheBasicsChatHistoryEntryMessage> current,
        IEnumerable<TheBasicsChatHistoryEntryMessage> next)
    {
        var merged = new List<TheBasicsChatHistoryEntryMessage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in (current ?? Array.Empty<TheBasicsChatHistoryEntryMessage>()).Concat(next ?? Array.Empty<TheBasicsChatHistoryEntryMessage>()))
        {
            if (entry == null)
            {
                continue;
            }

            var key = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id;
            if (seen.Add(key))
            {
                merged.Add(entry);
            }
        }

        return merged;
    }

    private static bool SameFilters(TheBasicsChatHistoryQueryRequest left, TheBasicsChatHistoryQueryRequest right)
    {
        return string.Equals(FilterFingerprint(left), FilterFingerprint(right), StringComparison.Ordinal);
    }

    private static string FilterFingerprint(TheBasicsChatHistoryQueryRequest query)
    {
        query ??= new TheBasicsChatHistoryQueryRequest();
        return string.Join(
            "\u001f",
            query.SearchText ?? string.Empty,
            query.Player ?? string.Empty,
            query.ChatKind ?? string.Empty,
            query.ProximityMode ?? string.Empty,
            query.Language ?? string.Empty,
            query.FromUtc ?? string.Empty,
            query.ToUtc ?? string.Empty,
            query.HasChannelId ? query.ChannelId.ToString(CultureInfo.InvariantCulture) : string.Empty,
            Math.Clamp(query.Limit <= 0 ? DefaultPageSize : query.Limit, 1, MaxPageSize).ToString(CultureInfo.InvariantCulture));
    }

    private static string ShortDate(string utc)
    {
        return DateTime.TryParse(
            utc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "unknown";
    }

    private static string DisplayKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "Unknown";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(kind.Replace('-', ' ').Replace('_', ' '));
    }

    private static string DisplaySource(string source)
    {
        return string.Equals(source, "thebasics", StringComparison.OrdinalIgnoreCase)
            ? "The BASICs"
            : DisplayKind(source);
    }

    private static string Preview(string value)
    {
        value = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length > 90 ? value[..90] + "..." : value;
    }

    private static string TrimText(string value, int maxLength)
    {
        value = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length > maxLength ? value[..Math.Max(0, maxLength - 3)] + "..." : value;
    }

    private static double EstimateDetailContentHeight(TheBasicsChatHistoryEntryMessage entry, double width)
    {
        var charsPerLine = Math.Max(28, (int)(width / 7.0));
        var lines = 16;
        lines += EstimateWrappedLines(entry.MessageText, charsPerLine) + 2;
        if (!string.IsNullOrWhiteSpace(entry.FormattedMessage))
        {
            lines += EstimateWrappedLines(entry.FormattedMessage, charsPerLine) + 2;
        }

        return lines * 18 + 20;
    }

    private static int EstimateWrappedLines(string value, int charsPerLine)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 1;
        }

        return value.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Sum(line => Math.Max(1, (line.Length + charsPerLine - 1) / charsPerLine));
    }

    private sealed class SafeGuiElementContainer : GuiElementContainer
    {
        public SafeGuiElementContainer(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds)
        {
        }

        public override void OnFocusGained()
        {
            if (FirstTabbableElement == null)
            {
                return;
            }

            base.OnFocusGained();
        }
    }
}
