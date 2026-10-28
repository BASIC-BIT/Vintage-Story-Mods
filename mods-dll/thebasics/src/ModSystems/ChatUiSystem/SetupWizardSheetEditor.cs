using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.CharacterSheets;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ChatUiSystem;

internal sealed class SetupWizardSheetEditor : IDisposable
{
    private readonly ICoreClientAPI _api;
    private readonly SetupWizardDraft _draft;
    private readonly Action _changed;
    private readonly Action<Action> _queue;
    private readonly System.Func<ICoreClientAPI, string, ISetupGuidePreview> _actorFactory;
    private readonly Action<string> _track;
    private GuiComposer _composer;
    private SetupWizardSheetPreview _preview;
    private GuiDialogConfirm _confirm;
    private ElementBounds _inspectorContent;
    private readonly List<ElementBounds> _choiceErrors = new();
    private readonly List<double> _choiceErrorHeights = new();
    private bool _focusInspectorTabs;
    private float _inspectorScroll;
    private float _sheetScroll;
    private bool _scrollSelectionPending;
    private List<CharacterSheetFieldConfigEntryMessage> _lastValidFields;
    private int _generation;
    private bool _disposed, _updating, _compact, _inspecting, _drawer, _adding, _advanced;
    private double _height = 440;
    internal string SelectedId { get; private set; }
    internal SheetPreviewContext Context { get; private set; } = SheetPreviewContext.Player;
    internal bool Filled { get; private set; } = true;
    internal int InspectorTab { get; private set; }
    internal bool SelectionHidden
    {
        get
        {
            if (_preview != null) return _preview.SelectionHidden;
            if (Selected == null) return false;
            var mode = Context == SheetPreviewContext.Staff ? CharacterSheetViewMode.Admin : Context == SheetPreviewContext.NearbyInspection ? CharacterSheetViewMode.Look : CharacterSheetViewMode.Full;
            var definition = new CharacterSheetFieldDefinition { Visibility = Selected.Visibility, ShowInLook = Selected.ShowInLook };
            return !CharacterSheetSystem.CanViewField(definition, mode, Context == SheetPreviewContext.Player)
                || Context == SheetPreviewContext.NearbyInspection && !Filled && Selected.Optional;
        }
    }
    internal bool InspectorVisible => !_compact || _inspecting;
    internal static bool NeedsReturningInspector(double width) => width < SetupWizardSheetPreview.RequiredWidth + 20 + 280;
    private CharacterSheetFieldConfigEntryMessage _searchSelected;
    internal CharacterSheetFieldConfigEntryMessage Selected => _searchSelected != null && _draft.SheetFields.Any(candidate => ReferenceEquals(candidate, _searchSelected))
        ? _searchSelected : _draft.SheetFields.FirstOrDefault(f => f.Id == SelectedId);

    internal string SelectSearchField(CharacterSheetFieldConfigEntryMessage entry, string key)
    {
        SelectField(entry.Id);
        _searchSelected = entry;
        _drawer = false; _adding = false; _inspecting = true; _advanced = true;
        InspectorTab = key is "Visibility" or "ShowInLook" ? 1 : key is "LayoutSection" or "Width" or "EditorRows" ? 2 : 0;
        return "sheet-editor-" + (key switch { "Optional" => "Required", "LayoutSection" => "Placement", "Options" => entry.Type == "option" ? "Option-0" : "Type", "EditorRows" when entry.Type != "longstring" => "Placement", _ => key });
    }
    internal SetupWizardSheetPreview Preview => _preview;
    private bool CanEdit => !_disposed && _draft.CanEditSheetFields;
    private bool CanReset => CanEdit && _draft.SheetFieldsChanged && !_draft.IsSavePending && !_draft.HasUnconfirmedSave;

    internal SetupWizardSheetEditor(ICoreClientAPI api, SetupWizardDraft draft, Action changed, Action<Action> queueRecompose,
        System.Func<ICoreClientAPI, string, ISetupGuidePreview> actorFactory = null, Action<string> track = null)
    { _api = api; _draft = draft; _changed = changed; _queue = queueRecompose; _actorFactory = actorFactory; _track = track; _lastValidFields = CharacterSheetFieldConfigAdmin.CloneEntries(draft.OriginalSheetFields); }

    internal void SetAvailableHeight(double height) => _height = Math.Max(160, height);

