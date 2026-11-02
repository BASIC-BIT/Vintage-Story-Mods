using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A labeled setup switch with native focus, mouse and keyboard input.</summary>
public sealed class GuiElementSetupToggle : GuiElementControl
{
    private readonly Action<bool> onChanged;
    private readonly CairoFont font = CairoFont.WhiteSmallText().WithFontSize(15).WithWeight(FontWeight.Bold);
    private LoadedTexture texture;
    private bool hovered, pressed, needsTexture = true, fontReady, disposed;

    public GuiElementSetupToggle(ICoreClientAPI api, bool value, Action<bool> onChanged, ElementBounds bounds)
        : base(api, bounds)
    {
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        Value = value;
        texture = new LoadedTexture(api);
        MouseOverCursor = "linkselect";
    }

    public bool Value { get; private set; }
    public override bool Focusable => enabled && !disposed;
    public override bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            MouseOverCursor = value ? "linkselect" : null;
            if (!value) OnFocusLost();
            needsTexture = true;
        }
    }

    public void SetValue(bool value)
    {
        if (Value == value) return;
        Value = value;
        needsTexture = true;
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        needsTexture = true;
    }

    private void ComposeTexture()
    {
        using var surface = new ImageSurface(Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
        using var context = genContext(surface);
        double width = surface.Width, height = surface.Height;
        double shade = pressed ? 0.82 : hovered ? 1.12 : 1;
        var background = GuiStyle.ButtonBackColor;
        RoundRectangle(context, scaled(0.5), scaled(0.5), width - scaled(1), height - scaled(1), scaled(5));
        context.SetSourceRGBA((Value ? 0.30 : background[0]) * shade,
            (Value ? 0.25 : background[1]) * shade, (Value ? 0.16 : background[2]) * shade, 0.98);
        context.FillPreserve();
        context.SetSourceRGBA(0.78, 0.65, 0.41, hovered || Value ? 0.88 : 0.45);
        context.LineWidth = scaled(1);
        context.Stroke();

        context.MoveTo(scaled(6), scaled(1.5));
        context.LineTo(width - scaled(6), scaled(1.5));
        context.SetSourceRGBA(1, 1, 1, pressed ? 0.03 : 0.10);
        context.Stroke();

        font.SetupContext(context);
        fontReady = true;
        context.SetSourceRGBA(Value ? 0.98 : 0.82, Value ? 0.88 : 0.79, Value ? 0.67 : 0.73, 1);
        var label = Value ? "On" : "Off";
        var extents = context.TextExtents(label);
        context.MoveTo(scaled(12), (height - extents.Height) / 2 - extents.YBearing);
        context.ShowText(label);

        double trackX = width - scaled(48), trackY = (height - scaled(16)) / 2;
        RoundRectangle(context, trackX, trackY, scaled(40), scaled(16), scaled(8));
        context.SetSourceRGBA(0.08, 0.07, 0.05, 0.8);
        context.FillPreserve();
        context.SetSourceRGBA(0.72, 0.59, 0.36, Value ? 0.65 : 0.25);
        context.Stroke();
        context.Arc(trackX + scaled(Value ? 32 : 8), trackY + scaled(8), scaled(5.5), 0, Math.PI * 2);
        context.SetSourceRGBA(Value ? 0.93 : 0.64, Value ? 0.77 : 0.61, Value ? 0.45 : 0.54, 1);
        context.Fill();

        if (enabled && HasFocus)
        {
            RoundRectangle(context, scaled(2.5), scaled(2.5), width - scaled(5), height - scaled(5), scaled(3));
            context.SetSourceRGBA(0.98, 0.83, 0.53, 1);
            context.LineWidth = scaled(1);
            context.Stroke();
        }
        if (!enabled)
        {
            context.Operator = Operator.DestIn;
            context.SetSourceRGBA(0, 0, 0, 0.38);
            context.Paint();
        }
        generateTexture(surface, ref texture);
        needsTexture = false;
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (disposed) return;
        var over = enabled && Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        if (hovered != over) { hovered = over; needsTexture = true; }
        if (needsTexture) ComposeTexture();
        SetupWizardViewportClip.Render(api, InsideClipBounds, () => api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, Bounds));
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || disposed || args.Handled || args.Button != EnumMouseButton.Left) return;
        pressed = true;
        needsTexture = true;
        args.Handled = true;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        var activate = pressed && enabled && !disposed && args.Button == EnumMouseButton.Left && IsPositionInside(args.X, args.Y);
        if (pressed) args.Handled = true;
        pressed = false;
        needsTexture = true;
        if (activate) Toggle();
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!enabled || disposed || !HasFocus || args.Handled ||
            args.KeyCode != (int)GlKeys.Space && args.KeyCode != (int)GlKeys.Enter && args.KeyCode != (int)GlKeys.KeypadEnter) return;
        args.Handled = true;
        Toggle();
    }

    private void Toggle()
    {
        SetValue(!Value);
        api.Gui.PlaySound("toggleswitch");
        onChanged(Value);
    }

    public override void OnFocusGained()
    {
        if (!enabled || disposed) return;
        base.OnFocusGained();
        needsTexture = true;
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        pressed = false;
        needsTexture = true;
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        texture.Dispose();
        if (fontReady) font.Dispose();
        base.Dispose();
    }
}
