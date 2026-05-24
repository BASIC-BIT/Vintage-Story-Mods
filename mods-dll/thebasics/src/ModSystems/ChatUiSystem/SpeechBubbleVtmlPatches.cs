using System;
using System.Collections.Generic;
using Cairo;
using HarmonyLib;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

// Client-side patch: render VTML in vanilla overhead speech bubbles.
// Vanilla bubbles create a plain text texture, so tags like <i> show literally.
[HarmonyPatch(typeof(EntityShapeRenderer), "OnChatMessage")]
public static class SpeechBubbleVtmlPatches
{
    internal static readonly AccessTools.FieldRef<EntityShapeRenderer, List<MessageTexture>> MessageTexturesRef =
        AccessTools.FieldRefAccess<EntityShapeRenderer, List<MessageTexture>>("messageTextures");

    internal static readonly AccessTools.FieldRef<EntityShapeRenderer, LoadedTexture> DebugTagTextureRef =
        AccessTools.FieldRefAccess<EntityShapeRenderer, LoadedTexture>("debugTagTexture");

    // Match vanilla speech bubbles so normal sentences do not wrap punctuation onto orphan lines.
    private const int BubbleMaxTextWidthPx = 350;

    public static bool Prefix(EntityShapeRenderer __instance, int groupId, string message, EnumChatType chattype, string data)
    {
        // Feature flag is server-configured and delivered to client.
        if (!ChatUiSystem.IsSpeechBubbleVtmlEnabled())
        {
            return true;
        }

        long startTicks = 0;
        if (ChatUiSystem.IsDebugModeEnabled())
        {
            startTicks = PerfStats.Timestamp();
        }

        try
        {
            return HandleSpeechBubble(__instance, message, data);
        }
        catch
        {
            // Crash-safe: do not break vanilla chat bubbles.
            return true;
        }
        finally
        {
            if (startTicks != 0)
            {
                // This can be expensive when rendering VTML. Only active in DebugMode.
                PerfStats.Record(__instance?.capi, "EntityShapeRenderer.OnChatMessage (bubble)", startTicks, PerfStats.Timestamp());
            }
        }
    }

    private static bool HandleSpeechBubble(EntityShapeRenderer renderer, string message, string data)
    {
        if (!TryGetBubbleContext(renderer, message, data, out var entity, out var capi))
        {
            return true;
        }

        if (!TryExtractBubblePayload(data, entity.EntityId, out var rawMsg, out var kind, out var mode))
        {
            return true;
        }

        var bubbleVtml = VtmlUtils.UnescapeRenderableVtmlTags(rawMsg);
        if (!RequiresCustomBubbleRendering(bubbleVtml, kind, mode))
        {
            return true;
        }

        var plainForTimer = VtmlUtils.StripVtmlTags(bubbleVtml, capi.Logger);
        MessageTexturesRef(renderer).Insert(0, new MessageTexture
        {
            tex = CreateBubbleTexture(capi, bubbleVtml, kind, mode),
            message = plainForTimer,
            receivedTime = CalculateReceivedTimeForMinimumDuration(
                capi.World.ElapsedMilliseconds,
                plainForTimer.Length,
                ChatUiSystem.GetSpeechBubbleMinimumDisplayMilliseconds())
        });

        return false;
    }

    private static bool TryGetBubbleContext(EntityShapeRenderer renderer, string message, string data, out Entity entity, out ICoreClientAPI capi)
    {
        entity = renderer.entity;
        capi = renderer.capi;
        var localPlayerEntity = capi?.World?.Player?.Entity;

        return capi != null
            && entity != null
            && localPlayerEntity != null
            && HasBubblePayload(message, data)
            && IsWithinBubbleRange(entity, localPlayerEntity);
    }

    private static bool HasBubblePayload(string message, string data)
    {
        return data != null && data.Contains("from:") && message?.Length > 0;
    }

    private static bool IsWithinBubbleRange(Entity entity, EntityPlayer localPlayerEntity)
    {
        // Vanilla uses 400 sq (20 blocks). We raise to 10000 sq (100 blocks) so bubbles can
        // appear at the full yell range. The server already gates by chat range.
        return entity.Pos.SquareDistanceTo(localPlayerEntity.Pos.XYZ) < 10000.0;
    }