    // The owner calls this before disposing its composer, so page navigation retains both viewports.
    internal void Detach()
    {
        _generation++;
        if (_preview?.Composer != null)
            _sheetScroll = _preview.Composer.GetScrollbar("scrollbar")?.CurrentYPosition ?? _sheetScroll;
        _preview = null;
        _composer = null;
        _inspectorContent = null;
        _choiceErrors.Clear();
        _choiceErrorHeights.Clear();
        _confirm?.TryClose(); _confirm?.Dispose(); _confirm = null;
    }

    public void Add(GuiComposer composer, double width, bool compact)
    {
        _composer = composer;
        composer.OnComposed += Refresh;
        _compact = NeedsReturningInspector(width);
        if (Selected == null) SelectedId = null;
        var editingCompact = _compact && _inspecting && !_adding && !_drawer;
        if (editingCompact)
        {
            Button(composer, "Back to sheet", "back", 0, 48, 150, () => Recompose(() => _inspecting = false));
            Richtext(composer, "", Font(13), ElementBounds.Fixed(170, 48, width - 170, 34), "sheet-editor-status");
            AddInspector(composer, 0, 90, width, _height + 66);
            return;
        }
        if (_adding) { AddTypes(composer, width); return; }
        if (_drawer) { AddDrawer(composer, width); return; }
        Text(composer, "Sheet builder", 0, 48, 200, 24, 19);
        Button(composer, "Add field", "add", 220, 48, 110, () => Recompose(() => { _adding = true; _drawer = false; }), CanEdit);
        Button(composer, "Browse fields", "all", 340, 48, 140, () => Recompose(() => { _drawer = true; _adding = false; }));
        Text(composer, "Sheet preview", 0, 90, 150, 24, 18);
        Text(composer, "View as", 0, 126, 70, 20, 13);
        Select(composer, "context", null, new[] { "Player", "Other player", "Nearby inspection", "Staff" },
            new[] { "0", "1", "2", "3" }, ((int)Context).ToString(CultureInfo.InvariantCulture), 70, 122, 180,
            value => Recompose(() => { Context = (SheetPreviewContext)int.Parse(value, CultureInfo.InvariantCulture); Track("sheet_preview"); }));
        Text(composer, "Example answers", 268, 126, 160, 20, 13);
        composer.AddInteractiveElement(new GuiElementSetupToggle(_api, Filled,
            value => Recompose(() => { Filled = value; Track("sheet_preview"); }), ElementBounds.Fixed(438, 122, 112, 28)), "sheet-editor-filled");
        if (SelectionHidden) Button(composer, "Show staff view", "staff", 330, 156, 220,
            () => Recompose(() => { Context = SheetPreviewContext.Staff; Track("sheet_preview"); }));
        Richtext(composer, "", Font(12), ElementBounds.Fixed(158, 90, 392, 28), "sheet-editor-status");
        var contentTop = SelectionHidden ? 194d : 162d;
        _preview = new SetupWizardSheetPreview(_api, ElementBounds.Fixed(0, contentTop, SetupWizardSheetPreview.RequiredWidth, _height + 156 - contentTop),
            _draft.SheetFields, id => Recompose(() => SelectField(id)), _actorFactory, _lastValidFields);
        _preview.Refresh(_draft.SheetFields, SelectedId, Context, Filled, int.TryParse(_draft.Get("MaxNicknameLength"), out var nicknameLimit) ? nicknameLimit : new thebasics.Configs.ModConfig().MaxNicknameLength);
        composer.AddInteractiveElement(_preview, "sheet-editor-preview");
        composer.OnComposed += () => { if (!_disposed && _preview != null) RestoreSheetScroll(); };
        if (!_compact) AddInspector(composer, SetupWizardSheetPreview.RequiredWidth + 20, contentTop - 72, width - SetupWizardSheetPreview.RequiredWidth - 20, _height + 156 - (contentTop - 72));
    }

    private void RestoreSheetScroll()
    {
        var scrollbar = _preview?.Composer?.GetScrollbar("scrollbar");
        if (scrollbar != null && _sheetScroll > 0) _preview.Dialog.RestoreTemplateScroll(_sheetScroll);
        if (_scrollSelectionPending)
        {
            _preview?.SelectField(SelectedId, true);
            if (!SelectionHidden) _scrollSelectionPending = false;
        }
    }

