using Vintagestory.API.MathTools;

namespace FlywheelPower.Tests;

public sealed class FlywheelMultiblockTests
{
    [Fact]
    public void ReservationsMatchRequiresEveryReservedCellToLinkToThePrincipal()
    {
        BlockPos center = new(10, 20, 30, 0);
        BlockPos missing = FlywheelMultiblock.GetPartPositions(center, EnumAxis.Z)[3];

        bool intact = FlywheelMultiblock.ReservationsMatch(
            center,
            EnumAxis.Z,
            partPos => !partPos.Equals(missing));

        Assert.False(intact);
    }

    [Fact]
    public void ReservationsMatchChecksAllEightReservedCells()
    {
        BlockPos center = new(10, 20, 30, 0);
        HashSet<BlockPos> checkedPositions = [];

        bool intact = FlywheelMultiblock.ReservationsMatch(
            center,
            EnumAxis.Y,
            partPos => checkedPositions.Add(partPos.Copy()));

        Assert.True(intact);
        Assert.Equal(8, checkedPositions.Count);
        Assert.DoesNotContain(checkedPositions, partPos => partPos.Equals(center));
    }

    [Theory]
    [InlineData(EnumAxis.X, 10, 21, 31, true)]
    [InlineData(EnumAxis.X, 11, 20, 30, false)]
    [InlineData(EnumAxis.Y, 11, 20, 31, true)]
    [InlineData(EnumAxis.Y, 10, 21, 30, false)]
    [InlineData(EnumAxis.Z, 11, 21, 30, true)]
    [InlineData(EnumAxis.Z, 10, 20, 31, false)]
    public void PartPositionMustBelongToThePrincipalReservationPlane(
        EnumAxis axis,
        int x,
        int y,
        int z,
        bool expected)
    {
        BlockPos center = new(10, 20, 30, 0);

        Assert.Equal(expected, FlywheelMultiblock.IsPartPosition(center, axis, new BlockPos(x, y, z, 0)));
    }
}
