using Vintagestory.API.MathTools;

namespace FlywheelPower;

internal static class FlywheelDirectionRebase
{
    internal static bool IsOpposite(BlockFacing previous, BlockFacing next)
    {
        return previous != null && next != null && previous == next.Opposite;
    }

    internal static (float FlywheelSpeed, float NetworkSpeed, float TransferTorque, float AngleRad) Rebase(
        float flywheelSpeed,
        float networkSpeed,
        float transferTorque,
        float angleRad)
    {
        return (
            -flywheelSpeed,
            -networkSpeed,
            -transferTorque,
            MirrorAngle(angleRad));
    }

    internal static float MirrorAngle(float angleRad)
    {
        float normalized = GameMath.Mod(angleRad, GameMath.TWOPI);
        return GameMath.Mod(GameMath.TWOPI - normalized, GameMath.TWOPI);
    }
}
