using System;
using System.Globalization;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public enum ColorPickerMode { Rgb, RgbaWithTheme }

/// <summary>A color swatch with a native palette and optional HSV controls.</summary>
public sealed class GuiElementColorPicker : GuiElementControl
{
    private static readonly string[] Palette = { "#F2E8CC", "#D6AF57", "#E49A3B", "#E67866", "#DC83AE", "#B497D6", "#7EAFE0", "#69B9B2", "#9EBE82", "#73996A", "#B78366", "#B9C2CB", "#FFFFFF", "#737F91", "#333842", "#000000" };
    private static readonly string[] Names = { "Ivory", "Gold", "Amber", "Coral", "Rose", "Lilac", "Sky", "Teal", "Sage", "Moss", "Clay", "Silver", "White", "Slate", "Charcoal", "Black" };
    private readonly Action<string> onChanged;
    private readonly string original;
    private readonly ColorPickerMode mode;
    private LoadedTexture chip;
    private double hue, saturation, brightness;
    private bool chipDirty = true, disposed, synchronizing, dispatching, popupDirty;

    public GuiElementColorPicker(ICoreClientAPI api, ElementBounds bounds, string color, Action<string> onChanged, ColorPickerMode mode = ColorPickerMode.Rgb)
        : base(api, bounds)
    {
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        this.mode = mode;
        original = color ?? "";
        Value = Normalize(color) ?? original;
        ReadHsv();
        chip = new LoadedTexture(api);
        MouseOverCursor = "linkselect";
    }

