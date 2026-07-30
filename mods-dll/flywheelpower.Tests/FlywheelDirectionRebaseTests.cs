using FlywheelPower;
using Vintagestory.API.MathTools;

namespace FlywheelPower.Tests;

public sealed class FlywheelDirectionRebaseTests
{
    [Fact]
    public void OppositePropagationRebasesSignedStateWithoutChangingStoredEnergy()
    {
        const float speed = 0.3f;
        float energyBefore = speed * speed;

        var rebased = FlywheelDirectionRebase.Rebase(speed, speed, 0.12f, 0.4f);

        Assert.Equal(-speed, rebased.FlywheelSpeed);
        Assert.Equal(-speed, rebased.NetworkSpeed);
        Assert.Equal(-0.12f, rebased.TransferTorque);
        Assert.Equal(energyBefore, rebased.FlywheelSpeed * rebased.FlywheelSpeed);
        Assert.Equal(0f, rebased.FlywheelSpeed - rebased.NetworkSpeed);
        Assert.Equal(GameMath.TWOPI - 0.4f, rebased.AngleRad, 5);
    }

    [Fact]
    public void DirectionFacesAreOnlyRebasedWhenOpposed()
    {
        Assert.True(FlywheelDirectionRebase.IsOpposite(BlockFacing.NORTH, BlockFacing.SOUTH));
        Assert.True(FlywheelDirectionRebase.IsOpposite(BlockFacing.UP, BlockFacing.DOWN));
        Assert.False(FlywheelDirectionRebase.IsOpposite(BlockFacing.NORTH, BlockFacing.EAST));
        Assert.False(FlywheelDirectionRebase.IsOpposite(BlockFacing.NORTH, BlockFacing.NORTH));
    }
}