    private static bool TryExtractBubblePayload(string data, long entityId, out string rawMsg, out string kind, out string mode)
    {
        rawMsg = null;
        kind = null;
        mode = null;

        string[] parts = data.Split(new char[1] { ',' }, 2);
        if (parts.Length < 2)
        {
            return false;
        }

        string[] partone = parts[0].Split(new char[1] { ':' }, 2);
        string[] parttwo = parts[1].Split(new char[1] { ':' }, 2);
        if (!IsBubblePayloadForEntity(partone, parttwo, entityId))
        {
            return false;
        }

        rawMsg = parttwo[1];
        var keySegment = parttwo[0];
        kind = ExtractMarker(keySegment, "\u001fkind=");
        mode = ExtractMarker(keySegment, "\u001fmode=");
        kind ??= ExtractLegacyKind(ref rawMsg);
        return true;
    }

    private static bool IsBubblePayloadForEntity(string[] partone, string[] parttwo, long entityId)
    {
        if (partone.Length < 2 || parttwo.Length < 2 || partone[0] != "from")
        {
            return false;
        }

        if (!parttwo[0].StartsWith("msg", StringComparison.Ordinal))
        {
            return false;
        }

        int.TryParse(partone[1], out var parsedEntityId);
        return entityId == parsedEntityId;
    }

    private static string ExtractLegacyKind(ref string rawMsg)
    {
        const string kindValueMarker = "\u001fkind:";
        var valueKindIndex = rawMsg.LastIndexOf(kindValueMarker, StringComparison.Ordinal);
        if (valueKindIndex < 0)
        {
            return null;
        }

        var kind = rawMsg[(valueKindIndex + kindValueMarker.Length)..].Trim();
        rawMsg = rawMsg[..valueKindIndex];
        return kind;
    }

    private static bool RequiresCustomBubbleRendering(string bubbleVtml, string kind, string mode)
    {
        return bubbleVtml.Contains('<') || kind != null || mode != null;
    }

    private static LoadedTexture CreateBubbleTexture(ICoreClientAPI capi, string bubbleVtml, string kind, string mode)
    {
        var background = GetBubbleBackground(kind);
        var baseFont = CreateBubbleFont(mode);

        var tex = RichTextTextureUtils.GenRichTextTexture(capi, bubbleVtml, baseFont, BubbleMaxTextWidthPx, background);
        if (tex != null)
        {
            return tex;
        }

        var plain = VtmlUtils.StripVtmlTags(bubbleVtml, capi.Logger);
        return capi.Gui.TextTexture.GenTextTexture(plain, baseFont, BubbleMaxTextWidthPx, background, EnumTextOrientation.Center);
    }

    private static CairoFont CreateBubbleFont(string mode)
    {
        var fontSize = 25.0 * GetFontSizeMultiplier(mode);
        return new CairoFont(fontSize, GuiStyle.StandardFontName, GetBubbleFontColor())
        {
            // Left-align to avoid GuiElementRichtext positioning errors at inline tag boundaries.
            Orientation = EnumTextOrientation.Left
        };
    }

    private static double GetFontSizeMultiplier(string mode)
    {
        return mode switch
        {
            "yell" => 1.3,
            "whisper" => 0.75,
            _ => 1.0
        };
    }

    private static TextBackground GetBubbleBackground(string kind)
    {
        // Default (speech)
        var bg = new TextBackground
        {
            FillColor = GuiStyle.DialogLightBgColor,
            Padding = 5,
            Radius = GuiStyle.ElementBGRadius
        };

        // Keep a consistent vanilla-like look; differentiate kinds via a subtle border.
        if (kind == "env")
        {
            bg.BorderWidth = 2;
            bg.BorderColor = ColorUtil.Hex2Doubles("#86AEE6");
        }
        else if (kind == "emote")
        {
            bg.BorderWidth = 2;
            bg.BorderColor = ColorUtil.Hex2Doubles("#E6C686");
        }
        else if (kind == "ooc")
        {
            bg.BorderWidth = 2;
            bg.BorderColor = ColorUtil.Hex2Doubles("#B6D9A2");
        }

        return bg;
    }

    private static double[] GetBubbleFontColor()
    {
        // Keep vanilla's white text; background stays consistent across kinds.
        return ColorUtil.WhiteArgbDouble;
    }

    internal static long CalculateReceivedTimeForMinimumDuration(long nowMs, int messageLength, int minimumDurationMs)
    {
        var vanillaDurationMs = 3500 + 100 * (messageLength - 10);
        var effectiveDurationMs = Math.Max(vanillaDurationMs, Math.Max(0, minimumDurationMs));
        return nowMs - vanillaDurationMs + effectiveDurationMs;
    }

