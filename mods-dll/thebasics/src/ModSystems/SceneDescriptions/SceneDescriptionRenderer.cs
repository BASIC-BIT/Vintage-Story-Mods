using System;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneDescriptionRenderer : IRenderer
{
    private readonly ICoreClientAPI _capi;
    private LoadedTexture _texture;
    private string _textureKey = string.Empty;

    public SceneDescriptionRenderer(ICoreClientAPI capi)
    {
        _capi = capi;
    }

    public double RenderOrder => 0.415;

    public int RenderRange => 32;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho)
        {
            return;
        }

        if (!TryGetSelectedDescription(out var position, out var data))
        {
            ClearTexture();
            return;
        }

        EnsureTexture(data);
        if (_texture == null)
        {
            return;
        }

        var rapi = _capi.Render;
        var worldPos = position.ToVec3d().Add(0.5, 0.72, 0.5);
        var projected = MatrixToolsd.Project(worldPos, rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.FrameWidth, rapi.FrameHeight);
        if (projected.Z < 0)
        {
            return;
        }

        var scale = GetScale(projected.Z);
        var x = (float)projected.X - scale * _texture.Width / 2f;
        var y = (float)rapi.FrameHeight - (float)projected.Y - scale * _texture.Height - 18f;
        rapi.Render2DTexture(_texture.TextureId, x, y, scale * _texture.Width, scale * _texture.Height, 20f);
    }

    public void Dispose()
    {
        ClearTexture();
    }

    private bool TryGetSelectedDescription(out BlockPos position, out SceneDescriptionData data)
    {
        position = _capi.World?.Player?.CurrentBlockSelection?.Position;
        var blockEntity = position == null ? null : _capi.World.BlockAccessor.GetBlockEntity(position) as SceneDescriptionBlockEntity;
        data = blockEntity?.Data;
        return position != null && !string.IsNullOrWhiteSpace(data?.Body);
    }

    private void EnsureTexture(SceneDescriptionData data)
    {
        var key = $"{(int)data.Kind}\n{data.Title}\n{data.Body}";
        if (_texture != null && string.Equals(_textureKey, key, StringComparison.Ordinal))
        {
            return;
        }

        ClearTexture();
        _textureKey = key;
        var border = data.Kind == SceneDescriptionKind.OocNotice ? "#A9A1B8" : "#86AEE6";
        var background = new TextBackground
        {
            FillColor = GuiStyle.DialogLightBgColor,
            Padding = 7,
            Radius = GuiStyle.ElementBGRadius,
            BorderWidth = 2,
            BorderColor = ColorUtil.Hex2Doubles(border),
        };
        var font = new CairoFont(24, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble)
        {
            Orientation = EnumTextOrientation.Left,
        };

        _texture = RichTextTextureUtils.GenRichTextTexture(_capi, SceneDescriptionFormatter.ToVtml(data), font, 360, background);
    }

    private void ClearTexture()
    {
        _texture?.Dispose();
        _texture = null;
        _textureKey = string.Empty;
    }

    private static float GetScale(double distance)
    {
        var dampened = Math.Pow(Math.Max(1, distance), 0.6);
        return Math.Min(1f, (float)(4 / dampened));
    }
}
