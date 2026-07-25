using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.HomeSpawn;
using thebasics.ModSystems.Teleportation;

namespace thebasics.Tests.ModSystems.Teleportation;

public class TeleportGearRequirementTests
{
    [Fact]
    public void EnabledTop_RequiresGearBeforeWarmup()
    {
        var consumeCalls = 0;
        var requirement = new TeleportGearRequirement(true, () => false, () =>
        {
            consumeCalls++;
            return true;
        });

        requirement.CanBegin().Should().BeFalse();
        consumeCalls.Should().Be(0);
    }

    [Fact]
    public void EnabledTop_ConsumesExactlyOnceOnSuccessfulCompletion()
    {
        var consumeCalls = 0;
        var requirement = new TeleportGearRequirement(true, () => true, () =>
        {
            consumeCalls++;
            return true;
        });

        requirement.CanBegin().Should().BeTrue();
        requirement.TryPayOnCompletion().Should().BeTrue();
        consumeCalls.Should().Be(1);
    }

    [Theory]
    [InlineData("no-safe-target")]
    [InlineData("cancelled-warmup")]
    public void EnabledTop_AttemptsThatNeverCompleteDoNotConsume(string incompleteOutcome)
    {
        var consumeCalls = 0;
        var requirement = new TeleportGearRequirement(true, () => true, () =>
        {
            consumeCalls++;
            return true;
        });

        requirement.CanBegin().Should().BeTrue(incompleteOutcome);
        consumeCalls.Should().Be(0);
    }

    [Fact]
    public void DisabledTop_RemainsFree()
    {
        var holdChecks = 0;
        var consumeCalls = 0;
        var requirement = new TeleportGearRequirement(false, () =>
        {
            holdChecks++;
            return false;
        }, () =>
        {
            consumeCalls++;
            return false;
        });

        requirement.CanBegin().Should().BeTrue();
        requirement.TryPayOnCompletion().Should().BeTrue();
        holdChecks.Should().Be(0);
        consumeCalls.Should().Be(0);
    }

    [Fact]
    public void Stuck_RemainsGearFreeWhenTopRequirementIsEnabled()
    {
        var config = new ModConfig
        {
            HomeSpawnRequireTemporalGear = true,
            Teleportation = new TeleportationConfig
            {
                TopRequireTemporalGear = true,
                BackRequireTemporalGear = true
            }
        };

        HomeSpawnSystem.RequiresTemporalGearForCommand(config, "stuck").Should().BeFalse();
        HomeSpawnSystem.RequiresTemporalGearForCommand(config, "top").Should().BeTrue();
    }
}
