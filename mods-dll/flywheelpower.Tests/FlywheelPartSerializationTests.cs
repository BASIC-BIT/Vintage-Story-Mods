using FlywheelPower;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace FlywheelPower.Tests;

public sealed class FlywheelPartSerializationTests
{
    [Fact]
    public void PrincipalRoundTripPreservesNegativeCoordinatesAndDimension()
    {
        BEFlywheelPart source = new()
        {
            Principal = new BlockPos(-12, 34, -56, 7)
        };
        TreeAttribute tree = new();

        source.WritePrincipal(tree);
        BEFlywheelPart restored = new();
        restored.ReadPrincipal(tree);

        Assert.NotNull(restored.Principal);
        Assert.Equal(-12, restored.Principal.X);
        Assert.Equal(34, restored.Principal.Y);
        Assert.Equal(-56, restored.Principal.Z);
        Assert.Equal(7, restored.Principal.dimension);
    }
}
