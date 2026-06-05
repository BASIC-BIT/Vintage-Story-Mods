using DimensionLib.Api;
using DimensionLib.Services;
using FluentAssertions;

namespace DimensionLib.Tests.Services;

public class DimensionMappingSpecValidatorTests
{
    [Fact]
    public void Validate_TrimsIdsAndDefaultsMissingTransform()
    {
        var spec = new DimensionMappingSpec
        {
            MappingId = " test:mapping ",
            OwnerModId = " test ",
            SourceDimensionId = " test:source ",
            TargetDimensionId = " test:target ",
            Transform = null!,
        };

        var result = DimensionMappingSpecValidator.Validate(spec);

        result.Success.Should().BeTrue();
        spec.MappingId.Should().Be("test:mapping");
        spec.OwnerModId.Should().Be("test");
        spec.SourceDimensionId.Should().Be("test:source");
        spec.TargetDimensionId.Should().Be("test:target");
        spec.Transform.Should().NotBeNull();
        spec.Transform.ScaleX.Should().Be(1);
    }

    [Theory]
    [InlineData(null, "test", "test:source", "test:target", "missing-mapping-id")]
    [InlineData("test:mapping", null, "test:source", "test:target", "missing-owner-mod-id")]
    [InlineData("test:mapping", "test", null, "test:target", "missing-source-dimension-id")]
    [InlineData("test:mapping", "test", "test:source", null, "missing-target-dimension-id")]
    public void Validate_RejectsMissingRequiredIds(string? mappingId, string? ownerModId, string? sourceDimensionId, string? targetDimensionId, string errorCode)
    {
        var spec = new DimensionMappingSpec
        {
            MappingId = mappingId!,
            OwnerModId = ownerModId!,
            SourceDimensionId = sourceDimensionId!,
            TargetDimensionId = targetDimensionId!,
        };

        var result = DimensionMappingSpecValidator.Validate(spec);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void Validate_RejectsZeroScale()
    {
        var spec = ValidSpec();
        spec.Transform.ScaleZ = 0;

        var result = DimensionMappingSpecValidator.Validate(spec);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid-mapping-scale");
    }

    [Fact]
    public void Validate_RejectsNonFiniteScaleAndOffset()
    {
        var badScale = ValidSpec();
        badScale.Transform.ScaleX = double.PositiveInfinity;
        var badOffset = ValidSpec();
        badOffset.Transform.OffsetY = double.NaN;

        DimensionMappingSpecValidator.Validate(badScale).ErrorCode.Should().Be("invalid-mapping-scale");
        DimensionMappingSpecValidator.Validate(badOffset).ErrorCode.Should().Be("invalid-mapping-offset");
    }

    [Fact]
    public void SameMapping_ComparesEndpointsBidirectionalityAndTransform()
    {
        var spec = ValidSpec();
        spec.Transform.ScaleX = 0.125;
        var mapping = spec.ToMapping();

        DimensionMappingSpecValidator.SameMapping(mapping, spec).Should().BeTrue();
        spec.Transform.OffsetX = 1;
        DimensionMappingSpecValidator.SameMapping(mapping, spec).Should().BeFalse();
    }

    private static DimensionMappingSpec ValidSpec()
    {
        return new DimensionMappingSpec
        {
            MappingId = "test:mapping",
            OwnerModId = "test",
            SourceDimensionId = "test:source",
            TargetDimensionId = "test:target",
            Transform = DimensionMappingTransform.Identity(),
        };
    }
}
