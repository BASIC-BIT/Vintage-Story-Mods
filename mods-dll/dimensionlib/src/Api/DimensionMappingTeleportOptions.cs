namespace DimensionLib.Api;

public sealed class DimensionMappingTeleportOptions
{
    /// <summary>
    /// Destination-local offset applied after the mapping transform. Useful for portals or paired controls that should arrive beside the equivalent point.
    /// </summary>
    public double OffsetX { get; set; }

    public double OffsetY { get; set; }

    public double OffsetZ { get; set; }

    public bool RequireCollisionFreeDestination { get; set; } = true;
}
