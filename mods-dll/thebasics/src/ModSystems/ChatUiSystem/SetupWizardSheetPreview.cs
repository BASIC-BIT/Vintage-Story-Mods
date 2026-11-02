using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cairo;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.CharacterSheets;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal enum SheetPreviewContext { Player, OtherPlayer, NearbyInspection, Staff }

/// <summary>Owns a detached production sheet, fictional answers and a detached seraph.</summary>
internal sealed class SetupWizardSheetPreview : GuiElement
{
    internal static double RequiredWidth => CharacterSheetDialog.TemplatePreviewRequiredWidth;
    private readonly SetupWizardActorPool _actors;
    private readonly Action<string> _selected;
    private List<CharacterSheetFieldDefinition> _lastValid = new();
    private GuiComposer _inspection;
    private SheetPreviewContext _context;
    private bool _disposed;
    private bool _reverseEntry;
    internal CharacterSheetDialog Dialog { get; private set; }
    internal CharacterSheetViewMessage View { get; private set; }
    internal GuiComposer Composer => _context == SheetPreviewContext.NearbyInspection ? _inspection : Dialog?.TemplatePreviewComposer;
    internal string SelectedId { get; private set; }
    internal bool SelectionHidden => SelectedId != null && (_context == SheetPreviewContext.NearbyInspection
        ? !View.Fields.Any(item => item.FieldId == SelectedId && (!item.Optional || !string.IsNullOrWhiteSpace(item.Value)))
        : !Dialog.TemplateSelections.ContainsKey(SelectedId));
    internal bool ShowingLastValid { get; private set; }
    internal string InspectionText { get; private set; }
    internal bool IsReady => Dialog?.TemplatePreviewComposer?.GetElement("template-portrait") is not SetupWizardActorLayer layer || layer.IsReady;
    public override bool Focusable => true;

    internal SetupWizardSheetPreview(ICoreClientAPI api, ElementBounds bounds,
        IReadOnlyList<CharacterSheetFieldConfigEntryMessage> fields, Action<string> onFieldSelected,
        Func<ICoreClientAPI, string, ISetupGuidePreview> actorFactory = null,
        IReadOnlyList<CharacterSheetFieldConfigEntryMessage> lastValidFields = null) : base(api, bounds)
    {
        _selected = onFieldSelected;
        _actors = new SetupWizardActorPool(api, actorFactory ?? ((client, identity) => new SetupGuidePreview(client, identity)));
        if (lastValidFields != null) Refresh(lastValidFields, null, SheetPreviewContext.Player, true);
        Refresh(fields, null, SheetPreviewContext.Player, true);
    }

    internal void Refresh(IReadOnlyList<CharacterSheetFieldConfigEntryMessage> fields, string selectedId, SheetPreviewContext context, bool filled, int maxNicknameLength = 100)
    {
        if (_disposed) return;
        var unfinished = (fields ?? Array.Empty<CharacterSheetFieldConfigEntryMessage>()).Where(IsUnfinished).ToList();
        var complete = (fields ?? Array.Empty<CharacterSheetFieldConfigEntryMessage>()).Where(item => !IsUnfinished(item)).ToList();
        var errors = CharacterSheetFieldConfigAdmin.ValidateEntries(SetupWizardState.NormalizeSheetDraft(complete), out var valid);
        ShowingLastValid = errors.Count > 0 || SetupWizardState.ValidateSheetChoices(complete).Count > 0;
        if (!ShowingLastValid) _lastValid = valid;
        _context = context;
        SelectedId = selectedId;
        var mode = context == SheetPreviewContext.Staff ? CharacterSheetViewMode.Admin : context == SheetPreviewContext.NearbyInspection ? CharacterSheetViewMode.Look : CharacterSheetViewMode.Full;
        View = new CharacterSheetViewMessage
        {
            Title = "Fictional character sheet", TargetPlayerUid = "setup-fictional-pip", DisplayName = "Pip", TargetPlayerName = "PipPlayer",
            CanEdit = context is SheetPreviewContext.Player or SheetPreviewContext.Staff,
            IsAdminView = context == SheetPreviewContext.Staff, IsLookView = context == SheetPreviewContext.NearbyInspection,
            Fields = _lastValid.Where(item => CharacterSheetSystem.CanViewField(item, mode, context == SheetPreviewContext.Player))
                .Select(item => Sample(item, filled, context is SheetPreviewContext.Player or SheetPreviewContext.Staff, maxNicknameLength)).ToList()
        };
        if (View.Fields.Count == 0) View.Message = Lang.Get("thebasics:charsheet-empty");
        if (Dialog == null)
        {
            Dialog = new CharacterSheetDialog(api, View, null);
            Dialog.ConfigureTemplatePreview(SurfaceBounds(), id => { SelectedId = id; _selected?.Invoke(id); }, AddPortrait);
        }
        Dialog.SetTemplateUnfinishedFields(unfinished.Select(item => new CharacterSheetFieldViewMessage
        {
            FieldId = item.Id, Label = "Untitled field (unfinished)", Optional = true,
            LayoutSection = item.LayoutSection, Width = item.Width
        }).ToList(), (fields ?? Array.Empty<CharacterSheetFieldConfigEntryMessage>()).Where(item => item != null).Select(item => item.Id).ToList());
        Dialog.SetView(View);
        Dialog.SelectTemplateField(SelectedId, false);
        if (context == SheetPreviewContext.NearbyInspection) ComposeInspection();
        else { _inspection?.Dispose(); _inspection = null; }
        SyncPosition();
    }

