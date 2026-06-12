using System;
using thebasics.Configs;
using Vintagestory.API.Datastructures;

namespace thebasics.ModSystems.ProximityChat;

internal static class MapPlayerVisibilityConfigApplier
{
    public const string HideOtherPlayersKey = "mapHideOtherPlayers";
    public const string PlayerRenderDistanceKey = "mapPlayerRenderDistance";
    public const string ShowGroupPlayersKey = "mapShowGroupPlayers";

    public static void Apply(ModConfig config, ITreeAttribute worldConfig)
    {
        if (config?.ManageMapPlayerVisibility != true || worldConfig == null)
        {
            return;
        }

        worldConfig.SetBool(HideOtherPlayersKey, config.MapHideOtherPlayers);
        worldConfig.SetFloat(PlayerRenderDistanceKey, NormalizeRenderDistance(config.MapPlayerRenderDistance));
        worldConfig.SetBool(ShowGroupPlayersKey, false);
    }

    private static float NormalizeRenderDistance(int renderDistance)
    {
        return renderDistance < 0 ? -1f : Math.Max(0, renderDistance);
    }
}
