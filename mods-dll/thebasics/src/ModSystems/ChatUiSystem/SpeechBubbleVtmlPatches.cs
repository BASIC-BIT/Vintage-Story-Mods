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

    private const int BubbleMaxTextWidthPx = 280;

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
            Entity entity = __instance.entity;
            ICoreClientAPI capi = __instance.capi;

            if (capi == null || entity == null)
            {
                return true;
            }

            var localPlayerEntity = capi.World?.Player?.Entity;
            if (localPlayerEntity == null)
            {
                return true;
            }

            // Vanilla uses 400 sq (20 blocks). We raise to 10000 sq (100 blocks) so bubbles
            // can appear at the full yell range (90 blocks). The server already gates by chat
            // range, so this is just a safety cap.
            if (data == null || !data.Contains("from:") || entity.Pos.SquareDistanceTo(localPlayerEntity.Pos.XYZ) >= 10000.0 || message.Length <= 0)
            {
                return true;
            }

            string[] parts = data.Split(new char[1] { ',' }, 2);
            if (parts.Length < 2)
            {
                return true;
            }

            string[] partone = parts[0].Split(new char[1] { ':' }, 2);
            string[] parttwo = parts[1].Split(new char[1] { ':' }, 2);
            if (partone.Length < 2 || parttwo.Length < 2)
            {
                return true;
            }

            if (partone[0] != "from")
            {
                return true;
            }

            // Validate the second segment is a message payload (starts with "msg").
            // Other payload types (if any) should not be treated as bubble text.
            if (!parttwo[0].StartsWith("msg", StringComparison.Ordinal))
            {
                return true;
            }

            int.TryParse(partone[1], out var entityid);
            if (entity.EntityId != entityid)
            {
                return true;
            }

            // Bubble text comes from the data payload.
            // The server encodes markers in the key segment (before ':') using unit separator:
            //   from:<id>,msg\u001fkind=<emote|env|ooc>\u001fmode=<yell|whisper>:<text>
            // Legacy suffix format in the value is also supported for kind.
            var rawMsg = parttwo[1];
            string kind = null;
            string mode = null;

            // Parse markers from the key segment (preferred format).
            var keySegment = parttwo[0];
            kind = ExtractMarker(keySegment, "\u001fkind=");
            mode = ExtractMarker(keySegment, "\u001fmode=");

            // Legacy suffix format for kind (kept for safety).
            if (kind == null)
            {
                const string kindValueMarker = "\u001fkind:";
                var valueKindIndex = rawMsg.LastIndexOf(kindValueMarker, StringComparison.Ordinal);
                if (valueKindIndex >= 0)
                {
                    kind = rawMsg[(valueKindIndex + kindValueMarker.Length)..].Trim();
                    rawMsg = rawMsg[..valueKindIndex];
                }
            }

            var bubbleVtml = rawMsg;
            bubbleVtml = bubbleVtml.Replace("&lt;", "<").Replace("&gt;", ">");

            var hasVtml = bubbleVtml.Contains('<');
            // If there are no tags, no kind, and no mode marker, vanilla rendering is fine.
            if (!hasVtml && kind == null && mode == null)
            {
                return true;
            }

            var background = GetBubbleBackground(kind);

            var fontColor = GetBubbleFontColor(kind);

            // Scale bubble font size by chat mode: yell is larger, whisper is smaller.
            var baseFontSize = 25.0;
            var fontSizeMultiplier = mode switch
            {
                "yell" => 1.3,
                "whisper" => 0.75,
                _ => 1.0
            };
            var fontSize = baseFontSize * fontSizeMultiplier;

            var baseFont = new CairoFont(fontSize, GuiStyle.StandardFontName, fontColor)
            {
                // Left-align to avoid GuiElementRichtext positioning errors at inline
                // tag boundaries (bold/color transitions) that occur with Center alignment.
                Orientation = EnumTextOrientation.Left
            };

            // Always attempt richtext rendering here (even for plain text) so we can apply
            // consistent sizing. Nametag spacing is handled at render time by
            // SpeechBubbleRenderPatches (no transparent margin baked into the texture).
            var tex = RichTextTextureUtils.GenRichTextTexture(capi, bubbleVtml, baseFont, BubbleMaxTextWidthPx, background);
            if (tex == null)
            {
                // Fallback: strip tags and let vanilla-esque plain rendering handle it.
                var plain = VtmlUtils.StripVtmlTags(bubbleVtml, capi.Logger);
                tex = capi.Gui.TextTexture.GenTextTexture(plain, baseFont, BubbleMaxTextWidthPx, background, EnumTextOrientation.Center);
            }

            var plainForTimer = VtmlUtils.StripVtmlTags(bubbleVtml, capi.Logger);

            var list = MessageTexturesRef(__instance);
            list.Insert(0, new MessageTexture
            {
                tex = tex,
                message = plainForTimer,
                receivedTime = capi.World.ElapsedMilliseconds
            });

            // We handled it.
            return false;
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

    private static double[] GetBubbleFontColor(string kind)
    {
        // Keep vanilla's white text; background stays consistent across kinds.
        return ColorUtil.WhiteArgbDouble;
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
///   <item>LOS gating — hides bubbles when the local player can't see the entity</item>
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
            var entity = __instance.entity;
            var capi = __instance.capi;
            if (capi == null || entity == null)
            {
                return;
            }

            var localPlayerEntity = capi.World?.Player?.Entity;
            if (localPlayerEntity == null)
            {
                return;
            }

            var textures = SpeechBubbleVtmlPatches.MessageTexturesRef(__instance);
            if (textures == null || textures.Count == 0)
            {
                return; // Nothing to render or hide.
            }

            // Skip LOS check for own entity — always see your own bubbles.
            // (Distance scaling still applies for third-person cameras, but Z is usually tiny.)
            var isOwnEntity = localPlayerEntity.EntityId == entity.EntityId;

            // LOS check.
            if (!isOwnEntity)
            {
                var nowMs = capi.World.ElapsedMilliseconds;
                if (!CanSeeCached(capi.World, nowMs, localPlayerEntity, entity))
                {
                    // Hide from vanilla, don't render ourselves.
                    __state = textures;
                    SpeechBubbleVtmlPatches.MessageTexturesRef(__instance) = EmptyList;
                    return;
                }
            }

            // Render bubbles ourselves with dampened distance scaling.
            var rapi = capi.Render;
            var aboveHeadPos = __instance.getAboveHeadPosition(localPlayerEntity);
            var pos = MatrixToolsd.Project(aboveHeadPos, rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.FrameWidth, rapi.FrameHeight);
            if (pos.Z < 0.0)
            {
                // Behind the camera — hide from vanilla but don't render.
                __state = textures;
                SpeechBubbleVtmlPatches.MessageTexturesRef(__instance) = EmptyList;
                return;
            }

            // Dampened distance scaling: 4 / Z^exponent instead of vanilla's 4 / Z.
            // This makes bubbles shrink much more gradually with distance.
            var dampenedZ = Math.Pow(Math.Max(1.0, pos.Z), DistanceDampeningExponent);
            var scale = (float)(4.0 / dampenedZ);
            var cappedScale = Math.Min(1f, scale);
            if (cappedScale > 0.75f)
            {
                cappedScale = 0.75f + (cappedScale - 0.75f) / 2f;
            }

            // Stack bubbles upward from the projected position (replicates vanilla stacking).
            // Start with a small base offset (in screen-space pixels) to separate the
            // first bubble from the nametag. This replaces the old transparent-margin
            // approach that baked spacing into the texture (which caused a dark bar artifact).
            var offY = 10f * cappedScale;
            for (var i = 0; i < textures.Count; i++)
            {
                var mt = textures[i];
                offY += mt.tex.Height * cappedScale + 4f * cappedScale;
                var posx = (float)pos.X - cappedScale * mt.tex.Width / 2f;
                var posy = (float)rapi.FrameHeight - ((float)pos.Y + offY);

                rapi.Render2DTexture(mt.tex.TextureId, posx, posy,
                    cappedScale * mt.tex.Width, cappedScale * mt.tex.Height, 20f);
            }

            // Hide textures from vanilla so it doesn't render them again.
            __state = textures;
            SpeechBubbleVtmlPatches.MessageTexturesRef(__instance) = EmptyList;
        }
        catch
        {
            // Crash-safe: if anything fails, let vanilla handle rendering normally.
        }
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
            var canSee = VisibilityUtils.HasLineOfSight(world, observer, target);
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
