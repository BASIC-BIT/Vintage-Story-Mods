namespace DimensionLib.Api;

public sealed class DimensionTeleportOptions
{
    public bool RecordReturn { get; set; } = true;

    public bool ForceSendDimension { get; set; } = true;

    public double? X { get; set; }

    public double? Y { get; set; }

    public double? Z { get; set; }

    public float? Yaw { get; set; }

    public float? Pitch { get; set; }

    public float? Roll { get; set; }
}
