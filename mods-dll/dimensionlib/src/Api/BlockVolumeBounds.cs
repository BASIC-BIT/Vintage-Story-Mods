namespace DimensionLib.Api;

public sealed class BlockVolumeBounds
{
    public BlockVolumeBounds(int sizeX, int sizeY, int sizeZ)
    {
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
    }

    public int SizeX { get; }

    public int SizeY { get; }

    public int SizeZ { get; }
}
