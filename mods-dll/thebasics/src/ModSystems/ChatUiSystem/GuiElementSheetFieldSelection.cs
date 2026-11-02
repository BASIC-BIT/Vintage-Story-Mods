using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A field's actual layout cell, selectable without forwarding input to its answer.</summary>
internal sealed class GuiElementSheetFieldSelection : GuiElement
{
    private readonly Action _select;
    private readonly Action _focus;
    private readonly LoadedTexture _selectedTexture;
    private readonly LoadedTexture _focusTexture;
    internal string FieldId { get; }
    internal bool Selected { get; set; }
    internal bool Unfinished { get; set; }
    public override bool Focusable => true;

    internal GuiElementSheetFieldSelection(ICoreClientAPI api, ElementBounds bounds, string fieldId, Action select, Action focus) : base(api, bounds)
    {
        FieldId = fieldId;
        _select = select;
        _focus = focus;
        _selectedTexture = new LoadedTexture(api);
        _focusTexture = new LoadedTexture(api);
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        DrawOutline(_selectedTexture, 0.95, 0.72, 0.3, 1);
        DrawOutline(_focusTexture, 0.9, 0.95, 1, 4);
    }

    private void DrawOutline(LoadedTexture texture, double r, double g, double b, double inset)
    {
        using var surface = new ImageSurface(Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
        using var context = new Context(surface);
        context.SetSourceRGBA(r, g, b, 1);
        context.LineWidth = scaled(2);
        var offset = scaled(inset);
        context.Rectangle(offset, offset, Math.Max(0, surface.Width - 2 * offset), Math.Max(0, surface.Height - 2 * offset));
        context.Stroke();
        generateTexture(surface, ref texture);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (InsideClipBounds != null) api.Render.PushScissor(InsideClipBounds, stacking: true);
        try
        {
            if (Selected || Unfinished) api.Render.Render2DTexturePremultipliedAlpha(_selectedTexture.TextureId, Bounds);
            if (HasFocus) api.Render.Render2DTexturePremultipliedAlpha(_focusTexture.TextureId, Bounds);
        }
        finally { if (InsideClipBounds != null) api.Render.PopScissor(); }
    }

    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
    {
        if (!IsPositionInside(args.X, args.Y)) return;
        _select();
        args.Handled = true;
    }

    public override void OnFocusGained()
    {
        base.OnFocusGained();
        _focus();
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!HasFocus || args.KeyCode == (int)GlKeys.Tab) return;
        if (args.KeyCode is (int)GlKeys.Enter or (int)GlKeys.Space) _select();
        args.Handled = true;
    }

    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
        if (HasFocus) args.Handled = true;
    }

    public override void Dispose()
    {
        _selectedTexture.Dispose();
        _focusTexture.Dispose();
        base.Dispose();
    }
}

/// <summary>Composes and renders the actual answer control with no input or focus surface.</summary>
internal sealed class GuiElementSheetPreviewAnswer(ICoreClientAPI api, GuiElement answer) : GuiElement(api, answer.Bounds)
{
    public override void ComposeElements(Context context, ImageSurface surface)
    {
        answer.InsideClipBounds = InsideClipBounds;
        answer.ComposeElements(context, surface);
    }
    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args) { /* Template answers have no input surface. */ }
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args) { /* Template answers have no input surface. */ }
    public override void RenderInteractiveElements(float deltaTime) => answer.RenderInteractiveElements(deltaTime);
    public override void Dispose() { answer.Dispose(); base.Dispose(); }
}
