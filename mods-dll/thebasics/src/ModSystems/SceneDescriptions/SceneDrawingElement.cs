using System;
using Cairo;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.SceneDescriptions;

// The game's dynamic custom-draw element does not dispose its private texture.
internal sealed class SceneDrawingElement : GuiElement
{
    private readonly DrawDelegateWithBounds _draw;
    private LoadedTexture _texture;

    internal SceneDrawingElement(ICoreClientAPI api, ElementBounds bounds, DrawDelegateWithBounds draw) : base(api, bounds)
    {
        _draw = draw;
        _texture = new LoadedTexture(api);
    }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        Redraw();
    }

    internal void Redraw()
    {
        using var surface = new ImageSurface(Format.Argb32, Math.Max(1, Bounds.OuterWidthInt), Math.Max(1, Bounds.OuterHeightInt));
        using var ctx = new Context(surface);
        _draw(ctx, surface, Bounds);
        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref _texture);
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args) { }
    public override void RenderInteractiveElements(float deltaTime) => api.Render.Render2DTexture(_texture.TextureId, Bounds);
    public override void Dispose()
    {
        _texture.Dispose();
        base.Dispose();
    }
}

internal static class SceneDrawingComposerExtensions
{
    internal static GuiComposer AddSceneDrawing(this GuiComposer composer, ElementBounds bounds, DrawDelegateWithBounds draw, string key = null)
    {
        if (!composer.Composed) composer.AddInteractiveElement(new SceneDrawingElement(composer.Api, bounds, draw), key);
        return composer;
    }

    internal static SceneDrawingElement GetSceneDrawing(this GuiComposer composer, string key) => (SceneDrawingElement)composer.GetElement(key);
}
