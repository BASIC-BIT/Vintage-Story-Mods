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

    internal static void RebuildTexture(Entity entity)
    {
        if (!ChatUiSystem.IsCustomNametagEnabled() || !IsEntityStillValid(entity))
        {
            return;
        }

        var behavior = entity.GetBehavior<EntityBehaviorNameTag>();
        if (behavior == null)
        {
            return;
        }

        try
        {
            ReplaceTexture(behavior, entity);
        }
        catch
        {
            // Cosmetic only; keep vanilla texture if the custom rebuild fails.
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
        var capi = GetClientApi(entity);
        if (capi == null)
        {
            return;
        }

        TrackPlayerEntity(entity);

        var name = GetNametagValue(entity, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var headshotHash = GetNametagValue(entity, CharacterSheetSystem.HeadshotHashAttrKey);
        var nicknameColor = GetNametagValue(entity, CharacterSheetSystem.NicknameColorAttrKey);
        var backgroundColor = GetNametagValue(entity, CharacterSheetSystem.NametagBackgroundColorAttrKey);
        var borderColor = GetNametagValue(entity, CharacterSheetSystem.NametagBorderColorAttrKey);
        var playerName = GetPlayerName(entity);
        var headshotBitmap = TryCreateHeadshotBitmap(capi, entity, headshotHash);

        try
        {
            ApplyNametagTexture(behavior, ComposeNametagTexture(capi, name, nicknameColor, backgroundColor, borderColor, playerName, headshotBitmap));
        }
        finally
        {
            headshotBitmap?.Dispose();
        }
    }

    private static ICoreClientAPI GetClientApi(Entity entity)
    {
        return entity?.World?.Api as ICoreClientAPI;
    }

    private static string GetNametagValue(Entity entity, string key)
    {
        return entity.WatchedAttributes?.GetTreeAttribute("nametag")?.GetString(key) ?? string.Empty;
    }

    private static string GetPlayerName(Entity entity)
    {
        return (entity as EntityPlayer)?.Player?.PlayerName ?? string.Empty;
    }

    private static void ApplyNametagTexture(EntityBehaviorNameTag behavior, LoadedTexture newTex)
    {
        if (newTex == null)
        {
            return;
        }

        var oldTex = NameTagTextureRef(behavior);
        oldTex?.Dispose();
        NameTagTextureRef(behavior) = newTex;
    }

    private static void TrackPlayerEntity(Entity entity)
    {
        if (entity is EntityPlayer ep && ep.PlayerUID is { Length: > 0 } uid)
        {
            _trackedPlayerEntities[uid] = entity;
        }
    }

    private static BitmapExternal TryCreateHeadshotBitmap(ICoreClientAPI capi, Entity entity, string headshotHash)
    {
        if (!ChatUiSystem.IsHeadshotInNametagEnabled() || string.IsNullOrEmpty(headshotHash))
        {
            return null;
        }

        var pngBytes = ChatUiSystem.TryGetCachedHeadshotPngBytes(headshotHash);
        if (pngBytes != null && pngBytes.Length > 0)
        {
            return TryDecodeHeadshotBitmap(capi, pngBytes);
        }

        RequestMissingHeadshot(entity, headshotHash);
        return null;
    }

    private static BitmapExternal TryDecodeHeadshotBitmap(ICoreClientAPI capi, byte[] pngBytes)
    {
        try
        {
            return new BitmapExternal(pngBytes, pngBytes.Length, capi.Logger);
        }
        catch
        {
            return null;
        }
    }

    private static void RequestMissingHeadshot(Entity entity, string headshotHash)
    {
        if (entity is EntityPlayer playerEntity && playerEntity.PlayerUID is { Length: > 0 } trackedUid)
        {
            ChatUiSystem.RequestHeadshotForHash(trackedUid, headshotHash);
        }
    }

    private static LoadedTexture ComposeNametagTexture(ICoreClientAPI capi, string name, string nicknameColor, string backgroundColor, string borderColor, string playerName, BitmapExternal headshotBitmap)
    {
        var resolvedName = Lang.GetIfExists("nametag-" + name.ToLowerInvariant()) ?? name;
        var vtml = WrapWithNicknameColor(resolvedName, nicknameColor, playerName);

        return NametagComposer.Compose(capi, new NametagComposer.Options
        {
            Vtml = vtml,
            BaseFont = CreateNametagBaseFont(),
            MaxTextWidthPx = NametagMaxTextWidthPx,
            TextBackground = CreateNametagBackground(backgroundColor, borderColor),
            HeadshotBitmap = headshotBitmap,
            HeadshotRenderSizePx = ChatUiSystem.GetNametagInlineImagePixelSize()
        });
    }

    private static CairoFont CreateNametagBaseFont()
    {
        var baseFont = CairoFont.WhiteMediumText().WithColor(ColorUtil.WhiteArgbDouble);
        baseFont.Orientation = EnumTextOrientation.Left;
        return baseFont;
    }

    private static TextBackground CreateNametagBackground(string backgroundColor, string borderColor)
    {
        return new TextBackground
        {
            FillColor = ResolveNametagColor(backgroundColor, ChatUiSystem.GetNametagBackgroundColor(), GuiStyle.DialogLightBgColor),
            Padding = 3,
            Radius = GuiStyle.ElementBGRadius,
            Shade = true,
            BorderColor = ResolveNametagColor(borderColor, ChatUiSystem.GetNametagBorderColor(), GuiStyle.DialogBorderColor),
            BorderWidth = 3.0
        };
    }

    private static double[] ResolveNametagColor(string playerColor, string serverColor, double[] fallback)
    {
        if (TryResolveNametagColor(playerColor, out var resolvedPlayerColor))
        {
            return resolvedPlayerColor;
        }

        return TryResolveNametagColor(serverColor, out var resolvedServerColor)
            ? resolvedServerColor
            : fallback;
    }

    private static bool TryResolveNametagColor(string hexColor, out double[] color)
    {
        color = null;
        if (string.IsNullOrWhiteSpace(hexColor))
        {
            return false;
        }

        try
        {
            color = ColorUtil.Hex2Doubles(hexColor.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Wraps the styled portion of the nametag name with a VTML color. When the name has the
    /// canonical <c>"DisplayName (PlayerName)"</c> form from BuildNametagDisplayName, only the
    /// DisplayName part is colored — the parenthetical PlayerName suffix stays neutral.
    /// </summary>
    private static string WrapWithNicknameColor(string plainName, string colorHex, string playerName)
    {
        if (string.IsNullOrEmpty(plainName))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return VtmlUtils.EscapeVtml(plainName);
        }

        var color = colorHex.Trim();
        if (!color.StartsWith('#'))
        {
            color = "#" + color;
        }

        if (!string.IsNullOrEmpty(playerName))
        {
            var suffix = $" ({playerName})";
            if (plainName.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                var styled = plainName.Substring(0, plainName.Length - suffix.Length);
                return $"<font color=\"{color}\">{VtmlUtils.EscapeVtml(styled)}</font>{VtmlUtils.EscapeVtml(suffix)}";
            }
        }

        return $"<font color=\"{color}\">{VtmlUtils.EscapeVtml(plainName)}</font>";
    }
}
