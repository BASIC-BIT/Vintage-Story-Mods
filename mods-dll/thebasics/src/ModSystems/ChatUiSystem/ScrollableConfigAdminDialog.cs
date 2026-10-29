using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal class ScrollableConfigAdminDialog : GuiJsonDialog
{
    // ponytail: reuse vanilla's private composer and controls; rerun native previews on game API upgrades.
    private static readonly MethodInfo ComposeElement = typeof(GuiJsonDialog).GetMethod("ComposeElement", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly JsonDialogSettings _settings;
    private readonly IReadOnlyList<ConfigAdminPrivilegeSuggestion> _privilegeSuggestions;
    private readonly Dictionary<string, (string Control, double Y)> _controls = new();
    private readonly Dictionary<string, GuiElementPrivilegePicker> _privilegePickers = new();
    private readonly Dictionary<string, GuiElementColorPicker> _colorPickers = new();
    private ElementBounds _content;
    private double _viewport;

    internal ScrollableConfigAdminDialog(JsonDialogSettings settings, ICoreClientAPI api, bool focusFirstElement,
        IEnumerable<ConfigAdminPrivilegeSuggestion> privilegeSuggestions = null)
        : base(settings, api, focusFirstElement)
    {
        _settings = settings;
        _privilegeSuggestions = ConfigAdminPrivilegeSuggestions.ForClient(privilegeSuggestions,
            ConfigAdminSettingRegistry.Settings.Where(setting => setting.IsPrivilege).Select(setting => settings.OnGet?.Invoke(setting.Key)));
        ComposeScrollable();
    }

    public override void Recompose() => ComposeScrollable();

    private void ComposeScrollable()
    {
        Composers.ClearComposers();
        _controls.Clear();
        _privilegePickers.Clear();
        _colorPickers.Clear();
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
        foreach (var control in _controls)
        {
            if (interactive.TryGetValue(control.Value.Control, out var booleanWidget) && booleanWidget is GuiElementSwitch)
            {
                var booleanKey = control.Key;
                var bounds = booleanWidget.Bounds.FlatCopy();
                bounds.fixedWidth = 112;
                bounds.fixedHeight = 28;
                var toggle = new GuiElementSetupToggle(capi, _settings.OnGet?.Invoke(booleanKey) == "1",
                    value => _settings.OnSet?.Invoke(booleanKey, value ? "1" : "0"), bounds)
                    { TabIndex = booleanWidget.TabIndex, InsideClipBounds = booleanWidget.InsideClipBounds };
                elements[control.Value.Control] = interactive[control.Value.Control] = toggle;
                booleanWidget.Dispose();
                continue;
            }
            if (interactive.TryGetValue(control.Value.Control, out var colorWidget) && colorWidget is GuiElementTextInput input &&
                ColorMode(control.Key) is { } mode)
            {
                var colorKey = control.Key;
                var bounds = input.Bounds.FlatCopy();
                bounds.fixedX += bounds.fixedWidth - 32;
                bounds.fixedWidth = 32;
                input.Bounds.fixedWidth -= 40;
                var colorPicker = new GuiElementColorPicker(capi, bounds, _settings.OnGet?.Invoke(colorKey), value => input.SetValue(value), mode)
                    { InsideClipBounds = input.InsideClipBounds };
                var onTextChanged = input.OnTextChanged;
                input.OnTextChanged = value => { colorPicker.SetValue(value); onTextChanged?.Invoke(value); };
                composer.AddInteractiveElement(colorPicker, "color-picker-" + colorKey);
                _colorPickers[colorKey] = colorPicker;
                continue;
            }
            if (!ConfigAdminSettingRegistry.TryGet(control.Key, out var definition) || !definition.IsPrivilege ||
                !interactive.TryGetValue(control.Value.Control, out var widget)) continue;
            var font = ((GuiElementTextBase)widget).Font.Clone();
            var key = control.Key;
            var picker = new GuiElementPrivilegePicker(capi, widget.Bounds, _settings.OnGet?.Invoke(key), _privilegeSuggestions,
                value => _settings.OnSet?.Invoke(key, value), font) { TabIndex = widget.TabIndex, InsideClipBounds = widget.InsideClipBounds };
            elements[control.Value.Control] = interactive[control.Value.Control] = picker;
            _privilegePickers[key] = picker;
            widget.Dispose();
        }
        var colorControls = _controls.Where(pair => ColorMode(pair.Key) != null).Select(pair => pair.Value.Control).ToHashSet();
        foreach (var pair in elements.Where(pair => pair.Value.Bounds.ParentBounds == _content).ToList())
        {
            var widget = new ScrollingWidget(capi, pair.Value, clip, colorControls.Contains(pair.Key) || pair.Value is GuiElementColorPicker or GuiElementSetupToggle);
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
        ScrollTo(target.Y);
        SingleComposer.FocusElement(SingleComposer.GetElement(target.Control).TabIndex);
        return true;
    }

    public override void OnKeyDown(KeyEvent args)
    {
        OpenColorPicker()?.OnKeyDown(capi, args);
        if (args.Handled) return;
        base.OnKeyDown(args);
        // Keyboard tabbing must keep the focused setting inside the viewport too.
        var focused = _controls.Values.FirstOrDefault(control => SingleComposer.GetElement(control.Control).HasFocus);
        if (focused.Control == null)
        {
            var colorKey = _colorPickers.FirstOrDefault(pair => pair.Value.HasFocus).Key;
            if (colorKey != null) focused = _controls[colorKey];
        }
        if (focused.Control != null && (focused.Y < -_content.fixedY / _settings.SizeMultiplier || focused.Y + 28 > -_content.fixedY / _settings.SizeMultiplier + _viewport))
        {
            ScrollTo(focused.Y);
        }
    }

    public override void OnKeyPress(KeyEvent args)
    {
        OpenColorPicker()?.OnKeyPress(capi, args);
        if (!args.Handled) base.OnKeyPress(args);
    }

    public override void OnKeyUp(KeyEvent args)
    {
        if (OpenColorPicker() != null) { args.Handled = true; return; }
        base.OnKeyUp(args);
    }

    public override void OnRenderGUI(float deltaTime)
    {
        var color = OpenColorPicker();
        GuiPopupHoverGuard.RenderBackground(SingleComposer, color != null, () => base.OnRenderGUI(deltaTime));
        color?.RenderPopup(deltaTime);
        if (color != null) MouseOverCursor = color.MouseOverCursor;
        var picker = OpenPrivilegePicker();
        if (picker?.IsPositionInside(capi.Input.MouseX, capi.Input.MouseY) == true)
            MouseOverCursor = picker.MouseOverCursor;
    }

    public override bool OnEscapePressed()
    {
        if (OpenColorPicker() is { } color) { color.Close(); return true; }
        var picker = OpenPrivilegePicker();
        if (picker == null) return base.OnEscapePressed();
        picker.CloseSuggestions();
        return true;
    }

    public override void OnMouseDown(MouseEvent args)
    {
        OpenColorPicker()?.OnMouseDown(capi, args);
        if (args.Handled) return;
        OpenPrivilegePicker()?.OnMouseDown(capi, args);
        if (!args.Handled) base.OnMouseDown(args);
    }

    public override void OnMouseUp(MouseEvent args)
    {
        OpenColorPicker()?.OnMouseUp(capi, args);
        if (!args.Handled) base.OnMouseUp(args);
    }

    public override void OnMouseMove(MouseEvent args)
    {
        OpenColorPicker()?.OnMouseMove(capi, args);
        if (args.Handled) return;
        OpenPrivilegePicker()?.OnMouseMove(capi, args);
        if (!args.Handled) base.OnMouseMove(args);
    }

    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        OpenColorPicker()?.OnMouseWheel(capi, args);
        if (args.IsHandled) return;
        OpenPrivilegePicker()?.OnMouseWheel(capi, args);
        if (!args.IsHandled) base.OnMouseWheel(args);
    }

    private GuiElementPrivilegePicker OpenPrivilegePicker() => _privilegePickers.Values.FirstOrDefault(picker => picker.IsSuggestionsOpen);
    private GuiElementColorPicker OpenColorPicker() => _colorPickers.Values.FirstOrDefault(picker => picker.IsOpen);

    private static ColorPickerMode? ColorMode(string key) => key switch
    {
        "EmoteColor" or "OOCColor" or "GlobalOOCColor" => ColorPickerMode.Rgb,
        "NametagBackgroundColor" or "NametagBorderColor" => ColorPickerMode.RgbaWithTheme,
        _ => null
    };

    private void ScrollTo(double rowY)
    {
        var y = Math.Clamp(rowY - 12, 0, Math.Max(0, _content.fixedHeight / _settings.SizeMultiplier - _viewport));
        SingleComposer.GetScrollbar("settings-scrollbar").CurrentYPosition = (float)y;
        Scroll((float)y);
    }

    private void Scroll(float value)
    {
        foreach (var picker in _privilegePickers.Values) picker.CloseSuggestions();
        foreach (var picker in _colorPickers.Values) picker.Close();
        _content.fixedY = -value * _settings.SizeMultiplier;
        _content.MarkDirtyRecursive();
        _content.CalcWorldBounds();
    }

    private static double Height(DialogRow row) => row.TopPadding + row.Elements.Max(element => element.Height) + row.BottomPadding;

    internal sealed class ScrollingWidget : GuiElement
    {
        internal GuiElement Widget => _widget;
        private readonly GuiElement _widget;
        private readonly ElementBounds _clip;
        private readonly bool _colorControl;
        private LoadedTexture _background;

        internal ScrollingWidget(ICoreClientAPI api, GuiElement widget, ElementBounds clip, bool colorControl = false) : base(api, widget.Bounds)
        {
            _widget = widget;
            _clip = clip;
            _colorControl = colorControl;
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
                if (_widget is GuiElementPrivilegePicker picker) picker.RenderControl(deltaTime);
                else if (_widget is GuiElementColorPicker color) color.RenderControl(deltaTime);
                else _widget.RenderInteractiveElements(deltaTime);
                MouseOverCursor = _widget.MouseOverCursor;
            }
            api.Render.PopScissor();
            if (_widget is GuiElementPrivilegePicker privilegePicker) privilegePicker.RenderSuggestions(deltaTime);
        }

        public override void OnFocusGained() { base.OnFocusGained(); _widget.OnFocusGained(); }
        public override void OnFocusLost() { base.OnFocusLost(); _widget.OnFocusLost(); }
        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            if (_colorControl && (!_clip.PointInside(args.X, args.Y) || Bounds.absY < _clip.absY ||
                Bounds.absY + Bounds.OuterHeight > _clip.absY + _clip.OuterHeight)) return;
            if (_widget is GuiElementPrivilegePicker picker && !picker.IsSuggestionsOpen && !_clip.PointInside(args.X, args.Y)) return;
            _widget.OnMouseDown(api, args);
        }
        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args) => _widget.OnMouseUp(api, args);
        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args) => _widget.OnMouseMove(api, args);
        public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args) => _widget.OnMouseWheel(api, args);
        public override void OnKeyDown(ICoreClientAPI api, KeyEvent args) => _widget.OnKeyDown(api, args);
        public override void OnKeyUp(ICoreClientAPI api, KeyEvent args) => _widget.OnKeyUp(api, args);
        public override void OnKeyPress(ICoreClientAPI api, KeyEvent args) => _widget.OnKeyPress(api, args);
        public override void Dispose() { _background.Dispose(); _widget.Dispose(); base.Dispose(); }
    }
}