    internal static bool IsUnfinished(CharacterSheetFieldConfigEntryMessage item) => item != null && string.IsNullOrWhiteSpace(item.OriginalId)
        && item.Id?.StartsWith("field-", StringComparison.Ordinal) == true && string.IsNullOrWhiteSpace(item.Label);

    private static CharacterSheetFieldViewMessage Sample(CharacterSheetFieldDefinition item, bool filled, bool editable, int maxNicknameLength)
    {
        var maxLength = item.MaxLength > 0 ? item.MaxLength : item.BindTo == "thebasics.nickname" ? maxNicknameLength : 0;
        var value = !filled ? "" : item.Type switch
        {
            CharacterSheetFieldTypes.Option => item.Options.FirstOrDefault(option => item.MaxLength <= 0 || option.Length <= item.MaxLength) ?? "",
            CharacterSheetFieldTypes.Number => "2",
            CharacterSheetFieldTypes.LongString => "Pip maps quiet roads and trades stories at the village inn.",
            _ => "Pip"
        };
        if (maxLength > 0 && value.Length > maxLength) value = value[..maxLength];
        return new CharacterSheetFieldViewMessage
        {
            FieldId = item.Id, Label = item.Label, Type = item.Type, Value = value, Optional = item.Optional,
            Options = item.Options.ToList(), MaxLength = maxLength, CanEdit = editable,
            Visibility = item.Visibility, EditorRows = item.EditorRows, LayoutSection = item.LayoutSection, Width = item.Width
        };
    }

    private void AddPortrait(GuiComposer composer, ElementBounds bounds) => composer.AddInteractiveElement(
        new SetupWizardActorLayer(api, bounds, _actors, () => [new("sheet", "Pip", "idle", 0, 0, 0, bounds.fixedWidth, bounds.fixedHeight)], () => true), "template-portrait");

    private void ComposeInspection()
    {
        _inspection?.Dispose();
        var text = new StringBuilder(Lang.Get("thebasics:charsheet-look-header", VtmlUtils.EscapeVtml("Pip")));
        var visible = View.Fields.Where(item => !item.Optional || !string.IsNullOrWhiteSpace(item.Value)).ToList();
        foreach (var item in visible) CharacterSheetSystem.AppendInspectionField(text, item.Label, item.Value);
        if (visible.Count == 0) text.Append('\n').Append(Lang.Get("thebasics:charsheet-look-empty"));
        InspectionText = text.ToString();
        var composer = api.Gui.CreateCompo("thebasics-setup-sheet-inspection", SurfaceBounds());
        try
        {
            SetupWizardDialog.AddChatWindow(composer, api, 0, Math.Max(50, Bounds.fixedHeight - 57),
                [new GuiTab { Name = "Proximity", DataInt = 0 }], 0, null, InspectionText);
            _inspection = composer.Compose(focusFirstElement: false);
            var history = _inspection.GetRichtext("preview-text");
            _inspection.GetCompactScrollbar("preview-chat-scroll").SetHeights((float)history.InsideClipBounds.fixedHeight, (float)history.Bounds.fixedHeight);
        }
        catch { composer.Dispose(); throw; }
    }

