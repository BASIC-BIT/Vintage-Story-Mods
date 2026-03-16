using System;
using System.Collections.Generic;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Client-side renderer for environmental messages placed at arbitrary world positions
/// via raycast (!! prefix / /envhere). Maintains its own list of active bubbles, each
/// with an independent lifetime. Renders with dampened distance scaling and LOS gating.
/// </summary>
public sealed class PlacedBubbleRenderer : IRenderer
{
    private readonly ICoreClientAPI _capi;

    /// <summary>Active placed bubbles, each with its own position, texture, and expiry.</summary>
    private readonly List<PlacedBubble> _bubbles = new();

    /// <summary>LOS cache keyed by a hash of the world position (quantized to block coords).</summary>
    private readonly Dictionary<int, (bool canSee, long nextCheckMs)> _losCache = new();

    // Same dampening exponent as SpeechBubbleRenderPatches for visual consistency.
    private const double DistanceDampeningExponent = 0.6;

    // Same duration as vanilla speech bubbles (~8s base + reading time).
    private const double BaseDurationMs = 8000;
    private const double MsPerCharacter = 70;

    private const long LosPurgeIntervalMs = 10_000;
    private const long LosStaleThresholdMs = 5_000;
    private long _nextPurgeMs;

    public PlacedBubbleRenderer(ICoreClientAPI capi)
    {
        _capi = capi;
    }

    // Render just after entity speech bubbles, before GUI.
    public double RenderOrder => 0.41;
    public int RenderRange => 999999;

    /// <summary>
    /// Adds a new placed bubble to the active list.
    /// Called by the network message handler when a PlacedEnvironmentMessage arrives.
    /// </summary>
    public void AddBubble(Vec3d worldPos, string bubbleVtml)
    {
        if (worldPos == null || string.IsNullOrWhiteSpace(bubbleVtml))
        {
            return;
        }

        // The received VTML already has correct entities — no unescaping needed.
        // Protobuf transports the string as-is from the server.
        var text = bubbleVtml;

        var background = new TextBackground
        {
            FillColor = GuiStyle.DialogLightBgColor,
            Padding = 5,
            Radius = GuiStyle.ElementBGRadius,
            BorderWidth = 2,
            BorderColor = ColorUtil.Hex2Doubles("#86AEE6") // Blue env border
        };

        var baseFont = new CairoFont(25.0, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble)
        {
            Orientation = EnumTextOrientation.Left
        };

        // Strip tags once up front — used for duration calculation and as a fallback render source.
        var plainText = VtmlUtils.StripVtmlTags(text, _capi.Logger);

        var tex = RichTextTextureUtils.GenRichTextTexture(_capi, text, baseFont, maxTextWidthPx: 280, background);
        if (tex == null)
        {
            // Fallback: render the pre-stripped plain text. Use Left to match the richtext
            // path (VS has a centering bug at inline tag boundaries with Center alignment).
            tex = _capi.Gui.TextTexture.GenTextTexture(plainText, baseFont, 280, background, EnumTextOrientation.Left);
        }

        if (tex == null)
        {
            return;
        }

        var durationMs = BaseDurationMs + plainText.Length * MsPerCharacter;

        _bubbles.Add(new PlacedBubble
        {
            WorldPos = worldPos,
            Texture = tex,
            CreatedMs = _capi.World.ElapsedMilliseconds,
            DurationMs = durationMs,
        });
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho)
        {
            return;
        }

        var world = _capi.World;
        var localPlayerEntity = world?.Player?.Entity;
        if (localPlayerEntity == null)
        {
            return;
        }

        // Placed bubbles share the same VTML rendering pipeline as speech bubbles.
        // When RP chat is disabled, placed bubbles are also hidden — the chat log entry
        // for the !! message still appears, but no world-position bubble renders.
        if (!ChatUiSystem.IsSpeechBubbleVtmlEnabled())
        {
            return;
        }

