using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using Vintagestory.API.Datastructures;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class MapPlayerVisibilityConfigApplierTests
{
    [Fact]
    public void Apply_DoesNothingWhenManagementDisabled()
    {
        var worldConfig = new TreeAttribute();
        var config = new ModConfig
        {
            ManageMapPlayerVisibility = false,
            MapHideOtherPlayers = true,
            MapPlayerRenderDistance = 12
        };

        MapPlayerVisibilityConfigApplier.Apply(config, worldConfig);

        worldConfig.HasAttribute(MapPlayerVisibilityConfigApplier.HideOtherPlayersKey).Should().BeFalse();
        worldConfig.HasAttribute(MapPlayerVisibilityConfigApplier.PlayerRenderDistanceKey).Should().BeFalse();
        worldConfig.HasAttribute(MapPlayerVisibilityConfigApplier.ShowGroupPlayersKey).Should().BeFalse();
    }

    [Fact]
    public void Apply_WritesVanillaWorldConfigAndDisablesGroupBypass()
    {
        var worldConfig = new TreeAttribute();
        var config = new ModConfig
        {
            ManageMapPlayerVisibility = true,
            MapHideOtherPlayers = true,
            MapPlayerRenderDistance = 48
        };

        MapPlayerVisibilityConfigApplier.Apply(config, worldConfig);

        worldConfig.GetBool(MapPlayerVisibilityConfigApplier.HideOtherPlayersKey).Should().BeTrue();
        worldConfig.GetFloat(MapPlayerVisibilityConfigApplier.PlayerRenderDistanceKey).Should().Be(48f);
        worldConfig.GetBool(MapPlayerVisibilityConfigApplier.ShowGroupPlayersKey, defaultValue: true).Should().BeFalse();
    }

    [Fact]
    public void Apply_NormalizesNegativeRenderDistanceToUnlimitedSentinel()
    {
        var worldConfig = new TreeAttribute();
        var config = new ModConfig
        {
            ManageMapPlayerVisibility = true,
            MapPlayerRenderDistance = -42
        };

        MapPlayerVisibilityConfigApplier.Apply(config, worldConfig);

        worldConfig.GetFloat(MapPlayerVisibilityConfigApplier.PlayerRenderDistanceKey).Should().Be(-1f);
    }
}
