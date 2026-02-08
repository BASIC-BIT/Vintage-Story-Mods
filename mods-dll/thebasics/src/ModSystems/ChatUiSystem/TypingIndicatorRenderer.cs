using System;
using System.Collections.Generic;
using thebasics.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class TypingIndicatorRenderer : IRenderer
{
    private readonly ICoreClientAPI _capi;

    // Only raytrace a given target a few times per second.
    // Keyed by target entity id.
    private readonly Dictionary<long, (bool canSee, long nextCheckMs)> _losCache = new();

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

        // Texture is now created per-state (Typing/Composing/Chat open), so don't pre-generate here.

        var range = ChatUiSystem.GetTypingIndicatorRange();
        if (range <= 0)
        {
            return;
        }

        var rapi = _capi.Render;
        var maxDistSq = (double)range * range;
        var nowMs = world.ElapsedMilliseconds;

        // Only iterate known players; this feature is cosmetic.
        foreach (var plr in world.AllPlayers)
        {
            var entity = plr?.Entity;
            if (entity == null || entity.EntityId == localPlayerEntity.EntityId)
            {
                continue;
            }

            var state = ChatUiSystem.GetEntityTypingIndicatorState(entity.EntityId);
            if (state == ChatTypingIndicatorState.None)
            {
                continue;
            }

            var distSq = localPlayerEntity.Pos.SquareDistanceTo(entity.Pos);
            if (distSq > maxDistSq)
            {
                continue;
            }

            if (!CanSeeCached(world, nowMs, localPlayerEntity, entity))
            {
                continue;
            }

            // Use the game's own above-head logic for correct mount offsets.
            if (entity.Properties?.Client?.Renderer is not EntityShapeRenderer esr)
            {
                continue;
            }

            // Lift above the normal nametag position to avoid overlap.
            // Use the target entity here (not local player), otherwise the projection can drift into the nametag.
            var (label, yWorldOffset) = GetIndicatorLabelAndOffset(state, text);
            EnsureTexture(label);
            if (_textTexture == null)
            {
                continue;
            }

            var aboveHeadPos = esr.getAboveHeadPosition(entity).Add(0, yWorldOffset, 0);
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

    private bool CanSeeCached(IWorldAccessor world, long nowMs, Entity observer, Entity target)
    {
        if (world == null || observer == null || target == null)
        {
            return false;
        }

        // If the cache is stale or missing, recompute.
        if (!_losCache.TryGetValue(target.EntityId, out var entry) || nowMs >= entry.nextCheckMs)
        {
            var canSee = VisibilityUtils.HasLineOfSight(world, observer, target);
            // Faster refresh when visible for nicer responsiveness.
            var refreshMs = canSee ? 250L : 500L;
            entry = (canSee, nowMs + refreshMs);
            _losCache[target.EntityId] = entry;
        }

        return entry.canSee;
    }

    private static (string label, double yWorldOffset) GetIndicatorLabelAndOffset(ChatTypingIndicatorState state, string typingLabel)
    {
        // Keep these short. The goal is just to communicate intent at a glance.
        return state switch
        {
            ChatTypingIndicatorState.Typing => (typingLabel, 0.25),
            ChatTypingIndicatorState.ChatOpenComposing => (Lang.Get("thebasics:typingindicator-composing"), 0.20),
            ChatTypingIndicatorState.ChatOpenEmpty => (Lang.Get("thebasics:typingindicator-chatopen"), 0.15),
            _ => (typingLabel, 0.25)
        };
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
        _losCache.Clear();
    }
}
