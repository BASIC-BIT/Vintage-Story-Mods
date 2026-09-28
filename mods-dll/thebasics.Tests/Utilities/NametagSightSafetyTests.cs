using System.Reflection;
using FluentAssertions;
using NSubstitute;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.Utilities;

public class NametagSightSafetyTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(512, true)]
    [InlineData(512.01, false)]
    [InlineData(104412.27544, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.NegativeInfinity, false)]
    public void BoundsRayLengthAndRejectsNonfiniteEndpoints(double distance, bool expected)
    {
        VisibilityUtils.IsSafeNametagSegment(new Vec3d(), new Vec3d(distance, 0, 0))
            .Should().Be(expected);
    }

    [Fact]
    public void RejectsInvalidOriginsAndUnrepresentableCoordinates()
    {
        VisibilityUtils.IsSafeNametagSegment(new Vec3d(double.NaN, 0, 0), new Vec3d()).Should().BeFalse();
        VisibilityUtils.IsSafeNametagSegment(new Vec3d(double.MaxValue, 0, 0), new Vec3d(double.MaxValue, 0, 0)).Should().BeFalse();
        VisibilityUtils.IsSafeNametagSegment(null!, new Vec3d()).Should().BeFalse();
        VisibilityUtils.IsSafeNametagSegment(new Vec3d(), new Vec3d(400, 400, 0)).Should().BeFalse();
    }

    [Fact]
    public void CapturedLongRayIsRejectedWithoutWorldAccess()
    {
        var world = Substitute.For<IWorldAccessor>();
        var observer = new TestEntity { EntityId = 1 };
        var target = new TestEntity { EntityId = 2 };
        // Relative displacement of the captured 104,412-block ray; no private world coordinates.
        target.Pos.SetPos(-69276.74716026976, -104.05270448327065, -78119.42612879443);

        VisibilityUtils.HasNametagLineOfSight(world, observer, target).Should().BeFalse();
        world.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void DimensionChangesAreRejectedBeforeReadingTheLosCache()
    {
        var world = Substitute.For<IWorldAccessor>();
        var observer = new TestEntity { EntityId = 1 };
        var target = new TestEntity { EntityId = 2 };
        target.Pos.Dimension = 1;
        var method = typeof(NameTagRenderRangePatches).GetMethod("CanSeeCached", BindingFlags.Static | BindingFlags.NonPublic)!;

        method.Invoke(null, [world, observer, target]).Should().Be(false);
        world.ReceivedCalls().Should().BeEmpty("the guard must run before cache clock access");
    }

    [Fact]
    public void MalformedEyeAndBodySamplesAreRejectedBeforeRayTraversal()
    {
        var world = Substitute.For<IWorldAccessor, IWorldIntersectionSupplier>();
        var observer = new TestEntity { EntityId = 1 };
        var target = new TestEntity
        {
            EntityId = 2,
            LocalEyePos = new Vec3d(104412, 0, 0),
            CollisionBox = new Cuboidf(0, 0, 0, 1, 1044120, 1)
        };

        VisibilityUtils.HasNametagLineOfSight(world, observer, target).Should().BeFalse();
        world.ReceivedCalls().Should().BeEmpty();
    }

    private sealed class TestEntity : Entity { }
}
