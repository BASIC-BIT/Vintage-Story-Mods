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

    private const int BubbleMaxTextWidthPx = 350;
    private const int BubbleBottomMarginPx = 40;

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

            if (data == null || !data.Contains("from:") || entity.Pos.SquareDistanceTo(localPlayerEntity.Pos.XYZ) >= 400.0 || message.Length <= 0)
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
            // When this patch is enabled, the server may attach an optional kind marker for styling:
            //   New (preferred): from:<id>,msg\u001fkind=<emote|env|ooc>:<text>
            //   Legacy:          from:<id>,msg:<text>\u001fkind:<emote|env|ooc>
            var rawMsg = parttwo[1];
            string kind = null;

            // Preferred format: kind marker embedded in the key segment so vanilla clients never display it.
            const string kindKeyMarker = "\u001fkind=";
            var keyKindIndex = parttwo[0].LastIndexOf(kindKeyMarker, StringComparison.Ordinal);
            if (keyKindIndex >= 0)
            {
                kind = parttwo[0][(keyKindIndex + kindKeyMarker.Length)..].Trim();
            }
            else
            {
                // Legacy suffix format (kept for safety).
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
            // If there are no tags and no kind marker, vanilla rendering is fine.
            if (!hasVtml && kind == null)
            {
                return true;
            }

            var background = GetBubbleBackground(kind);

            var fontColor = GetBubbleFontColor(kind);

            var baseFont = new CairoFont(25.0, GuiStyle.StandardFontName, fontColor)
            {
                // Left-align to avoid GuiElementRichtext positioning errors at inline
                // tag boundaries (bold/color transitions) that occur with Center alignment.
                Orientation = EnumTextOrientation.Left
            };

            // Always attempt richtext rendering here (even for plain text) so we can apply
            // consistent sizing and add a small transparent bottom margin to avoid nametag overlap.
            var tex = RichTextTextureUtils.GenRichTextTexture(capi, bubbleVtml, baseFont, BubbleMaxTextWidthPx, background, extraBottomMarginPx: BubbleBottomMarginPx);
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
            Padding = 3,
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

    // NOTE: richtext texture rendering lives in RichTextTextureUtils.
}

/// <summary>
/// Per-frame LOS gating for overhead speech bubbles.
/// Vanilla's <c>DoRender2D</c> renders all <c>messageTextures</c> unconditionally.
/// This patch temporarily hides them when the local player cannot see the entity,
/// using a cached LOS check (same pattern as <see cref="TypingIndicatorRenderer"/>).
/// Bubbles reappear as soon as LOS is restored.
/// </summary>
[HarmonyPatch(typeof(EntityShapeRenderer), "DoRender2D")]
public static class SpeechBubbleLosPatches
{
    // Cached LOS per entity. Keyed by target entity id.
    // Asymmetric refresh: 250ms when visible, 500ms when hidden for smooth reveal without expensive raytracing every frame.
    private static readonly Dictionary<long, (bool canSee, long nextCheckMs)> _losCache = new();

    // Shared empty list to avoid per-frame allocations when hiding bubbles.
    private static readonly List<MessageTexture> EmptyList = new();

    /// <summary>
    /// Before vanilla renders bubbles, check cached LOS.
    /// If the entity is not visible, stash the messageTextures list and replace it with
    /// an empty list so vanilla skips bubble rendering. The Postfix restores it.
    /// </summary>
    public static void Prefix(EntityShapeRenderer __instance, ref List<MessageTexture> __state)
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
            if (localPlayerEntity == null || localPlayerEntity.EntityId == entity.EntityId)
            {
                return; // Always see your own bubbles.
            }

            var textures = SpeechBubbleVtmlPatches.MessageTexturesRef(__instance);
            if (textures == null || textures.Count == 0)
            {
                return; // Nothing to hide.
            }

            var nowMs = capi.World.ElapsedMilliseconds;
            if (!CanSeeCached(capi.World, nowMs, localPlayerEntity, entity))
            {
                // Temporarily replace the list with a shared empty one.
                // Vanilla will see no bubbles to render.
                __state = textures;
                SpeechBubbleVtmlPatches.MessageTexturesRef(__instance) = EmptyList;
            }
        }
        catch
        {
            // Crash-safe: never break vanilla rendering.
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
                // Safety: clear the shared empty list in case vanilla somehow added to it.
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
            // An entry is stale if its next-check time has long passed (entity no longer being rendered).
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
