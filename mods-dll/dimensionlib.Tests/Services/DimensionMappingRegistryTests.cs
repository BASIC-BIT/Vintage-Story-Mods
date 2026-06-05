using DimensionLib.Api;
using DimensionLib.Services;
using FluentAssertions;

namespace DimensionLib.Tests.Services;

public class DimensionMappingRegistryTests
{
    [Fact]
    public void Register_AddsMappingAndAllowsLookup()
    {
        var registry = new DimensionMappingRegistry();
        var spec = ValidSpec();

        var result = registry.Register(spec);

        result.Success.Should().BeTrue();
        registry.Get("test:mapping").Value.SourceDimensionId.Should().Be("test:source");
        registry.Mappings.Should().ContainSingle();
    }

    [Fact]
    public void Register_IsIdempotentForSameMapping()
    {
        var registry = new DimensionMappingRegistry();
        registry.Register(ValidSpec()).Success.Should().BeTrue();

        var result = registry.Register(ValidSpec());

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("already registered");
    }

    [Fact]
    public void Register_RejectsSameIdWithDifferentTransform()
    {
        var registry = new DimensionMappingRegistry();
        registry.Register(ValidSpec()).Success.Should().BeTrue();
        var changed = ValidSpec();
        changed.Transform.ScaleX = 2;

        var result = registry.Register(changed);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("mapping-id-conflict");
    }

    [Fact]
    public void Get_RejectsUnknownMapping()
    {
        var registry = new DimensionMappingRegistry();

        var result = registry.Get("missing:mapping");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("unknown-mapping");
    }

    private static DimensionMappingSpec ValidSpec()
    {
        return new DimensionMappingSpec
        {
            MappingId = "test:mapping",
            OwnerModId = "test",
            SourceDimensionId = "test:source",
            TargetDimensionId = "test:target",
        };
    }
}
