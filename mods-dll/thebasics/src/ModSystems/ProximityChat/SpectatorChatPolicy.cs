using thebasics.Configs;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat;

/// <summary>
/// Centralizes the spectator-specific chat contract. Spectators may deliberately communicate,
/// but passive entity-attached cues must not reveal an otherwise invisible player.
/// </summary>
internal static class SpectatorChatPolicy
{
    internal static bool IsSpectator(IPlayer player)
    {
        return player?.WorldData?.CurrentGameMode == EnumGameMode.Spectator;
    }

    internal static bool IsSpectator(Entity entity)
    {
        return entity is EntityPlayer playerEntity && IsSpectator(playerEntity.Player);
    }

    /// <summary>
    /// Server policy only applies once a player is fully in-world. Vintage Story temporarily
    /// reports Spectator while a player is connecting, which is not a deliberate mode choice.
    /// </summary>
    internal static bool IsActiveSpectator(IServerPlayer player)
    {
        return player?.ConnectionState == EnumClientState.Playing && IsSpectator(player);
    }

    internal static bool CanPlaceEnvironmentalMessage(IServerPlayer player, ModConfig config)
    {
        return !IsActiveSpectator(player) || config?.AllowSpectatorPlacedEnvironmentalMessages == true;
    }

    internal static bool ShouldProtectRoleplayChat(IServerPlayer player, ModConfig config)
    {
        return IsActiveSpectator(player) && config?.ProtectSpectatorRoleplayChat == true;
    }

    internal static bool ShouldEmitEntityAttachedCues(IServerPlayer player)
    {
        return !IsActiveSpectator(player);
    }

    internal static bool UseNicknameInLocalOoc(IServerPlayer player, ModConfig config)
    {
        if (IsActiveSpectator(player))
        {
            return config?.UseNicknameInSpectatorOOC == true;
        }

        return config?.UseNicknameInOOC == true;
    }
}
