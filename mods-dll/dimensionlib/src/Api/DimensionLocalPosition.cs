namespace DimensionLib.Api;

public sealed class DimensionLocalPosition
{
    public string DimensionId { get; set; }

    public int DimensionPlaneId { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public int BlockX => (int)System.Math.Floor(X);

    public int BlockY => (int)System.Math.Floor(Y);

    public int BlockZ => (int)System.Math.Floor(Z);
}