    /// <summary>
    /// Extracts a marker value from a key segment string.
    /// Markers are encoded as <c>\u001fkey=value</c> and may be followed by another marker.
    /// </summary>
    private static string ExtractMarker(string keySegment, string markerPrefix)
    {
        var idx = keySegment.IndexOf(markerPrefix, StringComparison.Ordinal);
        if (idx < 0) return null;

        var valueStart = idx + markerPrefix.Length;
        // Value ends at the next unit separator or end of string.
        var nextSep = keySegment.IndexOf('\u001f', valueStart);
        var value = nextSep >= 0
            ? keySegment[valueStart..nextSep]
            : keySegment[valueStart..];
        return value.Trim();
    }

    // NOTE: richtext texture rendering lives in RichTextTextureUtils.
}

/// <summary>
/// Per-frame rendering patch for overhead speech bubbles.
/// Takes over vanilla's bubble rendering to provide:
/// <list type="bullet">
///   <item>Multi-point LOS gating — hides bubbles when the local player can't see the entity</item>
///   <item>Dampened distance scaling — bubbles shrink more subtly with distance, staying
///         legible even at long range instead of vanilla's linear 1/distance falloff</item>
/// </list>
/// When the patch is active, vanilla's own bubble loop is skipped (textures hidden via
/// the Prefix/Postfix stash pattern) and we render them ourselves.
/// </summary>
[HarmonyPatch(typeof(EntityShapeRenderer), "DoRender2D")]
public static class SpeechBubbleRenderPatches
{
    // Dampening exponent for distance scaling. Vanilla uses 1.0 (linear: 4/Z).
    // Values < 1.0 make shrinkage more gradual (e.g., 0.6 → 4/Z^0.6).
    // At exponent 0.6:  Z=10 → scale≈1.0,  Z=20 → 0.63,  Z=35 → 0.46
    // Vanilla linear:   Z=10 → 0.40,        Z=20 → 0.20,  Z=35 → 0.11
    private const double DistanceDampeningExponent = 0.6;

    // Cached LOS per entity. Keyed by target entity id.
    // Asymmetric refresh: 250ms when visible, 500ms when hidden for smooth reveal without expensive raytracing every frame.
    private static readonly Dictionary<long, (bool canSee, long nextCheckMs)> _losCache = new();

    // Shared empty list to avoid per-frame allocations when hiding bubbles from vanilla.
    private static readonly List<MessageTexture> EmptyList = new();

    /// <summary>
    /// Before vanilla renders bubbles: if the feature is active, render them ourselves
    /// with dampened distance scaling and LOS gating, then hide them from vanilla.
    /// </summary>
    public static void Prefix(EntityShapeRenderer __instance, float dt, ref List<MessageTexture> __state)
    {
        __state = null;

        if (!ChatUiSystem.IsSpeechBubbleVtmlEnabled())
        {
            return;
        }

        try
        {
            RenderBubblesAndHideVanilla(__instance, ref __state);
        }
        catch
        {
            // Crash-safe: if anything fails, let vanilla handle rendering normally.
        }
    }

    private static void RenderBubblesAndHideVanilla(EntityShapeRenderer renderer, ref List<MessageTexture> state)
    {
        if (!TryGetRenderContext(renderer, out var capi, out var entity, out var localPlayerEntity, out var textures))
        {
            return;
        }

        if (!CanRenderForLineOfSight(capi, localPlayerEntity, entity))
        {
            HideVanillaBubbles(renderer, textures, ref state);
            return;
        }

        var rapi = capi.Render;
        var pos = ProjectAboveHeadPosition(renderer, localPlayerEntity, rapi);
        if (pos.Z < 0.0)
        {
            HideVanillaBubbles(renderer, textures, ref state);
            return;
        }

        RenderMessageTextures(renderer, rapi, textures, pos, GetBubbleScale(pos.Z));
        HideVanillaBubbles(renderer, textures, ref state);
    }

    private static bool TryGetRenderContext(
        EntityShapeRenderer renderer,
        out ICoreClientAPI capi,
        out Entity entity,
        out EntityPlayer localPlayerEntity,
        out List<MessageTexture> textures)
    {
        entity = renderer.entity;
        capi = renderer.capi;
        localPlayerEntity = capi?.World?.Player?.Entity;
        textures = SpeechBubbleVtmlPatches.MessageTexturesRef(renderer);

        return capi != null && entity != null && localPlayerEntity != null && textures is { Count: > 0 };
    }

