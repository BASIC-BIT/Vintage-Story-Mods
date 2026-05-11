using Cairo;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Renders a headshot inside a fixed square in the bio dialog. The owning dialog assigns the
/// texture from the client cache; this element just paints a placeholder when none is set,
/// and the texture when present.
/// </summary>
public sealed class HeadshotElement : GuiElement
{
    private LoadedTexture _texture;
    private string _statusText;
    private LoadedTexture _statusTextTexture;

    public HeadshotElement(ICoreClientAPI capi, ElementBounds bounds)
        : base(capi, bounds)
    {
        _statusTextTexture = new LoadedTexture(capi);
    }

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();

        // Slot background with a soft inset, matching the visual language of other VS dialogs.
        ctxStatic.SetSourceRGBA(0.0, 0.0, 0.0, 0.35);
        RoundRectangle(ctxStatic, Bounds.drawX, Bounds.drawY, Bounds.InnerWidth, Bounds.InnerHeight, 2.0);
        ctxStatic.Fill();
        EmbossRoundRectangleElement(ctxStatic, Bounds, inverse: true, 1, 2);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (_texture != null && _texture.TextureId != 0)
        {
            api.Render.Render2DTexture(
                _texture.TextureId,
                (float)Bounds.renderX,
                (float)Bounds.renderY,
                (float)Bounds.OuterWidth,
                (float)Bounds.OuterHeight);
            return;
        }

        if (_statusTextTexture != null && _statusTextTexture.TextureId != 0)
        {
            var x = (float)(Bounds.renderX + (Bounds.OuterWidth - _statusTextTexture.Width) / 2.0);
            var y = (float)(Bounds.renderY + (Bounds.OuterHeight - _statusTextTexture.Height) / 2.0);
            api.Render.Render2DLoadedTexture(_statusTextTexture, x, y);
        }
    }

    public void SetTexture(LoadedTexture texture)
    {
        // Texture is cache-owned, not element-owned — see Dispose for the reciprocal "do not free" rule.
        _texture = texture;
        _statusText = null;
        // Free the placeholder text GPU texture now that a real headshot is shown; rebuilt on demand.
        _statusTextTexture?.Dispose();
        _statusTextTexture = null;
    }

    public void ClearTexture()
    {
        _texture = null;
    }

    public void SetStatusText(string text)
    {
        if (_statusText == text)
        {
            return;
        }

        _statusText = text;
        if (string.IsNullOrEmpty(text))
        {
            _statusTextTexture?.Dispose();
            _statusTextTexture = null;
            return;
        }

        _statusTextTexture ??= new LoadedTexture(api);
        var font = CairoFont.WhiteSmallText().WithFontSize(13f);
        // Default-constructed TextBackground has FillColor = new double[4] (transparent).
        // Setting FillColor = null causes an NRE inside Cairo.Context.SetSourceRGBA.
        api.Gui.TextTexture.GenOrUpdateTextTexture(text, font, ref _statusTextTexture, new TextBackground());
    }

    public override void Dispose()
    {
        base.Dispose();
        _statusTextTexture?.Dispose();
        _statusTextTexture = null;
        // _texture is owned by HeadshotClientCache; that cache disposes it on eviction.
    }
}
