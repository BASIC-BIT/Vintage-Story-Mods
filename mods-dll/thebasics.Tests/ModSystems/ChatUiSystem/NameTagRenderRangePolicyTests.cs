using FluentAssertions;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class NameTagRenderRangePolicyTests
{
    [Fact]
    public void SelfNeverEvaluatesLineOfSight()
    {
        NameTagRenderRangePatches.ShouldEvaluateLineOfSight(
                localPlayerEntityId: 10,
                targetEntityId: 10,
                showOnlyWhenTargeted: false,
                isTargeted: true,
                renderRange: 30,
                distanceSquared: 0)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(false, 10, 10, false)]
    [InlineData(true, 10, 10, false)]
    [InlineData(false, 10, 20, false)]
    [InlineData(true, 10, 20, true)]
    public void TargetOnlySettingAppliesOnlyToRemotePlayers(
        bool configuredShowOnlyWhenTargeted,
        long localPlayerEntityId,
        long targetEntityId,
        bool expected)
    {
        NameTagRenderRangePatches.ResolveShowOnlyWhenTargeted(
                configuredShowOnlyWhenTargeted,
                localPlayerEntityId,
                targetEntityId)
            .Should().Be(expected);
    }

    [Fact]
    public void TargetOnlySettingRemainsConfiguredUntilLocalPlayerIsKnown()
    {
        NameTagRenderRangePatches.ResolveShowOnlyWhenTargeted(
                configuredShowOnlyWhenTargeted: true,
                localPlayerEntityId: null,
                targetEntityId: 20)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void TargetOnlyGateMatchesVanilla(
        bool showOnlyWhenTargeted,
        bool isTargeted,
        bool expected)
    {
        NameTagRenderRangePatches.ShouldEvaluateLineOfSight(
                localPlayerEntityId: 10,
                targetEntityId: 20,
                showOnlyWhenTargeted,
                isTargeted,
                renderRange: 30,
                distanceSquared: 25)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(30, 899.999, true)]
    [InlineData(30, 900, false)]
    [InlineData(30, 900.001, false)]
    [InlineData(0, 0, false)]
    public void RangeGateUsesVanillasStrictSquaredDistanceBoundary(
        int renderRange,
        double distanceSquared,
        bool expected)
    {
        NameTagRenderRangePatches.ShouldEvaluateLineOfSight(
                localPlayerEntityId: 10,
                targetEntityId: 20,
                showOnlyWhenTargeted: false,
                isTargeted: false,
                renderRange,
                distanceSquared)
            .Should().Be(expected);
    }
}
