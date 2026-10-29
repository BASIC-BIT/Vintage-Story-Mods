using Cairo;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

internal sealed class GuiElementSetupPreviewDraw : GuiElement
{
    private readonly DrawDelegateWithBounds draw;
    private LoadedTexture texture;
    private bool disposed;
    internal int TextureId => texture.TextureId;

    internal GuiElementSetupPreviewDraw(ICoreClientAPI api, ElementBounds bounds, DrawDelegateWithBounds draw) : base(api, bounds)
    {
        this.draw = draw;
        texture = new LoadedTexture(api);
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        Redraw();
    }

    public void Redraw()
    {
        if (disposed) return;
        using var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
        using var context = new Context(surface);
        draw(context, surface, Bounds);
        generateTexture(surface, ref texture);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (!disposed) api.Render.Render2DTexture(texture.TextureId, Bounds);
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        texture.Dispose();
        base.Dispose();
    }
}