    internal void SelectField(string id, bool scrollIntoView)
    {
        SelectedId = id;
        Dialog.SelectTemplateField(id, scrollIntoView);
    }

    private ElementBounds PositionedBounds()
    {
        var positioned = Bounds.ParentBounds == null ? Bounds.FlatCopy().WithEmptyParent() : Bounds;
        positioned.CalcWorldBounds();
        return positioned;
    }

    private ElementBounds SurfaceBounds()
    {
        var positioned = PositionedBounds();
        var scale = Math.Max(RuntimeEnv.GUIScale, 0.1f);
        return ElementBounds.Fixed(positioned.renderX / scale, positioned.renderY / scale, RequiredWidth, Bounds.fixedHeight);
    }

    public override bool IsPositionInside(int x, int y) =>
        (InsideClipBounds == null || InsideClipBounds.PointInside(x, y)) && PositionedBounds().PointInside(x, y);
    private void SyncPosition()
    {
        var target = SurfaceBounds();
        foreach (var composer in new[] { Dialog?.TemplatePreviewComposer, _inspection })
        {
            if (composer == null || composer.Bounds.fixedX == target.fixedX && composer.Bounds.fixedY == target.fixedY) continue;
            composer.Bounds.WithFixedPosition(target.fixedX, target.fixedY);
            composer.Bounds.MarkDirtyRecursive();
            composer.Bounds.CalcWorldBounds();
        }
    }

    public override void ComposeElements(Context context, ImageSurface surface) => SyncPosition();
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (_disposed) return;
        SyncPosition();
        api.Render.PushScissor(PositionedBounds(), stacking: true);
        try { Composer?.Render(deltaTime); }
        finally { api.Render.PopScissor(); }
    }
    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
    {
        if (_disposed || !IsPositionInside(args.X, args.Y)) return;
        SyncPosition();
        Composer?.OnMouseDown(args);
    }
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (!_disposed) Composer?.OnMouseUp(args);
    }
    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (!_disposed) Composer?.OnMouseMove(args);
    }
    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (!_disposed && IsPositionInside(api.Input.MouseX, api.Input.MouseY)) Composer?.OnMouseWheel(args);
    }
    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (_disposed || args.Handled) return;
        if (args.KeyCode == (int)GlKeys.Tab)
        {
            _reverseEntry = args.ShiftPressed;
            if (!HasFocus || _context == SheetPreviewContext.NearbyInspection) return;
            var cells = Dialog.TemplateSelections.Values.ToList();
            var current = cells.FindIndex(item => item.HasFocus);
            var next = current < 0 ? (_reverseEntry ? cells.Count - 1 : 0) : current + (_reverseEntry ? -1 : 1);
            // Leave boundary Tab unhandled so the owning composer can move to its sibling.
            if (next < 0 || next >= cells.Count) return;
            Dialog.FocusTemplateField(cells[next].FieldId);
            args.Handled = true;
            return;
        }
        if (HasFocus) Composer?.OnKeyDown(args, true);
    }
    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
        if (!_disposed && HasFocus) Composer?.OnKeyPress(args);
    }
    public override void OnFocusGained()
    {
        base.OnFocusGained();
        if (Composer?.CurrentTabIndexElement != null) return;
        var cells = Dialog.TemplateSelections.Values.ToList();
        if (_context != SheetPreviewContext.NearbyInspection && cells.Count > 0)
            Dialog.FocusTemplateField(cells[_reverseEntry ? cells.Count - 1 : 0].FieldId);
    }
    public override void OnFocusLost()
    {
        Composer?.CurrentTabIndexElement?.OnFocusLost();
        base.OnFocusLost();
    }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inspection?.Dispose();
        Dialog?.Dispose();
        _actors.Dispose();
        base.Dispose();
    }
}
