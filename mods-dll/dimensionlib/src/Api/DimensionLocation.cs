using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace DimensionLib.Api;

/// <summary>
/// Explicit engine location captured for later DimensionLib-aware transfers.
/// </summary>
public sealed class DimensionLocation
{
    public string DimensionId { get; set; }

    public int DimensionPlaneId { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public float Yaw { get; set; }

    public float Pitch { get; set; }

    public float Roll { get; set; }

    public BlockPos AsBlockPos()
    {
        return new BlockPos((int)Math.Floor(X), (int)Math.Floor(Y), (int)Math.Floor(Z), DimensionPlaneId);
    }

    internal static DimensionLocation From(EntityPos pos, string dimensionId = null)
    {
        return new DimensionLocation
        {
            DimensionId = dimensionId,
            DimensionPlaneId = pos.Dimension,
            X = pos.X,
            Y = pos.Y,
            Z = pos.Z,
            Yaw = pos.Yaw,
            Pitch = pos.Pitch,
            Roll = pos.Roll,
        };
    }
}
