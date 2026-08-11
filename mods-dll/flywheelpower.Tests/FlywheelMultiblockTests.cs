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
}
