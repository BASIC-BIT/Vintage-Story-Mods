using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal class ScrollableConfigAdminDialog : GuiJsonDialog
{
    // ponytail: reuse vanilla's private composer and controls; rerun native previews on game API upgrades.
    private static readonly MethodInfo ComposeElement = typeof(GuiJsonDialog).GetMethod("ComposeElement", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly JsonDialogSettings _settings;
    private readonly Dictionary<string, (string Control, double Y)> _controls = new();
    private ElementBounds _content;
    private double _viewport;

    internal ScrollableConfigAdminDialog(JsonDialogSettings settings, ICoreClientAPI api, bool focusFirstElement)
        : base(settings, api, focusFirstElement)
    {
        _settings = settings;
        ComposeScrollable();
    }

    public override void Recompose() => ComposeScrollable();

    private void ComposeScrollable()
    {
        Composers.ClearComposers();
        _controls.Clear();
        var rows = _settings.Rows;
        var first = Array.FindIndex(rows, row => row.Elements.Any(element => element.Type == EnumDialogElementType.Text && element.Code?.StartsWith("group-", StringComparison.Ordinal) == true));
        var last = Array.FindIndex(rows, row => row.Elements.Any(element => element.Code == "save"));
        if (first < 0 || last < first) throw new InvalidOperationException("Config settings panel is missing.");
        var headerHeight = rows.Take(first).Sum(Height);
        var footerHeight = rows.Skip(last).Sum(Height);
        _viewport = Math.Clamp(capi.Render.FrameHeight / (RuntimeEnv.GUIScale * _settings.SizeMultiplier)
            - headerHeight - footerHeight - 90, 100, 360);
        var contentHeight = Math.Max(_viewport, rows.Skip(first).Take(last - first).Sum(Height));
        var root = ElementStdBounds.AutosizedMainDialog.WithAlignment(_settings.Alignment).WithFixedPadding(10)
            .WithScale(_settings.SizeMultiplier);
        var clip = ElementBounds.Fixed(0, headerHeight, 750, _viewport).WithScale(_settings.SizeMultiplier);
        _content = ElementBounds.Fixed(0, 0, 730, contentHeight).WithScale(_settings.SizeMultiplier);
        var composer = capi.Gui.CreateCompo("cmdDlg" + _settings.Code, root)
            .AddDialogBG(ElementStdBounds.DialogBackground().WithScale(_settings.SizeMultiplier).WithFixedPadding(_settings.Padding), false)
            .BeginChildElements();
        var index = 1;
        ComposeRows(composer, rows.Take(first), ref index, 0, false);
        composer.BeginClip(clip).BeginChildElements(_content);
        ComposeRows(composer, rows.Skip(first).Take(last - first), ref index, 0, true);
        // Vanilla JSON widgets bake their backgrounds into the dialog texture, and text
        // inputs reset scissoring. Cache each native widget separately for scrolling.
        var elements = (Dictionary<string, GuiElement>)typeof(GuiComposer)
            .GetField("staticElements", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(composer);
        var interactive = (Dictionary<string, GuiElement>)typeof(GuiComposer)
            .GetField("interactiveElements", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(composer);
        foreach (var pair in elements.Where(pair => pair.Value.Bounds.ParentBounds == _content).ToList())
        {
            var widget = new ScrollingWidget(capi, pair.Value, clip);
            if (interactive.ContainsKey(pair.Key))
            {
                elements[pair.Key] = interactive[pair.Key] = widget;
            }
            else
            {
                elements.Remove(pair.Key);
                composer.AddInteractiveElement(widget, pair.Key);
            }
        }
        composer.EndChildElements().EndClip();
        var scrollbarBounds = ElementBounds.Fixed(752, headerHeight, 12, _viewport).WithScale(_settings.SizeMultiplier);
        composer.AddVerticalScrollbar(Scroll, scrollbarBounds, "settings-scrollbar");
        ComposeRows(composer, rows.Skip(last), ref index, headerHeight + _viewport + 8, false);
        composer.GetScrollbar("settings-scrollbar").Bounds.CalcWorldBounds();
        composer.GetScrollbar("settings-scrollbar").SetHeights((float)_viewport, (float)contentHeight);
        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
    }

    private void ComposeRows(GuiComposer composer, IEnumerable<DialogRow> rows, ref int index, double y, bool settings)
    {
        foreach (var row in rows)
        {
            y += row.TopPadding;
            var x = 0d;
            foreach (var element in row.Elements)
            {
                x += element.PaddingLeft;
                ComposeElement.Invoke(this, new object[] { composer, _settings, element, index, x, y });
                if (settings)
                {
                    var prefix = element.Type switch
                    {
                        EnumDialogElementType.Switch => "switch-",
                        EnumDialogElementType.Input => "input-",
                        EnumDialogElementType.NumberInput => "numberinput-",
                        EnumDialogElementType.Select => "dropdown-",
                        _ => null
                    };
                    if (prefix != null) _controls[element.Code] = (prefix + index, y);
                }
                index++;
                x += element.Width + 20;
            }
            y += row.Elements.Max(element => element.Height) + row.BottomPadding;
        }
    }

    internal bool JumpTo(string key)
    {
        if (!_controls.TryGetValue(key, out var target)) return false;
        var scrollbar = SingleComposer.GetScrollbar("settings-scrollbar");
        var y = Math.Clamp(target.Y - 12, 0, Math.Max(0, _content.fixedHeight - _viewport));
        scrollbar.CurrentYPosition = (float)y;
        Scroll((float)y);
        SingleComposer.FocusElement(SingleComposer.GetElement(target.Control).TabIndex);
        return true;
    }

    public override void OnKeyDown(KeyEvent args)
    {
        base.OnKeyDown(args);
        // Keyboard tabbing must keep the focused setting inside the viewport too.
        var focused = _controls.Values.FirstOrDefault(control => SingleComposer.GetElement(control.Control).HasFocus);
        if (focused.Control != null && (focused.Y < -_content.fixedY || focused.Y + 28 > -_content.fixedY + _viewport))
        {
            var key = _controls.First(pair => pair.Value.Control == focused.Control).Key;
            JumpTo(key);
        }
    }

    private void Scroll(float value)
    {
        _content.fixedY = -value;
        _content.MarkDirtyRecursive();
        _content.CalcWorldBounds();
    }

    private static double Height(DialogRow row) => row.TopPadding + row.Elements.Max(element => element.Height) + row.BottomPadding;

    private sealed class ScrollingWidget : GuiElement
    {
        private readonly GuiElement _widget;
        private readonly ElementBounds _clip;
        private LoadedTexture _background;

        internal ScrollingWidget(ICoreClientAPI api, GuiElement widget, ElementBounds clip) : base(api, widget.Bounds)
        {
            _widget = widget;
            _clip = clip;
            _background = new LoadedTexture(api);
            TabIndex = widget.TabIndex;
            InsideClipBounds = clip;
        }

        public override bool Focusable => _widget.Focusable;
        public override double DrawOrder => _widget.DrawOrder;
        public override void BeforeCalcBounds() => _widget.BeforeCalcBounds();

        public override void ComposeElements(Cairo.Context ctx, Cairo.ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            using var image = new Cairo.ImageSurface(Cairo.Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
            using var local = new Cairo.Context(image);
            local.Translate(-Bounds.drawX, -Bounds.drawY);
            _widget.ComposeElements(local, image);
            api.Gui.LoadOrUpdateCairoTexture(image, true, ref _background);
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            api.Render.PushScissor(_clip);
            api.Render.Render2DTexturePremultipliedAlpha(_background.TextureId, (int)Bounds.renderX, (int)Bounds.renderY, _background.Width, _background.Height);
            // Inputs use their own scissor rectangle. Only render their interactive layer
            // when the complete control is visible; its cached background still clips.
            if (Bounds.absY >= _clip.absY && Bounds.absY + Bounds.OuterHeight <= _clip.absY + _clip.OuterHeight)
            {
                _widget.RenderInteractiveElements(deltaTime);
                MouseOverCursor = _widget.MouseOverCursor;
            }
            api.Render.PopScissor();
        }

        public override void OnFocusGained() { base.OnFocusGained(); _widget.OnFocusGained(); }
        public override void OnFocusLost() { base.OnFocusLost(); _widget.OnFocusLost(); }
        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args) => _widget.OnMouseDown(api, args);
        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args) => _widget.OnMouseUp(api, args);
        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args) => _widget.OnMouseMove(api, args);
        public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args) => _widget.OnMouseWheel(api, args);
        public override void OnKeyDown(ICoreClientAPI api, KeyEvent args) => _widget.OnKeyDown(api, args);
        public override void OnKeyUp(ICoreClientAPI api, KeyEvent args) => _widget.OnKeyUp(api, args);
        public override void OnKeyPress(ICoreClientAPI api, KeyEvent args) => _widget.OnKeyPress(api, args);
        public override void Dispose() { _background.Dispose(); _widget.Dispose(); base.Dispose(); }
    }
}
