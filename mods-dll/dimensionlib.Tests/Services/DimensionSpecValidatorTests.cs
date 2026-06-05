using DimensionLib.Api;
using DimensionLib.Services;
using FluentAssertions;

namespace DimensionLib.Tests.Services;

public class DimensionSpecValidatorTests
{
    [Fact]
    public void Validate_TrimsIdsAndAcceptsValidSpec()
    {
        var spec = TestDimensions.Spec(dimensionId: " test:dim ", ownerModId: " test ", generatorId: " generator ");

        var result = DimensionSpecValidator.Validate(spec, firstAllowedDimensionPlaneId: 3);

        result.Success.Should().BeTrue();
        spec.DimensionId.Should().Be("test:dim");
        spec.OwnerModId.Should().Be("test");
        spec.GeneratorId.Should().Be("generator");
    }

    [Theory]
    [InlineData(null, "test", "missing-dimension-id")]
    [InlineData("test:dim", null, "missing-owner-mod-id")]
    public void Validate_RejectsMissingRequiredIds(string? dimensionId, string? ownerModId, string errorCode)
    {
        var spec = TestDimensions.Spec(dimensionId: dimensionId!, ownerModId: ownerModId!);

        var result = DimensionSpecValidator.Validate(spec, firstAllowedDimensionPlaneId: 3);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void Validate_RejectsReservedDimensionPlanes()
    {
        var spec = TestDimensions.Spec(dimensionPlaneId: 2);

        var result = DimensionSpecValidator.Validate(spec, firstAllowedDimensionPlaneId: 3);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("reserved-dimension-plane");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void Validate_RejectsNonPositiveRegionSizes(int sizeX, int sizeZ)
    {
        var spec = TestDimensions.Spec(chunkSizeX: sizeX, chunkSizeZ: sizeZ);

        var result = DimensionSpecValidator.Validate(spec, firstAllowedDimensionPlaneId: 3);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid-size");
    }

    [Fact]
    public void Validate_NormalizesVisualSettings()
    {
        var settings = new DimensionVisualSettings
        {
            LerpSpeed = -1,
            Sky = new DimensionSkyVisualSettings { Color = new DimensionColor4(2, -1, 0.5f, 3) },
            Fog = new DimensionFogVisualSettings { Density = new DimensionWeightedFloat(-2, 2) },
            Scene = new DimensionSceneVisualSettings { MinimumLight = 2, LightLift = new DimensionColor3(-1, 2, 0.5f) },
        };
        var spec = TestDimensions.Spec(visualSettings: settings);

        var result = DimensionSpecValidator.Validate(spec, firstAllowedDimensionPlaneId: 3);

        result.Success.Should().BeTrue();
        settings.LerpSpeed.Should().Be(0.08f);
        settings.Sky.Color.Red.Should().Be(1);
        settings.Sky.Color.Green.Should().Be(0);
        settings.Sky.Color.Alpha.Should().Be(1);
        settings.Fog.Density.Value.Should().Be(0);
        settings.Fog.Density.Weight.Should().Be(1);
        settings.Scene.MinimumLight.Should().Be(0.8f);
        settings.Scene.LightLift.Red.Should().Be(0);
        settings.Scene.LightLift.Green.Should().Be(1);
    }
}