    private static bool CanRenderForLineOfSight(ICoreClientAPI capi, EntityPlayer localPlayerEntity, Entity entity)
    {
        if (localPlayerEntity.EntityId == entity.EntityId)
        {
            return true;
        }

        return CanSeeCached(capi.World, capi.World.ElapsedMilliseconds, localPlayerEntity, entity);
    }

    private static Vec3d ProjectAboveHeadPosition(EntityShapeRenderer renderer, EntityPlayer localPlayerEntity, IRenderAPI rapi)
    {
        var aboveHeadPos = renderer.getAboveHeadPosition(localPlayerEntity);
        return MatrixToolsd.Project(aboveHeadPos, rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.FrameWidth, rapi.FrameHeight);
    }

    private static float GetBubbleScale(double z)
    {
        var dampenedZ = Math.Pow(Math.Max(1.0, z), DistanceDampeningExponent);
        var cappedScale = Math.Min(1f, (float)(4.0 / dampenedZ));
        return cappedScale > 0.75f
            ? 0.75f + (cappedScale - 0.75f) / 2f
            : cappedScale;
    }

    private static void RenderMessageTextures(EntityShapeRenderer renderer, IRenderAPI rapi, List<MessageTexture> textures, Vec3d pos, float cappedScale)
    {
        var offY = ((SpeechBubbleVtmlPatches.DebugTagTextureRef(renderer)?.Height ?? 0) + 8f) * cappedScale;
        for (var i = 0; i < textures.Count; i++)
        {
            var mt = textures[i];
            offY += mt.tex.Height * cappedScale + 4f * cappedScale;
            var posx = (float)pos.X - cappedScale * mt.tex.Width / 2f;
            var posy = (float)rapi.FrameHeight - ((float)pos.Y + offY);

            rapi.Render2DTexture(mt.tex.TextureId, posx, posy,
                cappedScale * mt.tex.Width, cappedScale * mt.tex.Height, 20f);
        }
    }

    private static void HideVanillaBubbles(EntityShapeRenderer renderer, List<MessageTexture> textures, ref List<MessageTexture> state)
    {
        state = textures;
        SpeechBubbleVtmlPatches.MessageTexturesRef(renderer) = EmptyList;
    }

    /// <summary>
    /// Restore the original messageTextures list if we stashed it.
    /// </summary>
    public static void Postfix(EntityShapeRenderer __instance, List<MessageTexture> __state)
    {
        if (__state != null)
        {
            try
            {
                SpeechBubbleVtmlPatches.MessageTexturesRef(__instance) = __state;
                if (EmptyList.Count > 0)
                {
                    EmptyList.Clear();
                }
            }
            catch
            {
                // Crash-safe.
            }
        }
    }

    // Purge stale LOS cache entries every ~10 seconds to prevent unbounded growth
    // when entities despawn or disconnect during long play sessions.
    private const long PurgeIntervalMs = 10_000;
    private const long StaleThresholdMs = 5_000;
    private static long _nextPurgeMs;

    private static bool CanSeeCached(IWorldAccessor world, long nowMs, Entity observer, Entity target)
    {
        if (world == null || observer == null || target == null)
        {
            return false;
        }

        // Periodic cache cleanup.
        if (nowMs >= _nextPurgeMs)
        {
            _nextPurgeMs = nowMs + PurgeIntervalMs;
            PurgeStaleEntries(nowMs);
        }

        if (!_losCache.TryGetValue(target.EntityId, out var entry) || nowMs >= entry.nextCheckMs)
        {
            var canSee = VisibilityUtils.HasLineOfSight(world, observer, target, failOpen: false, useMultiPointTargets: true);
            var refreshMs = canSee ? 250L : 500L;
            entry = (canSee, nowMs + refreshMs);
            _losCache[target.EntityId] = entry;
        }

        return entry.canSee;
    }

    private static void PurgeStaleEntries(long nowMs)
    {
        List<long> toRemove = null;
        foreach (var kvp in _losCache)
        {
            if (nowMs - kvp.Value.nextCheckMs > StaleThresholdMs)
            {
                toRemove ??= new List<long>();
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
}
