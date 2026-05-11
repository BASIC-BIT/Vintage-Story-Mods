using System;
using System.Collections.Generic;
using HarmonyLib;
using thebasics.ModSystems.CharacterSheets;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Replaces vanilla's plain-text nametag texture with a richtext-rendered one that supports VTML
/// (colored names via <c>&lt;font color=...&gt;</c>) and an optional inline headshot image.
///
/// Vanilla's <c>OnNameChanged</c> rebuilds the texture from the watched-attribute "nametag/name"
/// string. We piggyback on that listener: the server stashes the headshot hash and nickname color
/// inside the same "nametag" tree, so any change to those values triggers a rebuild for free.
/// </summary>
[HarmonyPatch(typeof(EntityBehaviorNameTag), "OnNameChanged")]
public static class EntityBehaviorNameTagPatches
{
    private static readonly AccessTools.FieldRef<EntityBehaviorNameTag, LoadedTexture> NameTagTextureRef =
        AccessTools.FieldRefAccess<EntityBehaviorNameTag, LoadedTexture>("nameTagTexture");

    // Maps player UID → entity reference for "rebuild this nametag" calls when their headshot
    // bytes finally arrive client-side. The set is small (online players only) and entries are
    // pruned lazily when an entity is found stale during rebuild.
    private static readonly Dictionary<string, Entity> _trackedPlayerEntities = new();

    // Match the bubble look so headshot + name read as one nameplate.
    private const int NametagMaxTextWidthPx = 400;

    public static void Postfix(EntityBehaviorNameTag __instance, Entity ___entity)
    {
        if (!ChatUiSystem.IsCustomNametagEnabled())
        {
            return;
        }

        try
        {
            ReplaceTexture(__instance, ___entity);
        }
        catch
        {
            // Crash-safe: don't break vanilla nametag rendering.
        }
    }

    /// <summary>
    /// Called by ChatUiSystem when a HeadshotFetchResult arrives and a new texture is cached.
    /// Forces the matching player's nametag texture to rebuild so the now-available bitmap
    /// composites in.
    /// </summary>
    public static void OnHeadshotTextureCached(string targetPlayerUid)
    {
        if (string.IsNullOrEmpty(targetPlayerUid)) return;
        if (!_trackedPlayerEntities.TryGetValue(targetPlayerUid, out var entity) || !IsEntityStillValid(entity))
        {
            _trackedPlayerEntities.Remove(targetPlayerUid);
            return;
        }

        var behavior = entity.GetBehavior<EntityBehaviorNameTag>();
        if (behavior == null) return;

        try
        {
            ReplaceTexture(behavior, entity);
        }
        catch
        {
            // Ignore — nametag will refresh on the next vanilla OnNameChanged.
        }
    }

    private static bool IsEntityStillValid(Entity entity)
    {
        if (entity == null || !entity.Alive)
        {
            return false;
        }

        var world = entity.World;
        return world != null && world.GetEntityById(entity.EntityId) == entity;
    }

    private static void ReplaceTexture(EntityBehaviorNameTag behavior, Entity entity)
    {
        var capi = entity?.World?.Api as ICoreClientAPI;
        if (capi == null || entity == null)
        {
            return;
        }

        // Track player entities so HeadshotFetchResult can poke the right one.
        if (entity is EntityPlayer ep && ep.PlayerUID is { Length: > 0 } uid)
        {
            _trackedPlayerEntities[uid] = entity;
        }

        var nametagTree = entity.WatchedAttributes?.GetTreeAttribute("nametag");
        var name = nametagTree?.GetString("name") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var headshotHash = nametagTree?.GetString(CharacterSheetSystem.HeadshotHashAttrKey) ?? string.Empty;
        BitmapExternal headshotBitmap = null;
        var inlineImageEnabled = ChatUiSystem.IsHeadshotInNametagEnabled();
        if (inlineImageEnabled && !string.IsNullOrEmpty(headshotHash))
        {
            var pngBytes = ChatUiSystem.TryGetCachedHeadshotPngBytes(headshotHash);
            if (pngBytes != null && pngBytes.Length > 0)
            {
                try
                {
                    headshotBitmap = new BitmapExternal(pngBytes, pngBytes.Length, capi.Logger);
                }
                catch
                {
                    headshotBitmap?.Dispose();
                    headshotBitmap = null;
                }
            }
            else if (entity is EntityPlayer playerEntity && playerEntity.PlayerUID is { Length: > 0 } trackedUid)
            {
                // Bytes not yet available — kick off a fetch and we'll rebuild when they arrive.
                ChatUiSystem.RequestHeadshotForHash(trackedUid, headshotHash);
            }
        }

        // Text bubble styled like vanilla so the visual baseline stays familiar; the framed headshot
        // floats next to it as a separate visual element (MMO-style portrait card).
        var background = new TextBackground
        {
            FillColor = GuiStyle.DialogLightBgColor,
            Padding = 3,
            Radius = GuiStyle.ElementBGRadius,
            Shade = true,
            BorderColor = GuiStyle.DialogBorderColor,
            BorderWidth = 3.0
        };

        var baseFont = CairoFont.WhiteMediumText().WithColor(ColorUtil.WhiteArgbDouble);
        baseFont.Orientation = EnumTextOrientation.Left;

        try
        {
            // Vanilla supports legacy "nametag-..." Lang lookups for hardcoded entity names.
            // Our display names are runtime-built and never collide; preserve the lookup just in case.
            var resolvedName = Lang.GetIfExists("nametag-" + name.ToLowerInvariant()) ?? name;

            var newTex = NametagComposer.Compose(capi, new NametagComposer.Options
            {
                Vtml = resolvedName,
                BaseFont = baseFont,
                MaxTextWidthPx = NametagMaxTextWidthPx,
                TextBackground = background,
                HeadshotBitmap = headshotBitmap,
                HeadshotRenderSizePx = ChatUiSystem.GetNametagInlineImagePixelSize()
            });

            if (newTex == null)
            {
                return;
            }

            var oldTex = NameTagTextureRef(behavior);
            oldTex?.Dispose();
            NameTagTextureRef(behavior) = newTex;
        }
        finally
        {
            headshotBitmap?.Dispose();
        }
    }
}