    private void AddTypes(GuiComposer composer, double width)
    {
        Button(composer, "Back to sheet", "back", 0, 48, 150, () => Recompose(() => _adding = false));
        Text(composer, "Add a field", 0, 90, width, 24, 18);
        var names = new[] { "Short text", "Long text", "Number", "Choice" };
        var types = new[] { "string", "longstring", "number", "option" };
        var descriptions = new[] { "A name or brief answer", "A story or longer answer", "A numeric answer", "One answer from a list" };
        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var columnWidth = Math.Min(300, (width - 20) / 2);
            var x = i % 2 * (columnWidth + 20);
            var y = 132 + i / 2 * 86;
            Button(composer, names[i], "add-" + i, x, y, columnWidth, () => Recompose(() => AddField(type)), CanEdit);
            Text(composer, descriptions[i], x, y + 34, columnWidth, 38, 13);
        }
    }

    private void AddDrawer(GuiComposer composer, double width)
    {
        Button(composer, "Back to sheet", "back", 0, 48, 150, () => Recompose(() => _drawer = false));
        Text(composer, "Browse fields", 0, 90, width, 24, 18);
        var content = ElementBounds.Fixed(0, 0, width - 24, Math.Max(_height - 36, _draft.SheetFields.Count * 36));
        var clip = ElementBounds.Fixed(0, 132, width - 20, _height - 36);
        composer.BeginClip(clip).BeginChildElements(content);
        for (var i = 0; i < _draft.SheetFields.Count; i++)
        {
            var field = _draft.SheetFields[i];
            Button(composer, (field.Id == SelectedId ? "Selected: " : "") + (i + 1) + ". " + Label(field), "field-" + i, 0, i * 36, width - 44,
                () => Recompose(() => { SelectField(field.Id); _drawer = false; }));
        }
        composer.EndChildElements().EndClip();
        Scrollbar(composer, "drawer", content, clip, 0, _ => { });
        Button(composer, "Discard field edits", "reset", 0, _height + 108, 190, ConfirmReset, CanReset);
    }

    private void AddInspector(GuiComposer composer, double x, double y, double width, double height)
    {
        composer.AddInset(ElementBounds.Fixed(x, y, width, height), 3);
        composer.AddStaticElement(new GuiElementCustomDraw(_api, ElementBounds.Fixed(x + 3, y + 3, 3, height - 6),
            (context, _, bounds) =>
            {
                context.SetSourceRGBA(.95, .72, .3, 1);
                context.Rectangle(bounds.bgDrawX, bounds.bgDrawY, bounds.OuterWidth, bounds.OuterHeight);
                context.Fill();
            }), "sheet-editor-accent");
        x += 12; y += 10; width -= 24; height -= 20;
        Text(composer, "Character sheet field", x, y, width, 20, 12);
        y += 22; height -= 22;
        if (Selected == null)
        {
            Richtext(composer, "Choose a field", Font(18), ElementBounds.Fixed(x, y, width, 28), "sheet-editor-title");
            Text(composer, "Select a field on the sheet or browse the field list.", x, y + 38, width, 64, 14);
            return;
        }
        var field = Selected;
        _choiceErrors.Clear();
        _choiceErrorHeights.Clear();
        width -= 20;
        Richtext(composer, "", Font(18), ElementBounds.Fixed(x, y, width, 28), "sheet-editor-title");
        var generation = _generation;
        composer.AddHorizontalTabs(new[] { new GuiTab { Name = "Field", DataInt = 0 }, new GuiTab { Name = "Visibility", DataInt = 1 }, new GuiTab { Name = "Layout", DataInt = 2 } },
            ElementBounds.Fixed(x, y + 34, width, 28),
            index => { if (!_disposed && generation == _generation && InspectorTab != index) Recompose(() => { SelectInspectorTab(index); _focusInspectorTabs = true; }); }, Font(14), Font(14), "sheet-editor-tabs");
        composer.GetHorizontalTabs("sheet-editor-tabs").SetValue(InspectorTab, false);
        composer.OnComposed += () => { if (_focusInspectorTabs) { _focusInspectorTabs = false; composer.FocusElement(composer.GetHorizontalTabs("sheet-editor-tabs").TabIndex); } };
        _inspectorContent = ElementBounds.Fixed(0, -_inspectorScroll, width, 1500);
        var clip = ElementBounds.Fixed(x, y + 72, width, height - 112);
        composer.BeginClip(clip).BeginChildElements(_inspectorContent);
        var row = 0d;
        if (InspectorTab == 0)
        {
            Input(composer, "Label", "Field label", field.Label, width, ref row, text => field.Label = text);
            SelectRow("Type", "Answer type", new[] { "Short text", "Long text", "Number", "Choice" }, new[] { "string", "longstring", "number", "option" }, field.Type,
                value => Recompose(() => { field.Type = value; Changed(); }));
            Toggle("Required", "Required", !field.Optional, on => field.Optional = !on);
            Help(RequiredWarning(field, Bool("EnableCharacterSheets") && Bool("CharacterSheetRequireRequiredFieldsForRoleplay"), Bool("DisableNicknames")), 45);
            if (field.Type == "option")
            {
                var choices = (field.Options ?? "").Split(',').ToList();
                for (var i = 0; i < choices.Count; i++)
                {
                    var index = i;
                    Input(composer, "Option-" + i, "Choice " + (i + 1), choices[i], width - 50, ref row, text => { choices[index] = text; field.Options = string.Join(",", choices); });
                    Button(composer, "X", "option-remove-" + i, width - 44, row - 36, 40, () => Recompose(() => { choices.RemoveAt(index); field.Options = string.Join(",", choices); Changed(); }), CanEdit);
                    var errorBounds = ElementBounds.Fixed(0, row, width, 0);
                    _choiceErrors.Add(errorBounds);
                    _choiceErrorHeights.Add(0);
                    Richtext(composer,"", Font(12).WithColor(new[] { 1d, .72, .56, 1d }), errorBounds, "sheet-editor-option-error-" + i);
                }
                Button(composer, "Add choice", "option-add", 0, row, width, () => Recompose(() => { field.Options = (field.Options ?? "") + ","; Changed(); }), CanEdit);
                row += 36;
            }
            Button(composer, _advanced ? "Fewer options" : "More options", "advanced", 0, row, Math.Min(width, 160), () => Recompose(() => _advanced = !_advanced)); row += 36;
            if (_advanced)
            {
                Input(composer, "MaxLength", "Character limit (0: no field limit)", field.MaxLength, width, ref row, text => field.MaxLength = text);
                if (field.BindTo == "thebasics.nickname") Help("Without a field limit, the configured nickname limit applies.", 36);
                Input(composer, "Description", "Description", field.Description, width, ref row, text => field.Description = text);
                SelectRow("BindTo", "Use as", new[] { "Ordinary field", "Character name", "Nickname" }, new[] { "", "thebasics.fullName", "thebasics.nickname" }, field.BindTo,
                    value => { field.BindTo = value; Changed(); });
                Help(SetupWizardSheetReview.BindingWarning, 50);
                Input(composer, "Id", "Field ID (read-only, copyable)", field.Id, width, ref row, _ => { }, readOnly: true);
            }
        }
        else if (InspectorTab == 1)
        {
            SelectRow("Visibility", "Who can see it", new[] { "Everyone", "Nearby inspection", "Player and staff", "Staff only" }, new[] { "public", "nearby", "self", "admin" }, field.Visibility,
                value => Recompose(() => { field.Visibility = value; Changed(); }));
            Toggle("ShowInLook", "Show when inspecting nearby", field.ShowInLook, on => field.ShowInLook = on, field.Visibility is "public" or "nearby");
            Help(field.Visibility is "public" or "nearby" ? "Nearby inspection needs this toggle. Range and line of sight still apply." : "Choose Everyone or Nearby inspection to allow nearby inspection.", 48);
        }
        else
        {
        SelectRow("Placement", "Placement", new[] { "Beside portrait", "Main sheet" }, new[] { "header-side", "body" }, field.LayoutSection,
            value => Recompose(() => SetPlacement(value)));
        SelectRow("Width", "Width", new[] { "Full row", "Half row" }, new[] { "full", "half" }, field.Width,
            value => { field.Width = value; Changed(); }, field.LayoutSection != "header-side");
        Help(field.LayoutSection == "header-side" ? "Fields beside the portrait use the portrait grid width." : "Consecutive half-width fields pair on a row.", 38);
        if (field.Type == "longstring")
        {
            Input(composer, "EditorRows", "Text box height", field.EditorRows, width, ref row, text => field.EditorRows = text);
            Help("0 uses six rows. Other values use 2-16 rows in the player's answer editor.", 40);
        }
        Button(composer, "Move up", "up", 0, row, (width - 10) / 2, () => Recompose(() => MoveSelected(-1)), CanMove(-1));
        Button(composer, "Move down", "down", (width + 10) / 2, row, (width - 10) / 2, () => Recompose(() => MoveSelected(1)), CanMove(1));
        row += 36;
        }
        Richtext(composer,"", Font(12).WithColor(new[] { 1d, .72, .56, 1d }), ElementBounds.Fixed(0, row, width, 0), "sheet-editor-errors"); row += 8;
        _inspectorContent.fixedHeight = row;
        composer.EndChildElements().EndClip();
        Scrollbar(composer, "inspector", _inspectorContent, clip, _inspectorScroll, value => _inspectorScroll = value);
        Button(composer, "Remove field", "remove", x + Math.Max(0, width - 160), y + height - 32, Math.Min(width, 160), ConfirmRemove, CanEdit && _draft.SheetFields.Count > 1);

        void SelectRow(string key, string label, string[] names, string[] values, string value, Action<string> update, bool enabled = true)
        { Select(composer, key, label, names, values, value, 0, row, width, update, enabled && CanEdit); row += 58; }
        void Toggle(string key, string label, bool value, Action<bool> update, bool enabled = true)
        {
            Text(composer, label, 0, row, width, 20, 13); row += 22;
            var generation = _generation;
            var toggle = new GuiElementSetupToggle(_api, value, on => { if (!CanEdit || generation != _generation) return; update(on); Changed(); }, ElementBounds.Fixed(0, row, Math.Min(112, width), 28)) { Enabled = enabled && CanEdit };
            composer.AddInteractiveElement(toggle, "sheet-editor-" + key); row += 36;
        }
        void Help(string text, double size) { if (text.Length == 0) return; Text(composer, text, 0, row, width, size, 12); row += size; }
    }

    private void Scrollbar(GuiComposer composer, string key, ElementBounds content, ElementBounds clip, float initial, Action<float> changed)
    {
        var scrollbar = new BuilderScrollbar(_api, value => { content.fixedY = -value; content.MarkDirtyRecursive(); content.CalcWorldBounds(); changed(value); },
            ElementBounds.Fixed(clip.fixedX + clip.fixedWidth + 4, clip.fixedY, 12, clip.fixedHeight), clip);
        composer.AddInteractiveElement(scrollbar, "sheet-editor-" + key + "-scroll");
        composer.OnFocusChanged += focused =>
        {
            var control = composer.CurrentTabIndexElement;
            if (!focused || _disposed || control?.Bounds.ParentBounds != content) return;
            var top = control == composer.GetTextInput("sheet-editor-Label") ? 0 : Math.Max(0, control.Bounds.fixedY - 22);
            var current = scrollbar.CurrentYPosition;
            var bottom = control.Bounds.fixedY + control.Bounds.fixedHeight + (control is GuiElementDropDown ? Math.Min(180,clip.fixedHeight/2) : 0);
            var next = top < current ? top : bottom > current + clip.fixedHeight ? bottom - clip.fixedHeight : current;
            scrollbar.MoveTo((float)next);
        };
        composer.OnComposed += () =>
        {
            if (_disposed) return;
            var scroll = composer.GetScrollbar("sheet-editor-" + key + "-scroll");
            scroll.SetHeights((float)clip.fixedHeight, (float)content.fixedHeight);
            scroll.CurrentYPosition = Math.Clamp(initial, 0, (float)Math.Max(0, content.fixedHeight - clip.fixedHeight));
            content.fixedY = -scroll.CurrentYPosition; content.MarkDirtyRecursive(); content.CalcWorldBounds();
        };
    }

    private void Input(GuiComposer composer, string key, string label, string value, double width, ref double row, Action<string> update, bool readOnly = false)
    {
        Text(composer, label, 0, row, width, 20, 13); row += 22;
        var generation = _generation;
        GuiElementTextInput input = null;
        input = new SetupWizardViewportTextInput(_api, ElementBounds.Fixed(0, row, width, 28), text =>
        {
            if (_updating || _disposed || generation != _generation) return;
            if (readOnly || !CanEdit) { _updating = true; try { input.SetValue(value ?? ""); } finally { _updating = false; } return; }
            update(text); Changed();
        }, CairoFont.TextInput().WithFontSize(15));
        input.SetMaxLength(Math.Max(8192, value?.Length ?? 0));
        _updating = true; try { input.SetValue(value ?? ""); } finally { _updating = false; }
        composer.AddInteractiveElement(input, "sheet-editor-" + key); row += 36;
    }

    private void Select(GuiComposer composer, string key, string label, string[] names, string[] values, string value,
        double x, double y, double width, Action<string> update, bool enabled = true)
    {
        if (label != null) { Text(composer, label, x, y, width, 20, 13); y += 23; }
        var generation = _generation;
        var dropdown = new SetupWizardViewportDropDown(_api, values, names, Math.Max(0, Array.IndexOf(values, value)),
            (selected, on) => { if (on && !_disposed && !_updating && generation == _generation) update(selected); }, ElementBounds.Fixed(x, y, width, 28), Font(15)) { Enabled = enabled };
        composer.AddInteractiveElement(dropdown, "sheet-editor-" + key);
    }
    private void Button(GuiComposer composer, string label, string key, double x, double y, double width, Action action, bool enabled = true)
    {
        var generation = _generation;
        composer.AddInteractiveElement(new SetupWizardViewportButton(_api,label, () => { if (!_disposed && generation == _generation) action(); return true; }, ElementBounds.Fixed(x, y, width, 28)), "sheet-editor-" + key);
        composer.GetButton("sheet-editor-" + key).Enabled = enabled;
    }
    private static void Text(GuiComposer composer, string text, double x, double y, double width, double height, double size) =>
        Richtext(composer,VtmlUtils.EscapeVtml(text), Font(size), ElementBounds.Fixed(x, y, width, height));
    private static void Richtext(GuiComposer composer,string text,CairoFont font,ElementBounds bounds,string key=null) =>
        composer.AddInteractiveElement(new SetupWizardViewportRichtext(composer.Api,text,font,bounds),key);
    private static CairoFont Font(double size) => CairoFont.WhiteSmallText().WithFontSize((float)size);
    private bool Bool(string key) => ConfigAdminSettingDefinition.TryParseBool(_draft.Get(key), out var value) && value;
    internal static string Label(CharacterSheetFieldConfigEntryMessage field) => string.IsNullOrWhiteSpace(field.Label) ? "Untitled field" : field.Label;
    internal static string RequiredWarning(CharacterSheetFieldConfigEntryMessage field, bool gate, bool nicknamesDisabled = false) => gate && !field.Optional && field.Visibility != "admin" && !(field.BindTo == "thebasics.nickname" && nicknamesDisabled)
        ? "A missing player-editable answer can block roleplay while the required-fields rule is on." : "";
    private void Track(string action) => _track?.Invoke(action);
    private void Recompose(Action action)
    { if (!_disposed) _queue?.Invoke(() => { if (_disposed) return; action(); }); }
    private void Changed() { if (_disposed) return; _changed?.Invoke(); Refresh(); }

    public void Refresh()
    {
        if (_disposed) return;
        _composer?.GetRichtext("sheet-editor-title")?.SetNewText(VtmlUtils.EscapeVtml(Selected == null ? "Choose a field" : Label(Selected)), Font(18).WithColor(new[] { .95, .72, .3, 1d }));
        var complete = _draft.SheetFields.Where(field => !SetupWizardSheetPreview.IsUnfinished(field)).ToList();
        if (CharacterSheetFieldConfigAdmin.ValidateEntries(SetupWizardState.NormalizeSheetDraft(complete), out _).Count == 0 &&
            SetupWizardState.ValidateSheetChoices(complete).Count == 0)
            _lastValidFields = CharacterSheetFieldConfigAdmin.CloneEntries(complete);
        _preview?.Refresh(_draft.SheetFields, SelectedId, Context, Filled, int.TryParse(_draft.Get("MaxNicknameLength"), out var nicknameLimit) ? nicknameLimit : new thebasics.Configs.ModConfig().MaxNicknameLength);
        var errors = CharacterSheetFieldConfigAdmin.ValidateEntries(SetupWizardState.NormalizeSheetDraft(_draft.SheetFields), out _).ToList();
        errors.AddRange(SetupWizardState.ValidateSheetChoices(_draft.SheetFields));
        if (Selected?.Type == "option")
        {
            var choices = (Selected.Options ?? "").Split(',').Select(value => value.Trim()).ToArray();
            for (var index = 0; index < choices.Length; index++)
            {
                var value = choices[index];
                var error = value.Length == 0 ? "Choice " + (index + 1) + " must not be empty." :
                    choices.Count(other => string.Equals(value, other, StringComparison.OrdinalIgnoreCase)) > 1 ? "Choice " + (index + 1) + " duplicates another choice." : "";
                if (index < _choiceErrors.Count)
                {
                    var bounds = _choiceErrors[index];
                    var height = error.Length == 0 ? 0 : 28;
                    var delta = height - _choiceErrorHeights[index];
                    _choiceErrorHeights[index] = height;
                    if (delta != 0)
                    {
                        foreach (var child in _inspectorContent.ChildBounds)
                            if (child != bounds && child.fixedY >= bounds.fixedY) child.fixedY += delta;
                        bounds.fixedHeight = height;
                        _inspectorContent.fixedHeight += delta;
                        _inspectorContent.MarkDirtyRecursive(); _inspectorContent.CalcWorldBounds();
                        var scrollbar = _composer?.GetScrollbar("sheet-editor-inspector-scroll");
                        scrollbar?.SetHeights((float)scrollbar.Bounds.fixedHeight, (float)_inspectorContent.fixedHeight);
                    }
                }
                _composer?.GetRichtext("sheet-editor-option-error-" + index)?.SetNewText(error, Font(12).WithColor(new[] { 1d, .72, .56, 1d }));
            }
        }
        var generalErrors = _composer?.GetRichtext("sheet-editor-errors");
        if (generalErrors != null && _inspectorContent != null)
        {
            generalErrors.SetNewText(VtmlUtils.EscapeVtml(string.Join(" ", errors.Take(3))), Font(12).WithColor(new[] { 1d, .72, .56, 1d }));
            generalErrors.Bounds.fixedHeight = errors.Count == 0 ? 0 : generalErrors.TotalHeight / RuntimeEnv.GUIScale;
            _inspectorContent.fixedHeight = generalErrors.Bounds.fixedY + generalErrors.Bounds.fixedHeight + 8;
            _inspectorContent.MarkDirtyRecursive(); _inspectorContent.CalcWorldBounds();
            var scrollbar = _composer.GetScrollbar("sheet-editor-inspector-scroll") as BuilderScrollbar;
            if (scrollbar != null)
            {
                scrollbar.SetHeights((float)scrollbar.Bounds.fixedHeight, (float)_inspectorContent.fixedHeight);
                scrollbar.MoveTo(scrollbar.CurrentYPosition);
            }
        }
        var status = new List<string>();
        if (!_draft.CanEditSheetFields) status.Add(_draft.SheetFieldsUnavailableReason);
        else if (!Bool("EnableCharacterSheets")) status.Add("Character sheets are off.");
        if (_preview?.ShowingLastValid == true || errors.Count > 0) status.Add("Preview keeps the last valid sheet.");
        if (SelectionHidden) status.Add("Selected field is hidden in this preview.");
        var statusText = status.Count == 0 && !(_compact && _inspecting) ? "Fictional preview. Select a field to edit." : string.Join(" ", status);
        _composer?.GetRichtext("sheet-editor-status")?.SetNewText(VtmlUtils.EscapeVtml(statusText), Font(12));
        var staff = _composer?.GetButton("sheet-editor-staff"); if (staff != null) staff.Enabled = SelectionHidden;
        var reset = _composer?.GetButton("sheet-editor-reset"); if (reset != null) reset.Enabled = CanReset;
    }
    internal void SelectInspectorTab(int index)
    {
        if (_disposed || index is < 0 or > 2 || InspectorTab == index) return;
        InspectorTab = index; _inspectorScroll = 0;
    }
    internal void SelectField(string id)
    {
        _searchSelected = null;
        if (_disposed || !_draft.SheetFields.Any(f => f.Id == id)) return;
        if (SelectedId != id) { _inspectorScroll = 0; InspectorTab = 0; }
        _drawer = false; _adding = false;
        _scrollSelectionPending = true;
        SelectedId = id; _inspecting = true; _preview?.SelectField(id, true); Track("sheet_select");
    }
    internal void AddField(string type)
    {
        if (!CanEdit || _draft.SheetFields.Count >= 256 || type is not ("string" or "longstring" or "number" or "option")) return;
        var field = CharacterSheetFieldConfigAdmin.CreateBuilderEntry(); field.Type = type;
        _searchSelected = null; _draft.SheetFields.Add(field); _scrollSelectionPending = true; SelectedId = field.Id; _adding = false; _inspecting = true; _inspectorScroll = 0; InspectorTab = 0; Changed(); Track("sheet_add");
    }
    internal bool CanMove(int offset)
    {
        if (!CanEdit || Selected == null) return false;
        var section = _draft.SheetFields.Where(f => f.LayoutSection == Selected.LayoutSection).ToList();
        var target = section.IndexOf(Selected) + offset;
        return target >= 0 && target < section.Count;
    }
    internal void MoveSelected(int offset)
    {
        if (!CanMove(offset)) return;
        var fields = _draft.SheetFields; var selected = Selected;
        var section = fields.Where(f => f.LayoutSection == selected.LayoutSection).ToList();
        var neighbor = section[section.IndexOf(selected) + offset];
        fields.Remove(selected); fields.Insert(fields.IndexOf(neighbor) + (offset > 0 ? 1 : 0), selected);
        Changed(); Track("sheet_move");
    }
    internal void SetPlacement(string section)
    {
        if (!CanEdit || Selected == null || section is not ("header-side" or "body") || Selected.LayoutSection == section) return;
        var field = Selected; var fields = _draft.SheetFields; fields.Remove(field); field.LayoutSection = section;
        var last = fields.FindLastIndex(f => f.LayoutSection == section); fields.Insert(last < 0 ? fields.Count : last + 1, field);
        Changed(); Track("sheet_move");
    }
    internal bool RemoveSelected(bool confirmed)
    {
        if (!CanEdit || !confirmed || Selected == null || _draft.SheetFields.Count <= 1) return false;
        _draft.SheetFields.Remove(Selected); SelectedId = null; _inspecting = false; _scrollSelectionPending = false; _inspectorScroll = 0; Changed(); Track("sheet_remove"); return true;
    }
    private void ConfirmRemove() => Confirm(SetupWizardSheetReview.RemovalWarning, () => RemoveSelected(true));
    private void ConfirmReset()
    {
        if (!CanReset) return;
        Confirm("Discard sheet field edits and load the latest server template? Other settings and language edits are kept.", () =>
        { if (!CanReset) return; ResetFields(); });
    }
    internal void ResetFields()
    {
        if (!CanReset) return;
        _draft.ResetSheetFields();
        if (Selected == null) { SelectedId = null; _inspecting = false; _scrollSelectionPending = false; }
        _inspectorScroll = 0; Changed(); Track("sheet_reset");
    }
    private void Confirm(string text, Action accepted)
    {
        if (_confirm?.IsOpened() == true || _disposed) return;
        _confirm?.Dispose();
        _confirm = new BuilderConfirm(_api, text, ok => { if (!_disposed) Recompose(() => { if (ok) accepted(); }); }); _confirm.TryOpen();
    }
    private sealed class BuilderConfirm(ICoreClientAPI api, string text, Action<bool> accepted) : GuiDialogConfirm(api, text, accepted)
    { public override bool UnregisterOnClose => true; }

    internal sealed class BuilderScrollbar(ICoreClientAPI api, Action<float> changed, ElementBounds bounds, ElementBounds viewport)
        : GuiElementScrollbar(api, changed, bounds)
    {
        public override bool Focusable => totalHeight > visibleHeight && base.Focusable;
        public override void RenderInteractiveElements(float deltaTime)
        { if (totalHeight > visibleHeight) base.RenderInteractiveElements(deltaTime); }
        internal void MoveTo(float value)
        {
            CurrentYPosition = Math.Clamp(value, 0, Math.Max(0, totalHeight - visibleHeight));
            onNewScrollbarValue(CurrentYPosition);
        }
        public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
        { if (totalHeight > visibleHeight && (viewport.PointInside(api.Input.MouseX, api.Input.MouseY) || Bounds.PointInside(api.Input.MouseX, api.Input.MouseY))) base.OnMouseWheel(api, args); }
        public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
        {
            if (!HasFocus) return;
            var value = args.KeyCode switch
            {
                (int)GlKeys.Up => CurrentYPosition - 36, (int)GlKeys.Down => CurrentYPosition + 36,
                (int)GlKeys.PageUp => CurrentYPosition - visibleHeight, (int)GlKeys.PageDown => CurrentYPosition + visibleHeight,
                (int)GlKeys.Home => 0, (int)GlKeys.End => totalHeight, _ => float.NaN
            };
            if (float.IsNaN(value)) return;
            MoveTo(value); args.Handled = true;
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _confirm?.TryClose(); _confirm?.Dispose(); _confirm = null;
        _preview?.Dispose(); _preview = null; _composer = null;
    }
}
