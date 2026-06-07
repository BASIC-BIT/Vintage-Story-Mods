namespace DimensionLib.Api;

/// <summary>
/// Affine local-coordinate transform from a source dimension into a target dimension.
/// </summary>
public sealed class DimensionMappingTransform
{
    public double ScaleX { get; set; } = 1;

    public double ScaleY { get; set; } = 1;

    public double ScaleZ { get; set; } = 1;

    public double OffsetX { get; set; }

    public double OffsetY { get; set; }

    public double OffsetZ { get; set; }

    public static DimensionMappingTransform Identity()
    {
        return new DimensionMappingTransform();
    }

    public DimensionMappingTransform Clone()
    {
        return new DimensionMappingTransform
        {
            ScaleX = ScaleX,
            ScaleY = ScaleY,
            ScaleZ = ScaleZ,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            OffsetZ = OffsetZ,
        };
    }
}
