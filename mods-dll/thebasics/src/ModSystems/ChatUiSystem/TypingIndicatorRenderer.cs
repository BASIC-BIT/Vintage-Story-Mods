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

    // Cache textures per (displayMode, state, label) to avoid thrashing.
    private readonly Dictionary<string, LoadedTexture> _textTextures = new();

    // Track the last display mode so we can flush textures on change.
    private TypingIndicatorDisplayMode _lastDisplayMode;

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

        var displayMode = ChatUiSystem.GetTypingIndicatorDisplayMode();

        // Flush cached textures when the display mode changes.
        if (displayMode != _lastDisplayMode)
        {
            FlushTextureCache();
            _lastDisplayMode = displayMode;
        }

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

            var label = GetLabelForState(state, displayMode);
            var tex = GetOrCreateTexture(label, state, displayMode);
            if (tex == null)
            {
                continue;
            }

            // Consistent world-space offset for all states — avoids vertical jumping when state changes.
            const double yWorldOffset = 0.25;
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

            float posx = (float)pos.X - cappedScale * tex.Width / 2f;

            // Small screen-space nudge; most separation comes from the world-space offset above.
            float yOffset = 2f;
            float posy = (float)rapi.FrameHeight - (float)pos.Y - cappedScale * tex.Height - yOffset;

            rapi.Render2DTexture(tex.TextureId, posx, posy, cappedScale * tex.Width, cappedScale * tex.Height, 20f);
        }
    }

    /// <summary>
    /// Builds the display string for the given state and display mode.
    /// </summary>
    private static string GetLabelForState(ChatTypingIndicatorState state, TypingIndicatorDisplayMode displayMode)
    {
        // Resolve text and icon lang keys per state.
        var (iconKey, textKey) = state switch
        {
            ChatTypingIndicatorState.Typing => ("thebasics:typingindicator-typing-icon", "thebasics:typingindicator-typing-text"),
            ChatTypingIndicatorState.ChatOpenComposing => ("thebasics:typingindicator-composing-icon", "thebasics:typingindicator-composing-text"),
            ChatTypingIndicatorState.ChatOpenEmpty => ("thebasics:typingindicator-chatopen-icon", "thebasics:typingindicator-chatopen-text"),
            _ => ("thebasics:typingindicator-typing-icon", "thebasics:typingindicator-typing-text"),
        };

        // For "Typing" state, respect the server-side text override if configured.
        string textLabel;
        if (state == ChatTypingIndicatorState.Typing)
        {
            textLabel = ChatUiSystem.GetTypingIndicatorText();
        }
        else
        {
            textLabel = Lang.Get(textKey);
        }

        // ChatOpenEmpty is always text-only ("...") — no icon regardless of mode.
        if (state == ChatTypingIndicatorState.ChatOpenEmpty)
        {
            return textLabel;
        }

        return displayMode switch
        {
            TypingIndicatorDisplayMode.Text => textLabel,
            TypingIndicatorDisplayMode.Both => $"{Lang.Get(iconKey)} {textLabel}",
            _ => Lang.Get(iconKey), // Icon (default)
        };
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

    private LoadedTexture GetOrCreateTexture(string text, ChatTypingIndicatorState state, TypingIndicatorDisplayMode displayMode)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Cache includes the state and display mode.
        var cacheKey = $"{(byte)displayMode}:{(byte)state}:{text}";
        if (_textTextures.TryGetValue(cacheKey, out var existing) && existing != null)
        {
            return existing;
        }

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
            BorderWidth = state == ChatTypingIndicatorState.ChatOpenEmpty ? 0 : 2,
            BorderColor = state switch
            {
                ChatTypingIndicatorState.Typing => ColorUtil.Hex2Doubles("#E6C686"),
                ChatTypingIndicatorState.ChatOpenComposing => ColorUtil.Hex2Doubles("#A0AAB8"),
                _ => ColorUtil.Hex2Doubles("#AAAAAA")
            }
        };

        // Use a slightly larger font so icons are clearly visible at distance.
        var font = CairoFont.WhiteSmallText().WithFontSize(18f);
        font.Orientation = EnumTextOrientation.Center;

        LoadedTexture tex;
        var hasVtml = text.Contains('<');
        if (hasVtml)
        {
            // Supports <icon> and other VTML tags.
            tex = RichTextTextureUtils.GenRichTextTexture(_capi, text, font, maxTextWidthPx: 250, bg);
            if (tex == null)
            {
                var plain = VtmlUtils.StripVtmlTags(text, _capi.Logger);
                tex = _capi.Gui.TextTexture.GenUnscaledTextTexture(plain, font, bg);
            }
        }
        else
        {
            tex = _capi.Gui.TextTexture.GenUnscaledTextTexture(text, font, bg);
        }
        if (tex != null)
        {
            _textTextures[cacheKey] = tex;
        }

        return tex;
    }

    private void FlushTextureCache()
    {
        foreach (var kvp in _textTextures)
        {
            kvp.Value?.Dispose();
        }
        _textTextures.Clear();
    }

    public void Dispose()
    {
        FlushTextureCache();
        _losCache.Clear();
    }
}
