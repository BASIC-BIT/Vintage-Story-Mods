using FluentAssertions;
using thebasics.ModSystems.HomeSpawn;
using Vintagestory.API.Common.Entities;

namespace thebasics.Tests.ModSystems.HomeSpawn;

public class HomeSpawnLocationTests
{
    [Fact]
    public void FromAndToEntityPos_PreserveCoordinatesAndLookDirection()
    {
        var source = new EntityPos(123.5, 64.25, 987.75, 1.2f, 0.3f, 0.1f);

        var location = HomeSpawnLocation.From(source);
        var restored = location.ToEntityPos();

        restored.X.Should().Be(source.X);
        restored.Y.Should().Be(source.Y);
        restored.Z.Should().Be(source.Z);
        restored.Yaw.Should().Be(source.Yaw);
        restored.Pitch.Should().Be(source.Pitch);
        restored.Roll.Should().Be(source.Roll);
        location.Format().Should().Be("123.5, 64.3, 987.8");
    }
}
