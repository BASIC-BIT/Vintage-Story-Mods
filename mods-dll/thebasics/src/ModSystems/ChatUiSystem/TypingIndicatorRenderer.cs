using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class TypingIndicatorRenderer : IRenderer
{
    private readonly ICoreClientAPI _capi;

    private LoadedTexture _textTexture;
    private string _lastText;

    public TypingIndicatorRenderer(ICoreClientAPI capi)
    {
        _capi = capi;
    }

    // Render just after entities/nametags; must stay below GUI manager.
    public double RenderOrder => 0.405;

    // Not currently used by VS, but required by interface.
    public int RenderRange => 999999;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho)
        {
            return;
        }

        if (!ChatUiSystem.IsTypingIndicatorEnabled())
        {
            return;
        }

        var world = _capi.World;
        var localPlayerEntity = world?.Player?.Entity;
        if (localPlayerEntity == null)
        {
            return;
        }

        var text = ChatUiSystem.GetTypingIndicatorText();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "Typing...";
        }

        EnsureTexture(text);
        if (_textTexture == null)
        {
            return;
        }

        var range = ChatUiSystem.GetTypingIndicatorRange();
        if (range <= 0)
        {
            return;
        }

        var rapi = _capi.Render;
        var maxDistSq = (double)range * range;

        // Only iterate known players; this feature is cosmetic.
        foreach (var plr in world.AllPlayers)
        {
            var entity = plr?.Entity;
            if (entity == null || entity.EntityId == localPlayerEntity.EntityId)
            {
                continue;
            }

            if (!ChatUiSystem.IsEntityTyping(entity.EntityId))
            {
                continue;
            }

            var distSq = localPlayerEntity.Pos.SquareDistanceTo(entity.Pos);
            if (distSq > maxDistSq)
            {
                continue;
            }

            // Use the game's own above-head logic for correct mount offsets.
            if (entity.Properties?.Client?.Renderer is not EntityShapeRenderer esr)
            {
                continue;
            }

            // Lift slightly above the normal nametag position to avoid overlap.
            // Use the target entity here (not local player), otherwise the projection can drift into the nametag.
            var aboveHeadPos = esr.getAboveHeadPosition(entity).Add(0, 0.25, 0);
            Vec3d pos = MatrixToolsd.Project(aboveHeadPos, rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.FrameWidth, rapi.FrameHeight);
            if (pos.Z < 0.0)
            {
                continue;
            }

            float scale = 4f / Math.Max(1f, (float)pos.Z);
            float cappedScale = Math.Min(1f, scale);
            if (cappedScale > 0.75f)
            {
                cappedScale = 0.75f + (cappedScale - 0.75f) / 2f;
            }

            float posx = (float)pos.X - cappedScale * _textTexture.Width / 2f;

            // Small screen-space nudge; most separation comes from the world-space offset above.
            float yOffset = 2f;
            float posy = (float)rapi.FrameHeight - (float)pos.Y - cappedScale * _textTexture.Height - yOffset;

            rapi.Render2DTexture(_textTexture.TextureId, posx, posy, cappedScale * _textTexture.Width, cappedScale * _textTexture.Height, 20f);
        }
    }

    private void EnsureTexture(string text)
    {
        if (_textTexture != null && text == _lastText)
        {
            return;
        }

        _lastText = text;
        _textTexture?.Dispose();

        var bg = new TextBackground
        {
            Padding = 3,
            Radius = GuiStyle.ElementBGRadius,
            FillColor = new[]
            {
                GuiStyle.DialogLightBgColor[0],
                GuiStyle.DialogLightBgColor[1],
                GuiStyle.DialogLightBgColor[2],
                0.85
            },
            BorderWidth = 0
        };

        var font = CairoFont.WhiteSmallText().WithFontSize(14f);
        _textTexture = _capi.Gui.TextTexture.GenUnscaledTextTexture(text, font, bg);
    }

    public void Dispose()
    {
        _textTexture?.Dispose();
        _textTexture = null;
    }
}
