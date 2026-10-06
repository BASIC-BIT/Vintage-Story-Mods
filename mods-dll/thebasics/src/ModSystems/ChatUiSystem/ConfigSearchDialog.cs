using System;
using System.Collections.Generic;
using System.Reflection;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal sealed class ConfigSearchDialog : GuiDialog
{
    private const double Width = 720;
    private readonly List<ConfigSearchResult> _entries;
    private readonly Action<ConfigSearchResult> _onSelect;
    private readonly Action<string> _onQuery;
    private readonly double _height;
    private readonly GuiElementContainer _results;
    private string _query;

    internal ConfigSearchDialog(ICoreClientAPI api, List<ConfigSearchResult> entries, string query,
        Action<string> onQuery, Action<ConfigSearchResult> onSelect) : base(api)
    {
        _entries = entries;
        _query = query ?? string.Empty;
        _onQuery = onQuery;
        _onSelect = onSelect;
        _height = Math.Clamp(api.Render.FrameHeight / (double)RuntimeEnv.GUIScale - 250, 120, 420);
        var body = ElementBounds.Fixed(0, 0, Width, _height + 140).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var clip = ElementBounds.Fixed(0, 98, Width - 24, _height);
        _results = new SearchResults(api, ElementBounds.Fixed(0, 0, Width - 30, 1)) { unscaledCellSpacing = 0 };
        var composer = api.Gui.CreateCompo("thebasics-config-search", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(body).AddDialogTitleBar(Lang.Get("thebasics:config-search-title"), () => TryClose())
            .BeginChildElements(body)
            .AddTextInput(ElementBounds.Fixed(0, 38, Width, 28), ChangeQuery, CairoFont.TextInput(), "query")
            .AddStaticText(Lang.Get("thebasics:config-search-help"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 72, Width, 22))
            .BeginClip(clip).AddInteractiveElement(_results, "results").EndClip()
            .AddVerticalScrollbar(Scroll, ElementStdBounds.VerticalScrollbar(clip), "scrollbar")
            .AddSmallButton(Lang.Get("thebasics:config-search-back"), () => TryClose(), ElementBounds.Fixed(0, _height + 106, 150, 28));
        AddResults();
        composer.GetTextInput("query").SetValue(_query);
        _results.CalcTotalHeight();
        composer.GetScrollbar("scrollbar").Bounds.CalcWorldBounds();
        composer.GetScrollbar("scrollbar").SetHeights((float)_height, (float)Math.Max(_height, _results.Bounds.fixedHeight));
        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
        UpdateScrollbar();
    }

    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override string ToggleKeyCombinationCode => null;
    public override double DrawOrder => 0.3;

    public override void OnMouseDown(MouseEvent args) { base.OnMouseDown(args); args.Handled = true; }
    public override void OnMouseUp(MouseEvent args) { base.OnMouseUp(args); args.Handled = true; }
    public override void OnMouseMove(MouseEvent args) { base.OnMouseMove(args); args.Handled = true; }
    public override void OnMouseWheel(MouseWheelEventArgs args) { base.OnMouseWheel(args); args.SetHandled(); }
    public override void OnKeyPress(KeyEvent args) { base.OnKeyPress(args); args.Handled = true; }
    public override void OnKeyUp(KeyEvent args) { base.OnKeyUp(args); args.Handled = true; }

    public override void OnKeyDown(KeyEvent args)
    {
        if (args.KeyCode == (int)GlKeys.Escape) TryClose();
        else base.OnKeyDown(args);
        var focused = _results.CurrentTabIndexElement;
        if (focused != null)
        {
            var top = focused.Bounds.fixedY;
            var bottom = top + focused.Bounds.fixedHeight;
            var current = -_results.Bounds.fixedY;
            if (top < current || bottom > current + _height)
            {
                var y = (float)(top < current ? top : bottom - _height);
                SingleComposer.GetScrollbar("scrollbar").CurrentYPosition = y;
                Scroll(y);
            }
        }
        args.Handled = true;
    }

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        var input = SingleComposer.GetTextInput("query");
        SingleComposer.FocusElement(input.TabIndex);
        // Vanilla has no public select-all API; use the same anchor and caret as Ctrl+A.
        typeof(GuiElementEditableTextBase).GetField("selectedTextStart", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(input, 0);
        input.CaretPosWithoutLineBreaks = input.GetText().Length;
    }

    private void ChangeQuery(string query)
    {
        if (query == _query) return;
        _query = query;
        _onQuery(query);
        foreach (var element in _results.Elements) element.Dispose();
        _results.Clear();
        AddResults();
        SingleComposer.ReCompose();
        UpdateScrollbar();
    }

    private void AddResults()
    {
        var matches = ConfigAdminSearch.Find(_entries, _query);
        _results.Tabbable = matches.Count > 0;
        if (matches.Count == 0)
        {
            _results.Add(new GuiElementDynamicText(capi, Lang.Get(string.IsNullOrWhiteSpace(_query)
                ? "thebasics:config-search-empty" : "thebasics:config-search-none"),
                CairoFont.WhiteSmallText(), ElementBounds.Fixed(4, 4, Width - 40, 40)));
            return;
        }
        var y = 0d;
        foreach (var result in matches)
        {
            var value = result.Value ?? string.Empty;
            if (value.Length > 120) value = value[..120] + "...";
            var text = $"{result.Group} | {Lang.Get("thebasics:config-search-value", value)}" +
                (result.Unsaved ? " | " + Lang.Get("thebasics:config-search-unsaved") : "") + "\n" + result.Description;
            var font = CairoFont.WhiteSmallText().WithFontSize(13).WithOrientation(EnumTextOrientation.Left);
            var height = Math.Max(35, new TextDrawUtil().GetMultilineTextHeight(font, text, (Width - 40) * RuntimeEnv.GUIScale) / RuntimeEnv.GUIScale + 6);
            var button = new GuiElementTextButton(capi, result.Label, font, font.Clone().WithColor(GuiStyle.ActiveButtonTextColor),
                () => { TryClose(); _onSelect(result); return true; }, ElementBounds.Fixed(4, y, Width - 40, 26), EnumButtonStyle.Small);
            button.SetOrientation(EnumTextOrientation.Left);
            _results.Add(button);
            _results.Add(new GuiElementDynamicText(capi, text, font, ElementBounds.Fixed(4, y + 30, Width - 40, height)));
            y += height + 44;
        }
    }

    private void UpdateScrollbar()
    {
        _results.Bounds.fixedY = 0;
        _results.Bounds.MarkDirtyRecursive();
        _results.Bounds.CalcWorldBounds();
        var scrollbar = SingleComposer.GetScrollbar("scrollbar");
        scrollbar.SetHeights((float)_height, (float)Math.Max(_height, _results.Bounds.fixedHeight));
        scrollbar.CurrentYPosition = 0;
        Scroll(0);
    }

    private void Scroll(float value)
    {
        _results.Bounds.fixedY = -value;
        _results.Bounds.MarkDirtyRecursive();
        _results.Bounds.CalcWorldBounds();
    }

    private sealed class SearchResults : GuiElementContainer
    {
        internal SearchResults(ICoreClientAPI api, ElementBounds bounds) : base(api, bounds) { }

        // Render native row textures individually. A broad query must not create a single
        // texture taller than the GPU limit, as the stock container does.
        public override void ComposeElements(Cairo.Context ctx, Cairo.ImageSurface surface)
        {
            CalcTotalHeight();
            Bounds.CalcWorldBounds();
            foreach (var element in Elements) element.ComposeElements(ctx, surface);
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            MouseOverCursor = null;
            foreach (var element in Elements)
            {
                if (element.Bounds.absY + element.Bounds.OuterHeight < InsideClipBounds.absY ||
                    element.Bounds.absY > InsideClipBounds.absY + InsideClipBounds.OuterHeight) continue;
                element.RenderInteractiveElements(deltaTime);
                if (element.HasFocus) element.RenderFocusOverlay(deltaTime);
                if (element.IsPositionInside(api.Input.MouseX, api.Input.MouseY)) MouseOverCursor = element.MouseOverCursor;
            }
        }
    }
}
