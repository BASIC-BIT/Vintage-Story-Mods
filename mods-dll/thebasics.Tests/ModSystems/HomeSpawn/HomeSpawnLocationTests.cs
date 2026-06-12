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
        source.Dimension = 2;

        var location = HomeSpawnLocation.From(source);
        var restored = location.ToEntityPos();

        restored.X.Should().Be(source.X);
        restored.Y.Should().Be(source.Y);
        restored.Z.Should().Be(source.Z);
        restored.Yaw.Should().Be(source.Yaw);
        restored.Pitch.Should().Be(source.Pitch);
        restored.Roll.Should().Be(source.Roll);
        restored.Dimension.Should().Be(source.Dimension);
        location.IsSameDimensionAs(source).Should().BeTrue();
        location.Format().Should().Be("123.5, 64.3, 987.8");
    }

    [Fact]
    public void Registry_UsesDefaultHomeWhenNameIsMissing()
    {
        var registry = new HomeSpawnHomeRegistry();
        var location = HomeSpawnLocation.From(new EntityPos(1, 2, 3));

        registry.TrySetHome(null, location, maxHomes: 1, out var normalizedName).Should().BeTrue();

        normalizedName.Should().Be(HomeSpawnHomeRegistry.DefaultHomeName);
        registry.TryGetHome(null, out var restored).Should().BeTrue();
        restored.Should().BeSameAs(location);
    }

    [Fact]
    public void Registry_NormalizesNamesAndEnforcesLimitForNewHomes()
    {
        var registry = new HomeSpawnHomeRegistry();
        var first = HomeSpawnLocation.From(new EntityPos(1, 2, 3));
        var second = HomeSpawnLocation.From(new EntityPos(4, 5, 6));

        registry.TrySetHome(" Mine ", first, maxHomes: 1, out var normalizedName).Should().BeTrue();
        registry.TrySetHome("other", second, maxHomes: 1, out _).Should().BeFalse();
        registry.TrySetHome("MINE", second, maxHomes: 1, out _).Should().BeTrue();

        normalizedName.Should().Be("mine");
        registry.TryGetHome("mine", out var restored).Should().BeTrue();
        restored.Should().BeSameAs(second);
    }

    [Theory]
    [InlineData("valid-name_1", true)]
    [InlineData("bad name", false)]
    [InlineData("bad/name", false)]
    public void Registry_ValidatesNameShape(string name, bool expected)
    {
        HomeSpawnHomeRegistry.IsValidName(name, out _).Should().Be(expected);
    }
}
