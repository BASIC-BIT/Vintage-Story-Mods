using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ChatUiSystem;

public enum SetupTopicCardIcon
{
    Chat,
    Teleportation,
    Notifications,
    PlayerDisplay,
    Map,
    Characters,
    Roleplay,
    ServerTools
}

/// <summary>A topic choice with native button input and an integrated icon, title and description.</summary>
public sealed class GuiElementSetupTopicCard : GuiElementTextButton
{
    private readonly string description;
    private readonly SetupTopicCardIcon icon;
    private readonly CairoFont titleFont = CairoFont.WhiteSmallText().WithFontSize(17).WithWeight(FontWeight.Bold);
    private readonly CairoFont descriptionFont = CairoFont.WhiteSmallText().WithFontSize(14)
        .WithColor(new[] { 0.86, 0.83, 0.76, 1.0 });
    private readonly TextDrawUtil textUtil = new();
    private LoadedTexture normalTexture;
    private LoadedTexture hoverTexture;
    private LoadedTexture pressedTexture;
    private LoadedTexture disabledTexture;
    private LoadedTexture focusTexture;
    private bool pressed;
    private bool disposed;
    private bool fontsReady;

    public GuiElementSetupTopicCard(ICoreClientAPI api, string title, string description,
        SetupTopicCardIcon icon, ActionConsumable onClick, ElementBounds bounds)
        : base(api, title, CairoFont.WhiteSmallText().WithFontSize(17),
            CairoFont.WhiteSmallText().WithFontSize(17), onClick, bounds, EnumButtonStyle.None)
    {
        this.description = description ?? throw new ArgumentNullException(nameof(description));
        this.icon = icon;
        normalTexture = new LoadedTexture(api);
        hoverTexture = new LoadedTexture(api);
        pressedTexture = new LoadedTexture(api);
        disabledTexture = new LoadedTexture(api);
        focusTexture = new LoadedTexture(api);
    }

    public override bool Focusable => Visible && Enabled;
    internal double DescriptionDrawBottom { get; private set; }
    internal double DescriptionFontSize => descriptionFont.UnscaledFontsize;