    public string Value { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsCustomExpanded { get; private set; }
    public GuiComposer PopupComposer { get; private set; }
    public ElementBounds PopupBounds => PopupComposer?.Bounds;
    public Action OnRestoreOriginal { get; set; }
    /// <summary>Preferred left edge in screen coordinates divided by GUI scale. The frame edge takes priority.</summary>
    public double PopupMinimumX { get; set; }
    public override bool Focusable => enabled && !disposed;
    public override double DrawOrder => 0.5;
    public override bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            MouseOverCursor = value ? "linkselect" : null;
            if (!value) { Close(); base.OnFocusLost(); }
            chipDirty = true;
        }
    }

    /// <summary>Accepts valid colors for this mode without changing the draft or firing its callback.</summary>
    public void SetValue(string color)
    {
        var next = Normalize(color);
        if (next == null || next == Value || disposed) return;
        Value = next;
        ReadHsv();
        SyncControls();
    }

    public void Open()
    {
        if (!enabled || disposed || IsOpen) return;
        IsOpen = true;
        base.OnFocusGained();
        RebuildPopup();
        chipDirty = true;
    }

    public void Close()
    {
        IsOpen = false;
        PopupComposer?.UnfocusOwnElements();
        CancelDrag();
        SyncControls();
        chipDirty = true;
    }

    public void ExpandCustom() => SetCustomExpanded(true);

    public void SetCustomExpanded(bool expanded)
    {
        if (disposed || IsCustomExpanded == expanded) return;
        IsCustomExpanded = expanded;
        CancelDrag();
        if (IsOpen) RebuildPopup();
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        chipDirty = true;
    }

    private void RebuildPopup()
    {
        if (dispatching) { popupDirty = true; return; }
        var restoreCustomFocus = PopupComposer?.GetElement("custom")?.HasFocus == true;
        PopupComposer?.Dispose();
        double scale = RuntimeEnv.GUIScale;
        const double width = 284;
        double height = (IsCustomExpanded ? 446 : 208) + (mode == ColorPickerMode.RgbaWithTheme ? 32 : 0);
        double x = Math.Clamp(Math.Max(PopupMinimumX, Bounds.renderX / scale), 4, Math.Max(4, api.Render.FrameWidth / scale - width - 4));
        double y = Math.Clamp((Bounds.renderY + Bounds.InnerHeight) / scale + 5, 4, Math.Max(4, api.Render.FrameHeight / scale - height - 4));
        var bounds = ElementBounds.Fixed(x, y, width, height).WithEmptyParent();
        var popup = PopupComposer = api.Gui.CreateCompo("color-palette", bounds);
        popup.AddShadedDialogBG(ElementBounds.Fill, false)
            .AddStaticText("Color", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold), ElementBounds.Fixed(12, 10, 130, 22))
            .AddSmallButton("×", () => { Close(); return true; }, ElementBounds.Fixed(244, 7, 28, 24), key: "close");
        if (mode == ColorPickerMode.RgbaWithTheme)
            popup.AddSmallButton("Use theme", () => { Choose(""); return true; }, ElementBounds.Fixed(148, 7, 88, 24), key: "theme");
        for (var i = 0; i < Palette.Length; i++)
        {
            var index = i;
            var swatch = new KeyboardSwatch(api, unchecked((int)(0xFF000000u | uint.Parse(Palette[i].AsSpan(1), NumberStyles.HexNumber))),
                ElementBounds.Fixed(12 + i % 8 * 33, 42 + i / 8 * 33, 28, 28), () => Choose(WithOpacity(Palette[index])));
            swatch.TooltipText = Names[i] + " " + Palette[i];
            swatch.ShowToolTip = true;
            popup.AddInteractiveElement(swatch, "palette-" + i);
        }
        popup.AddSmallButton(IsCustomExpanded ? "Custom color   −" : "Custom color   +", () =>
        {
            SetCustomExpanded(!IsCustomExpanded);
            return true;
        }, ElementBounds.Fixed(12, 113, 260, 28), key: "custom");
        if (IsCustomExpanded)
        {
            popup.AddInteractiveElement(new Spectrum(this, ElementBounds.Fixed(12, 151, 260, 112), false), "spectrum");
            popup.AddInteractiveElement(new Spectrum(this, ElementBounds.Fixed(12, 273, 260, 14), true), "hue-strip");
            AddChannel(popup, 0, "Hue", 300);
            AddChannel(popup, 1, "Saturation", 332);
            AddChannel(popup, 2, "Brightness", 364);
        }
        if (mode == ColorPickerMode.RgbaWithTheme) AddChannel(popup, 3, "Opacity", IsCustomExpanded ? 396 : 151);
        var footer = (IsCustomExpanded ? 408 : 169) + (mode == ColorPickerMode.RgbaWithTheme ? 32 : 0);
        popup.AddSmallButton("Original", () => { Choose(original); OnRestoreOriginal?.Invoke(); return true; }, ElementBounds.Fixed(12, footer, 94, 28), key: "original")
            .AddTextInput(ElementBounds.Fixed(168, footer, 104, 28), text =>
            {
                if (!synchronizing && Normalize(text) is { } color) Choose(color);
            }, CairoFont.WhiteSmallText().WithFontSize(14), "hex");
        popup.AddInteractiveElement(new CurrentSwatch(this, ElementBounds.Fixed(130, footer + 3, 28, 24)), "current-swatch");
        popup.Compose(focusFirstElement: false);
        popup.GetTextInput("hex").SetMaxLength(mode == ColorPickerMode.RgbaWithTheme ? 9 : 7);
        for (var i = 0; i < 4; i++)
        {
            if (i < 3 && !IsCustomExpanded || i == 3 && mode != ColorPickerMode.RgbaWithTheme) continue;
            popup.GetSlider("channel-" + i).SetValues(Channel(i), 0, i == 0 ? 360 : 100, 1, i == 0 ? "°" : "%");
            popup.GetTextInput("number-" + i).SetMaxLength(3);
        }
        popupDirty = false;
        SyncControls();
        if (restoreCustomFocus) popup.FocusElement(popup["custom"].TabIndex);
    }

    private void AddChannel(GuiComposer popup, int channel, string label, double top)
    {
        popup.AddStaticText(label, CairoFont.WhiteSmallText().WithFontSize(12), ElementBounds.Fixed(12, top + 5, 76, 22));
        popup.AddSlider(value => { ChangeChannel(channel, value); return true; }, ElementBounds.Fixed(93, top + 5, 115, 20), "channel-" + channel);
        popup.AddTextInput(ElementBounds.Fixed(218, top, 54, 26), text =>
        {
            if (!synchronizing && int.TryParse(text, out var value) && value >= 0 && value <= (channel == 0 ? 360 : 100)) ChangeChannel(channel, value);
        }, CairoFont.WhiteSmallText().WithFontSize(13), "number-" + channel);
    }

    private void Choose(string color)
    {
        if (Value == color) { SyncControls(); return; }
        Value = color;
        ReadHsv();
        SyncControls();
        onChanged(Value);
    }

    private void ChangeChannel(int channel, double value)
    {
        if (synchronizing) return;
        if (channel == 3)
        {
            Choose($"{RgbValue}{(int)Math.Round(Math.Clamp(value, 0, 100) / 100 * 255):X2}");
            return;
        }
        if (channel == 0) hue = Math.Clamp(value, 0, 360);
        if (channel == 1) saturation = Math.Clamp(value, 0, 100);
        if (channel == 2) brightness = Math.Clamp(value, 0, 100);
        EmitHsv();
    }

    private void EmitHsv()
    {
        var rgb = Hsv(hue, saturation, brightness);
        var next = WithOpacity($"#{(int)Math.Round(rgb.R * 255):X2}{(int)Math.Round(rgb.G * 255):X2}{(int)Math.Round(rgb.B * 255):X2}");
        var changed = next != Value;
        Value = next;
        SyncControls();
        if (changed) onChanged(next);
    }

    private int Channel(int channel) => (int)Math.Round(channel == 0 ? hue : channel == 1 ? saturation : channel == 2 ? brightness : Alpha / 255d * 100);
    private string RgbValue => Normalize(Value) is { Length: >= 7 } color ? color[..7] : "#FFFFFF";
    private byte Alpha => mode == ColorPickerMode.RgbaWithTheme && Normalize(Value) is { Length: 9 } color ? byte.Parse(color.AsSpan(7), NumberStyles.HexNumber) : (byte)255;
    private string WithOpacity(string rgb) => mode == ColorPickerMode.RgbaWithTheme && Normalize(Value) is { Length: 9 } ? rgb + Value[7..] : rgb;

    private void SyncControls()
    {
        chipDirty = true;
        if (PopupComposer == null) return;
        synchronizing = true;
        try
        {
            for (var i = 0; i < Palette.Length; i++) PopupComposer.GetColorListPicker("palette-" + i).SetValue(Normalize(Value) is { Length: >= 7 } && RgbValue == Palette[i]);
            var hex = PopupComposer.GetTextInput("hex");
            if (!hex.HasFocus) hex.SetValue(Value);
            for (var i = 0; i < 4; i++)
            {
                if (i < 3 && !IsCustomExpanded || i == 3 && mode != ColorPickerMode.RgbaWithTheme) continue;
                var slider = PopupComposer.GetSlider("channel-" + i);
                slider.SetValues(Channel(i), 0, i == 0 ? 360 : 100, 1, i == 0 ? "°" : "%");
                var number = PopupComposer.GetTextInput("number-" + i);
                if (!number.HasFocus) number.SetValue(Channel(i).ToString(CultureInfo.InvariantCulture));
            }
            if (IsCustomExpanded)
            {
                ((Spectrum)PopupComposer["spectrum"]).Dirty = true;
                ((Spectrum)PopupComposer["hue-strip"]).Dirty = true;
            }
            ((CurrentSwatch)PopupComposer["current-swatch"]).Dirty = true;
        }
        finally { synchronizing = false; }
    }

    private void ReadHsv()
    {
        var rgb = uint.Parse(RgbValue.AsSpan(1), NumberStyles.HexNumber);
        double r = (rgb >> 16 & 255) / 255d, g = (rgb >> 8 & 255) / 255d, b = (rgb & 255) / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));
        if (delta > 0) hue = ((max == r ? (g - b) / delta : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4) * 60 + 360) % 360;
        saturation = max > 0 ? delta / max * 100 : saturation;
        brightness = max * 100;
    }

    private string Normalize(string text)
    {
        if (mode == ColorPickerMode.RgbaWithTheme && text == "") return "";
        return (text?.Length == 7 || mode == ColorPickerMode.RgbaWithTheme && text?.Length == 9) && text[0] == '#' && uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _)
            ? text.ToUpperInvariant() : null;
    }

    private static Color Hsv(double h, double s, double v)
    {
        h = h % 360 / 60;
        double c = v / 100 * s / 100, x = c * (1 - Math.Abs(h % 2 - 1)), m = v / 100 - c;
        var (r, g, b) = (int)h switch { 0 => (c, x, 0d), 1 => (x, c, 0d), 2 => (0d, c, x), 3 => (0d, x, c), 4 => (x, 0d, c), _ => (c, 0d, x) };
        return new Color(r + m, g + m, b + m, 1);
    }

    private void ComposeChip()
    {
        using var surface = new ImageSurface(Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
        using var context = genContext(surface);
        PaintSwatch(context, surface.Width, surface.Height, Value);
        if (HasFocus || IsOpen)
        {
            context.Rectangle(scaled(2), scaled(2), surface.Width - scaled(4), surface.Height - scaled(4));
            context.SetSourceRGBA(1, 0.87, 0.55, 1);
            context.LineWidth = scaled(1);
            context.Stroke();
        }
        if (!enabled) { context.Operator = Operator.DestIn; context.SetSourceRGBA(0, 0, 0, .4); context.Paint(); }
        generateTexture(surface, ref chip);
        chipDirty = false;
    }

    private static void PaintSwatch(Context context, double width, double height, string hex)
    {
        var parsed = hex?.Length is 7 or 9 && hex[0] == '#' && uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
        var rgb = parsed ? uint.Parse(hex.AsSpan(1, 6), NumberStyles.HexNumber) : 0xFFFFFFu;
        var alpha = parsed && hex.Length == 9 ? byte.Parse(hex.AsSpan(7), NumberStyles.HexNumber) / 255d : 1;
        RoundRectangle(context, 1, 1, width - 2, height - 2, 2);
        context.Save();
        context.ClipPreserve();
        if (alpha < 1 || hex == "")
        {
            for (var y = 0; y < height; y += 6)
                for (var x = 0; x < width; x += 6)
                {
                    var shade = (x / 6 + y / 6) % 2 == 0 ? .4 : .7;
                    context.SetSourceRGB(shade, shade, shade);
                    context.Rectangle(x, y, 6, 6);
                    context.Fill();
                }
        }
        context.Restore();
        RoundRectangle(context, 1, 1, width - 2, height - 2, 2);
        context.SetSourceRGBA((rgb >> 16 & 255) / 255d, (rgb >> 8 & 255) / 255d, (rgb & 255) / 255d, hex == "" ? 0 : alpha);
        context.FillPreserve();
        context.SetSourceRGBA(.8, .7, .5, .8);
        context.LineWidth = 1;
        context.Stroke();
    }

    public override void RenderInteractiveElements(float deltaTime) => RenderControl(deltaTime);
    public void RenderControl(float deltaTime)
    {
        if (disposed) return;
        if (chipDirty) ComposeChip();
        MouseOverCursor = enabled ? "linkselect" : null;
        SetupWizardViewportClip.Render(api, InsideClipBounds, () => api.Render.Render2DTexturePremultipliedAlpha(chip.TextureId, Bounds));
    }

    /// <summary>Render after the containing dialog so the palette appears above its ordinary controls.</summary>
    public void RenderPopup(float deltaTime)
    {
        if (!IsOpen || disposed) return;
        if (popupDirty) RebuildPopup();
        api.Render.GlPushMatrix();
        api.Render.GlTranslate(0, 0, 400);
        try { PopupComposer.Render(deltaTime); }
        finally { api.Render.GlPopMatrix(); }
        MouseOverCursor = PopupComposer.MouseOverCursor ?? "linkselect";
    }

    public override bool IsPositionInside(int x, int y) => base.IsPositionInside(x, y) || IsOpen && PopupBounds.PointInside(x, y);
    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || disposed || args.Handled) return;
        if (IsOpen)
        {
            if (!PopupBounds.PointInside(args.X, args.Y)) { Close(); base.OnFocusGained(); args.Handled = true; return; }
            dispatching = true;
            try { PopupComposer.OnMouseDown(args); }
            finally { dispatching = false; }
            if (popupDirty) RebuildPopup();
            else SyncControls();
            args.Handled = true;
            return;
        }
        if (args.Button != EnumMouseButton.Left || !base.IsPositionInside(args.X, args.Y)) return;
        Open();
        args.Handled = true;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (!IsOpen || !enabled || disposed) return;
        dispatching = true;
        try { PopupComposer.OnMouseUp(args); }
        finally { dispatching = false; }
        if (popupDirty) RebuildPopup();
        args.Handled = true;
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (!IsOpen || !enabled || disposed) return;
        PopupComposer.OnMouseMove(args);
        args.Handled = true;
    }

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (!IsOpen || !enabled || disposed) return;
        PopupComposer.OnMouseWheel(args);
        args.SetHandled();
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!enabled || disposed || args.Handled) return;
        if (IsOpen)
        {
            if (args.KeyCode == (int)GlKeys.Escape) { Close(); base.OnFocusGained(); args.Handled = true; return; }
            dispatching = true;
            try { PopupComposer.OnKeyDown(args, true); }
            finally { dispatching = false; }
            if (popupDirty) RebuildPopup();
            else SyncControls();
            args.Handled = true;
        }
        else if (HasFocus && (args.KeyCode == (int)GlKeys.Enter || args.KeyCode == (int)GlKeys.KeypadEnter || args.KeyCode == (int)GlKeys.Space))
        {
            Open();
            args.Handled = true;
        }
    }

    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
        if (!IsOpen || !enabled || disposed || args.Handled) return;
        PopupComposer.OnKeyPress(args);
        args.Handled = true;
    }

    public override void OnFocusGained() { if (enabled && !disposed) { base.OnFocusGained(); chipDirty = true; } }
    public override void OnFocusLost() { base.OnFocusLost(); Close(); }
    private void CancelDrag()
    {
        if (PopupComposer == null) return;
        PopupComposer.OnMouseUp(new MouseEvent(0, 0, EnumMouseButton.Left, 0));
    }

    public override void Dispose()
    {
        if (disposed) return;
        Close();
        disposed = true;
        PopupComposer?.Dispose();
        chip.Dispose();
        base.Dispose();
    }

    private sealed class KeyboardSwatch : GuiElementColorListPicker
    {
        private readonly Action choose;
        public KeyboardSwatch(ICoreClientAPI api, int color, ElementBounds bounds, Action choose) : base(api, color, bounds) { this.choose = choose; MouseOverCursor = "linkselect"; }
        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Left || args.Handled) return;
            choose();
            args.Handled = true;
        }
        public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
        {
            if (!HasFocus || args.Handled || args.KeyCode != (int)GlKeys.Enter && args.KeyCode != (int)GlKeys.Space && args.KeyCode != (int)GlKeys.KeypadEnter) return;
            choose();
            args.Handled = true;
        }
    }

    private sealed class CurrentSwatch : GuiElement
    {
        private readonly GuiElementColorPicker owner;
        private LoadedTexture texture;
        public bool Dirty = true;
        public CurrentSwatch(GuiElementColorPicker owner, ElementBounds bounds) : base(owner.api, bounds) { this.owner = owner; texture = new LoadedTexture(api); }
        public override void RenderInteractiveElements(float deltaTime)
        {
            if (Dirty)
            {
                using var surface = new ImageSurface(Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
                using var context = genContext(surface);
                PaintSwatch(context, surface.Width, surface.Height, owner.Value);
                generateTexture(surface, ref texture);
                Dirty = false;
            }
            api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, Bounds);
        }
        public override void Dispose() { texture.Dispose(); base.Dispose(); }
    }

    private sealed class Spectrum : GuiElementControl
    {
        private readonly GuiElementColorPicker owner;
        private readonly bool hueOnly;
        private LoadedTexture texture;
        private bool dragging;
        public bool Dirty = true;
        public override bool Focusable => false;
        public Spectrum(GuiElementColorPicker owner, ElementBounds bounds, bool hueOnly) : base(owner.api, bounds)
        {
            this.owner = owner;
            this.hueOnly = hueOnly;
            texture = new LoadedTexture(api);
            MouseOverCursor = "linkselect";
        }
        public override void ComposeElements(Context context, ImageSurface surface) { Bounds.CalcWorldBounds(); Dirty = true; }
        public override void RenderInteractiveElements(float deltaTime)
        {
            if (Dirty)
            {
                using var surface = new ImageSurface(Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
                using var context = genContext(surface);
                using var gradient = new LinearGradient(0, 0, surface.Width, 0);
                if (hueOnly)
                {
                    for (var i = 0; i <= 6; i++) gradient.AddColorStop(i / 6d, Hsv(i * 60, 100, 100));
                }
                else
                {
                    gradient.AddColorStop(0, new Color(1, 1, 1));
                    gradient.AddColorStop(1, Hsv(owner.hue, 100, 100));
                }
                context.SetSource(gradient);
                context.Paint();
                if (!hueOnly)
                {
                    using var shade = new LinearGradient(0, 0, 0, surface.Height);
                    shade.AddColorStop(0, new Color(0, 0, 0, 0));
                    shade.AddColorStop(1, new Color(0, 0, 0, 1));
                    context.SetSource(shade);
                    context.Paint();
                }
                double x = (hueOnly ? owner.hue / 360 : owner.saturation / 100) * (surface.Width - 1);
                double y = hueOnly ? surface.Height / 2d : (1 - owner.brightness / 100) * (surface.Height - 1);
                context.Arc(x, y, scaled(5), 0, Math.PI * 2);
                context.SetSourceRGBA(0, 0, 0, 1);
                context.LineWidth = scaled(3);
                context.StrokePreserve();
                context.SetSourceRGBA(1, 1, 1, 1);
                context.LineWidth = scaled(1.5);
                context.Stroke();
                generateTexture(surface, ref texture);
                Dirty = false;
            }
            api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, Bounds);
        }
        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Left || args.Handled) return;
            dragging = true;
            Move(args.X, args.Y);
            args.Handled = true;
        }
        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            if (!dragging) return;
            Move(args.X, args.Y);
            args.Handled = true;
        }
        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args) => dragging = false;
        private void Move(int x, int y)
        {
            var fx = Math.Clamp((x - Bounds.renderX) / Math.Max(1, Bounds.InnerWidth - 1), 0, 1);
            if (hueOnly) owner.hue = fx * 360;
            else
            {
                owner.saturation = fx * 100;
                owner.brightness = (1 - Math.Clamp((y - Bounds.renderY) / Math.Max(1, Bounds.InnerHeight - 1), 0, 1)) * 100;
            }
            owner.EmitHsv();
        }
        public override void Dispose() { dragging = false; texture.Dispose(); base.Dispose(); }
    }
}
