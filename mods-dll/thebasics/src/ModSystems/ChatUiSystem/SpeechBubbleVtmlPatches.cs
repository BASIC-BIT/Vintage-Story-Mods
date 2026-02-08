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

    public static bool Prefix(EntityShapeRenderer __instance, int groupId, string message, EnumChatType chattype, string data)
    {
        // Feature flag is server-configured and delivered to client.
        if (!ChatUiSystem.IsSpeechBubbleVtmlEnabled())
        {
            return true;
        }

        try
        {
            Entity entity = __instance.entity;
            ICoreClientAPI capi = __instance.capi;

            if (capi == null || entity == null)
            {
                return true;
            }

            if (data == null || !data.Contains("from:") || !(entity.Pos.SquareDistanceTo(capi.World.Player.Entity.Pos.XYZ) < 400.0) || message.Length <= 0)
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
            var localEntity = capi.World?.Player?.Entity;
            if (localEntity != null && !VisibilityUtils.HasLineOfSight(capi.World, localEntity, entity))
            {
                return false;
            }

            // Bubble text comes from the data payload.
            // We support an optional metadata suffix (only used when this patch is enabled):
            //   from:<id>,msg:<text>,kind:<speech|emote|env>
            var rawMsg = parttwo[1];
            string kind = null;
            var kindIndex = rawMsg.LastIndexOf(",kind:", StringComparison.Ordinal);
            if (kindIndex >= 0)
            {
                kind = rawMsg[(kindIndex + 6)..].Trim();
                rawMsg = rawMsg[..kindIndex];
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

            LoadedTexture tex;
            if (hasVtml)
            {
                tex = GenRichTextBubbleTexture(capi, bubbleVtml, baseFont, BubbleMaxTextWidthPx, background);
                if (tex == null)
                {
                    // Fallback: strip tags and let vanilla-esque plain rendering handle it.
                    var plain = VtmlUtils.StripVtmlTags(bubbleVtml, capi.Logger);
                    tex = capi.Gui.TextTexture.GenTextTexture(plain, baseFont, BubbleMaxTextWidthPx, background, EnumTextOrientation.Center);
                }
            }
            else
            {
                tex = capi.Gui.TextTexture.GenTextTexture(bubbleVtml, baseFont, BubbleMaxTextWidthPx, background, EnumTextOrientation.Center);
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

        // Add subtle borders for non-speech bubbles.
        if (kind == "env")
        {
            // Cool, slightly airy background for environmental text.
            bg.FillColor = ColorUtil.Hex2Doubles("#E7F0FA");
            bg.BorderWidth = 2;
            bg.BorderColor = ColorUtil.Hex2Doubles("#7AA3C7");
        }
        else if (kind == "emote")
        {
            // Warm parchment background for emotes.
            bg.FillColor = ColorUtil.Hex2Doubles("#F8F2E6");
            bg.BorderWidth = 2;
            bg.BorderColor = ColorUtil.Hex2Doubles("#C7B27A");
        }

        return bg;
    }

    private static double[] GetBubbleFontColor(string kind)
    {
        // Speech bubbles use vanilla's white text.
        // Environmental/emote bubbles use lighter backgrounds, so use a dark font for contrast.
        if (kind == "env" || kind == "emote")
        {
            return ColorUtil.Hex2Doubles("#1B1B1B");
        }

        return ColorUtil.WhiteArgbDouble;
    }

    private static LoadedTexture GenRichTextBubbleTexture(ICoreClientAPI capi, string vtml, CairoFont baseFont, int maxTextWidthPx, TextBackground background)
    {
        if (capi == null || string.IsNullOrWhiteSpace(vtml))
        {
            return null;
        }

        try
        {
            var comps = VtmlUtil.Richtextify(capi, vtml, baseFont);

            // Measure with the maximum width.
            var guiScale = Math.Max(1, RuntimeEnv.GUIScale);
            var measureBounds = ElementBounds.FixedSize(maxTextWidthPx / (double)guiScale, 600 / (double)guiScale);
            measureBounds.ParentBounds = ElementBounds.Empty;
            var measure = new GuiElementRichtext(capi, comps, measureBounds);
            measure.BeforeCalcBounds();

            var textWidthPx = (int)Math.Min(maxTextWidthPx, Math.Ceiling(measure.MaxLineWidth));
            var textHeightPx = (int)Math.Ceiling(measure.TotalHeight);
            textWidthPx = Math.Max(1, textWidthPx);
            textHeightPx = Math.Max(1, textHeightPx);

            // Reflow/align using the final width for correct centering.
            var finalBounds = ElementBounds.FixedSize(textWidthPx / (double)guiScale, textHeightPx / (double)guiScale);
            finalBounds.ParentBounds = ElementBounds.Empty;
            var rich = new GuiElementRichtext(capi, comps, finalBounds);
            rich.BeforeCalcBounds();

            var surfaceWidth = textWidthPx + 2 * background.HorPadding;
            var surfaceHeight = textHeightPx + 2 * background.VerPadding;

            using var surface = new ImageSurface(Format.Argb32, surfaceWidth, surfaceHeight);
            using var ctx = new Context(surface);

            // Background
            GuiElement.RoundRectangle(ctx, 0, 0, surfaceWidth, surfaceHeight, background.Radius);
            ctx.SetSourceRGBA(background.FillColor);
            if (background.BorderWidth > 0)
            {
                ctx.FillPreserve();
                ctx.LineWidth = background.BorderWidth;
                ctx.SetSourceRGBA(background.BorderColor);
                ctx.Stroke();
            }
            else
            {
                ctx.Fill();
            }

            // Render richtext at padded offset
            var offsetBounds = ElementBounds.Fixed(
                background.HorPadding / (double)guiScale,
                background.VerPadding / (double)guiScale,
                textWidthPx / (double)guiScale,
                textHeightPx / (double)guiScale
            );
            offsetBounds.ParentBounds = ElementBounds.Empty;
            rich.ComposeFor(offsetBounds, ctx, surface);

            var tex = new LoadedTexture(capi);
            capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: false, ref tex);
            return tex;
        }
        catch
        {
            return null;
        }
    }
}