    public override void BeforeCalcBounds()
    {
        // The native text button otherwise replaces the card bounds with its one-line text size.
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        ComposeCard(ref normalTexture, false, false, false);
        ComposeCard(ref hoverTexture, true, false, false);
        ComposeCard(ref pressedTexture, false, true, false);
        ComposeCard(ref disabledTexture, false, false, true);

        using var focusSurface = new ImageSurface(Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
        using var focusContext = genContext(focusSurface);
        RoundRectangle(focusContext, scaled(3), scaled(3), focusSurface.Width - scaled(6), focusSurface.Height - scaled(6), scaled(4));
        focusContext.SetSourceRGBA(0.92, 0.78, 0.48, 0.95);
        focusContext.LineWidth = scaled(2);
        focusContext.Stroke();
        generateTexture(focusSurface, ref focusTexture);
    }

    private void ComposeCard(ref LoadedTexture texture, bool hover, bool down, bool disabled)
    {
        using var surface = new ImageSurface(Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
        using var context = genContext(surface);
        double width = surface.Width;
        double height = surface.Height;
        double shade = down ? 0.78 : hover ? 1.14 : 1;
        var background = GuiStyle.ButtonBackColor;
        RoundRectangle(context, scaled(1), scaled(1), width - scaled(2), height - scaled(2), scaled(6));
        context.SetSourceRGBA(background[0] * shade, background[1] * shade, background[2] * shade, 0.98);
        context.FillPreserve();
        context.SetSourceRGBA(0.70, 0.57, 0.36, hover ? 0.95 : 0.55);
        context.LineWidth = scaled(1);
        context.Stroke();

        // A small bevel and a warm edge use the same visual vocabulary as the game's buttons.
        context.MoveTo(scaled(8), scaled(2));
        context.LineTo(width - scaled(8), scaled(2));
        context.SetSourceRGBA(1, 1, 1, down ? 0.04 : 0.12);
        context.Stroke();
        RoundRectangle(context, scaled(1), scaled(12), scaled(3), height - scaled(24), scaled(1));
        context.SetSourceRGBA(0.78, 0.63, 0.39, hover ? 1 : 0.80);
        context.Fill();

        context.Save();
        if (down) context.Translate(scaled(1), scaled(1));
        context.Save();
        context.Translate(scaled(16), scaled(19));
        context.Scale(scaled(0.875), scaled(0.875));
        context.SetSourceRGBA(0.86, 0.73, 0.49, 1);
        context.LineWidth = 1.7;
        context.LineJoin = LineJoin.Round;
        context.LineCap = LineCap.Round;
        DrawIcon(context, icon);
        context.Restore();

        var cardTitleFont = titleFont.WithFontSize(17);
        cardTitleFont.SetupContext(context);
        var titleWidth = Math.Max(1, width - scaled(66));
        var textWidth = context.TextExtents(Text).XAdvance;
        if (textWidth > titleWidth)
            cardTitleFont.WithFontSize((float)Math.Max(14, 17 * titleWidth / textWidth));
        cardTitleFont.SetupContext(context);
        textUtil.DrawTextLine(context, cardTitleFont, Text, scaled(54), scaled(22 + (17 - cardTitleFont.UnscaledFontsize) / 2));
        var descriptionWidth = Math.Max(1, width - scaled(32));
        var descriptionHeight = Math.Max(1, height - scaled(67));
        descriptionFont.WithFontSize(14);
        while (descriptionFont.UnscaledFontsize > 12 &&
               textUtil.GetMultilineTextHeight(descriptionFont, description, descriptionWidth, EnumLinebreakBehavior.AfterWord) > descriptionHeight)
            descriptionFont.WithFontSize((float)Math.Max(12, descriptionFont.UnscaledFontsize - 0.25));
        var drawnHeight = textUtil.AutobreakAndDrawMultilineTextAt(context, descriptionFont, description,
            scaled(16), scaled(58), descriptionWidth);
        // Include the pressed state's one-pixel shift in the reported bound.
        DescriptionDrawBottom = scaled(59) + drawnHeight;
        fontsReady = true;
        context.Restore();

        if (disabled)
        {
            context.Operator = Operator.DestIn;
            context.SetSourceRGBA(0, 0, 0, 0.45);
            context.Paint();
        }
        generateTexture(surface, ref texture);
    }

    internal static void DrawIcon(Context context, SetupTopicCardIcon icon)
    {
        context.NewPath();
        switch (icon)
        {
            case SetupTopicCardIcon.Chat:
                RoundRectangle(context, 9, 10, 23, 16, 3);
                context.Stroke();
                RoundRectangle(context, 0, 0, 25, 17, 3);
                context.Stroke();
                context.MoveTo(4, 17);
                context.LineTo(4, 23);
                context.LineTo(11, 17);
                context.Stroke();
                context.MoveTo(6, 6);
                context.LineTo(19, 6);
                context.MoveTo(6, 11);
                context.LineTo(15, 11);
                context.Stroke();
                break;
            case SetupTopicCardIcon.Teleportation:
                context.Arc(3, 25, 3, 0, Math.PI * 2);
                context.Stroke();
                context.MoveTo(6, 25);
                context.LineTo(15, 25);
                context.LineTo(15, 7);
                context.LineTo(30, 7);
                context.MoveTo(24, 1);
                context.LineTo(30, 7);
                context.LineTo(24, 13);
                context.Stroke();
                break;
            case SetupTopicCardIcon.Notifications:
                context.MoveTo(5, 23);
                context.CurveTo(10, 17, 7, 5, 16, 5);
                context.CurveTo(25, 5, 22, 17, 27, 23);
                context.ClosePath();
                context.Stroke();
                context.MoveTo(16, 2);
                context.LineTo(16, 5);
                context.Stroke();
                context.Arc(16, 24, 3, 0, Math.PI);
                context.Stroke();
                break;
            case SetupTopicCardIcon.PlayerDisplay:
                RoundRectangle(context, 0, 3, 32, 23, 3);
                context.Stroke();
                context.Arc(8, 10, 3, 0, Math.PI * 2);
                context.Stroke();
                context.MoveTo(3, 21);
                context.CurveTo(3, 15, 13, 15, 13, 21);
                context.MoveTo(17, 10);
                context.LineTo(27, 10);
                context.MoveTo(17, 17);
                context.LineTo(25, 17);
                context.Stroke();
                break;
            case SetupTopicCardIcon.Map:
                context.MoveTo(1, 7);
                context.LineTo(10, 3);
                context.LineTo(21, 7);
                context.LineTo(31, 3);
                context.LineTo(31, 24);
                context.LineTo(21, 28);
                context.LineTo(10, 24);
                context.LineTo(1, 28);
                context.ClosePath();
                context.MoveTo(10, 3);
                context.LineTo(10, 24);
                context.MoveTo(21, 7);
                context.LineTo(21, 28);
                context.Stroke();
                break;
            case SetupTopicCardIcon.Characters:
                context.Arc(10, 7, 5, 0, Math.PI * 2);
                context.Stroke();
                context.Arc(23, 9, 4, 0, Math.PI * 2);
                context.Stroke();
                context.MoveTo(1, 28);
                context.CurveTo(1, 13, 19, 13, 19, 28);
                context.MoveTo(20, 17);
                context.CurveTo(28, 14, 32, 21, 31, 28);
                context.Stroke();
                break;
            case SetupTopicCardIcon.Roleplay:
                context.MoveTo(3, 4);
                context.CurveTo(11, 0, 21, 0, 29, 4);
                context.LineTo(28, 16);
                context.CurveTo(27, 24, 21, 29, 16, 30);
                context.CurveTo(11, 29, 5, 24, 4, 16);
                context.ClosePath();
                context.MoveTo(8, 11);
                context.CurveTo(10, 9, 12, 9, 14, 11);
                context.MoveTo(18, 11);
                context.CurveTo(20, 9, 22, 9, 24, 11);
                context.MoveTo(9, 18);
                context.CurveTo(12, 24, 20, 24, 23, 18);
                context.Stroke();
                break;
            case SetupTopicCardIcon.ServerTools:
                context.MoveTo(20, 1);
                context.CurveTo(12, 1, 9, 8, 13, 14);
                context.LineTo(2, 24);
                context.LineTo(7, 29);
                context.LineTo(19, 17);
                context.CurveTo(25, 20, 31, 16, 30, 9);
                context.LineTo(25, 14);
                context.LineTo(19, 8);
                context.LineTo(24, 3);
                context.ClosePath();
                context.Stroke();
                break;
        }
        context.NewPath();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (!Visible || disposed) return;
        bool hover = enabled && Bounds.PointInside(api.Input.MouseX, api.Input.MouseY);
        var texture = !enabled ? disabledTexture : pressed ? pressedTexture : hover ? hoverTexture : normalTexture;
        api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, Bounds);
        if (enabled && HasFocus) api.Render.Render2DTexturePremultipliedAlpha(focusTexture.TextureId, Bounds);
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseDownOnElement(api, args);
        pressed = Visible && enabled;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseUp(api, args);
        pressed = false;
    }

    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseUpOnElement(api, args);
        pressed = false;
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        pressed = false;
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!Visible || !enabled || !HasFocus || args.Handled) return;
        if (args.KeyCode == (int)GlKeys.Space || args.KeyCode == (int)GlKeys.KeypadEnter)
        {
            var enter = new KeyEvent { KeyCode = (int)GlKeys.Enter };
            base.OnKeyDown(api, enter);
            args.Handled = enter.Handled;
            return;
        }
        base.OnKeyDown(api, args);
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        normalTexture.Dispose();
        hoverTexture.Dispose();
        pressedTexture.Dispose();
        disabledTexture.Dispose();
        focusTexture.Dispose();
        if (fontsReady)
        {
            titleFont.Dispose();
            descriptionFont.Dispose();
        }
        base.Dispose();
    }
}
