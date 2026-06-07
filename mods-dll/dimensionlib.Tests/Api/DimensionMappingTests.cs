using DimensionLib.Api;
using FluentAssertions;

namespace DimensionLib.Tests.Api;

public class DimensionMappingTests
{
    [Fact]
    public void Constructor_ClonesTransform()
    {
        var transform = new DimensionMappingTransform { ScaleX = 2, OffsetZ = 5 };

        var mapping = new DimensionMapping("test:mapping", "test", "test:source", "test:target", transform: transform);
        transform.ScaleX = 3;
        transform.OffsetZ = 9;

        mapping.Transform.ScaleX.Should().Be(2);
        mapping.Transform.OffsetZ.Should().Be(5);
    }

    [Fact]
    public void TransformGetter_ReturnsClone()
    {
        var mapping = new DimensionMapping(
            "test:mapping",
            "test",
            "test:source",
            "test:target",
            transform: new DimensionMappingTransform { ScaleX = 2 });

        var exposed = mapping.Transform;
        exposed.ScaleX = 99;

        mapping.Transform.ScaleX.Should().Be(2);
    }

    [Fact]
    public void SpecToMapping_DefaultsToIdentityTransformAndBidirectionalMapping()
    {
        var spec = new DimensionMappingSpec
        {
            MappingId = "test:mapping",
            OwnerModId = "test",
            SourceDimensionId = "test:source",
            TargetDimensionId = "test:target",
        };

        var mapping = spec.ToMapping();

        mapping.Bidirectional.Should().BeTrue();
        mapping.IsTransient.Should().BeFalse();
        mapping.Transform.ScaleX.Should().Be(1);
        mapping.Transform.ScaleY.Should().Be(1);
        mapping.Transform.ScaleZ.Should().Be(1);
    }

    [Fact]
    public void SpecToMapping_PreservesTransientFlag()
    {
        var spec = new DimensionMappingSpec
        {
            MappingId = "test:mapping",
            OwnerModId = "test",
            SourceDimensionId = "test:source",
            TargetDimensionId = "test:target",
            IsTransient = true,
        };

        var mapping = spec.ToMapping();

        mapping.IsTransient.Should().BeTrue();
    }

    [Fact]
    public void TransformClone_PreservesScaleAndOffsetValues()
    {
        var transform = new DimensionMappingTransform
        {
            ScaleX = 0.125,
            ScaleY = 2,
            ScaleZ = 0.25,
            OffsetX = 1,
            OffsetY = 2,
            OffsetZ = 3,
        };

        var clone = transform.Clone();

        clone.Should().NotBeSameAs(transform);
        clone.ScaleX.Should().Be(0.125);
        clone.ScaleY.Should().Be(2);
        clone.ScaleZ.Should().Be(0.25);
        clone.OffsetX.Should().Be(1);
        clone.OffsetY.Should().Be(2);
        clone.OffsetZ.Should().Be(3);
    }
}