        var rapi = _capi.Render;
        var nowMs = world.ElapsedMilliseconds;

        // Periodic LOS cache cleanup.
        if (nowMs >= _nextPurgeMs)
        {
            _nextPurgeMs = nowMs + LosPurgeIntervalMs;
            PurgeStaleLosEntries(nowMs);
        }

        // Iterate in reverse so we can remove expired entries without index shifting.
        for (var i = _bubbles.Count - 1; i >= 0; i--)
        {
            var bubble = _bubbles[i];
            var age = nowMs - bubble.CreatedMs;

            if (age > bubble.DurationMs)
            {
                bubble.Texture?.Dispose();
                _bubbles.RemoveAt(i);
                continue;
            }

            // LOS check to the world position.
            if (!CanSeePositionCached(world, nowMs, localPlayerEntity, bubble.WorldPos))
            {
                continue;
            }

            var tex = bubble.Texture;
            if (tex == null)
            {
                continue;
            }

            // Project world position to screen space.
            var pos = MatrixToolsd.Project(
                bubble.WorldPos,
                rapi.PerspectiveProjectionMat,
                rapi.PerspectiveViewMat,
                rapi.FrameWidth,
                rapi.FrameHeight
            );

            if (pos.Z < 0.0)
            {
                continue; // Behind the camera.
            }

            // Dampened distance scaling — same formula as SpeechBubbleRenderPatches.
            var dampenedZ = Math.Pow(Math.Max(1.0, pos.Z), DistanceDampeningExponent);
            var scale = (float)(4.0 / dampenedZ);
            var cappedScale = Math.Min(1f, scale);
            if (cappedScale > 0.75f)
            {
                cappedScale = 0.75f + (cappedScale - 0.75f) / 2f;
            }

            var posx = (float)pos.X - cappedScale * tex.Width / 2f;
            var posy = (float)rapi.FrameHeight - (float)pos.Y - cappedScale * tex.Height;

            rapi.Render2DTexture(tex.TextureId, posx, posy, cappedScale * tex.Width, cappedScale * tex.Height, 20f);
        }
    }

    /// <summary>
    /// Cached LOS check to a world position. Quantizes position to block coords for cache key.
    /// </summary>
    private bool CanSeePositionCached(IWorldAccessor world, long nowMs, Entity observer, Vec3d targetPos)
    {
        if (world == null || observer == null || targetPos == null)
        {
            return false;
        }

        // Use Math.Floor for correct block-coord mapping with negative coordinates,
        // matching the fix in RecipientDeterminationTransformer.
        var key = HashCode.Combine((int)Math.Floor(targetPos.X), (int)Math.Floor(targetPos.Y), (int)Math.Floor(targetPos.Z));

        if (!_losCache.TryGetValue(key, out var entry) || nowMs >= entry.nextCheckMs)
        {
            var canSee = VisibilityUtils.HasLineOfSight(world, observer, targetPos);
            var refreshMs = canSee ? 250L : 500L;
            entry = (canSee, nowMs + refreshMs);
            _losCache[key] = entry;
        }

        return entry.canSee;
    }

    private void PurgeStaleLosEntries(long nowMs)
    {
        List<int> toRemove = null;
        foreach (var kvp in _losCache)
        {
            if (nowMs - kvp.Value.nextCheckMs > LosStaleThresholdMs)
            {
                toRemove ??= new List<int>();
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove != null)
        {
            foreach (var key in toRemove)
            {
                _losCache.Remove(key);
            }
        }
    }

    public void Dispose()
    {
        foreach (var bubble in _bubbles)
        {
            bubble.Texture?.Dispose();
        }
        _bubbles.Clear();
        _losCache.Clear();
    }

    private class PlacedBubble
    {
        public Vec3d WorldPos;
        public LoadedTexture Texture;
        public long CreatedMs;
        public double DurationMs;
    }
}
