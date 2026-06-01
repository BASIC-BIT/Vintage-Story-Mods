using DimensionLib.Api;
using Vintagestory.API.Config;

namespace DimensionLib.Generation;

internal static class BlockVolumeSourceValidator
{
    public static DimensionLibResult ValidateBounds(Dimension dimension, IBlockVolumeSource source)
    {
        if (source.Bounds == null)
        {
            return DimensionLibResult.Fail($"Block source '{source.SourceId}' did not provide bounds.", "missing-source-bounds");
        }

        var expectedSizeX = dimension.ChunkSizeX * GlobalConstants.ChunkSize;
        var expectedSizeZ = dimension.ChunkSizeZ * GlobalConstants.ChunkSize;
        if (source.Bounds.SizeX < expectedSizeX || source.Bounds.SizeZ < expectedSizeZ)
        {
            return DimensionLibResult.Fail($"Block source '{source.SourceId}' bounds {source.Bounds.SizeX}x{source.Bounds.SizeZ} do not cover dimension bounds {expectedSizeX}x{expectedSizeZ}.", "source-bounds-too-small");
        }

        return DimensionLibResult.Ok();
    }
}
