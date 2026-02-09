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
    private static readonly AccessTools.FieldRef<EntityShapeRenderer, List<MessageTexture>> MessageTexturesRef =
        AccessTools.FieldRefAccess<EntityShapeRenderer, List<MessageTexture>>("messageTextures");

    private const int BubbleMaxTextWidthPx = 350;
    private const int BubbleBottomMarginPx = 16;

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

            int.TryParse(partone[1], out var entityid);
            if (entity.EntityId != entityid)
            {
                return true;
            }

            // Line-of-sight gating: overhead bubbles should behave like a visual cue.
            // If the local player cannot see the target entity, don't display the bubble.
            // (Chat log still receives the message as normal.)
            if (!VisibilityUtils.HasLineOfSight(capi.World, localPlayerEntity, entity))
            {
                return false;
            }

            // Bubble text comes from the data payload.
            // When this patch is enabled, the server may attach an optional kind marker for styling:
            //   New (preferred): from:<id>,msg\u001fkind=<emote|env>:<text>
            //   Legacy:          from:<id>,msg:<text>\u001fkind:<emote|env>
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
                Orientation = EnumTextOrientation.Center
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

        return bg;
    }

    private static double[] GetBubbleFontColor(string kind)
    {
        // Keep vanilla's white text; background stays consistent across kinds.
        return ColorUtil.WhiteArgbDouble;
    }

    // NOTE: richtext texture rendering lives in RichTextTextureUtils.
}
