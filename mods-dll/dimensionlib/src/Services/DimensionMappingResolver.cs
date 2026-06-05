using DimensionLib.Api;

namespace DimensionLib.Services;

internal static class DimensionMappingResolver
{
    public static DimensionLocation MapLocalPosition(
        DimensionMappingTransform transform,
        Dimension from,
        Dimension to,
        double x,
        double y,
        double z,
        bool reverse,
        DimensionMappingTeleportOptions options)
    {
        transform ??= DimensionMappingTransform.Identity();
        options ??= new DimensionMappingTeleportOptions();

        var localX = x - from.MinBlockX;
        var localY = y;
        var localZ = z - from.MinBlockZ;
        double mappedLocalX;
        double mappedLocalY;
        double mappedLocalZ;
        if (reverse)
        {
            mappedLocalX = (localX - transform.OffsetX) / transform.ScaleX;
            mappedLocalY = (localY - transform.OffsetY) / transform.ScaleY;
            mappedLocalZ = (localZ - transform.OffsetZ) / transform.ScaleZ;
        }
        else
        {
            mappedLocalX = localX * transform.ScaleX + transform.OffsetX;
            mappedLocalY = localY * transform.ScaleY + transform.OffsetY;
            mappedLocalZ = localZ * transform.ScaleZ + transform.OffsetZ;
        }

        return new DimensionLocation
        {
            DimensionId = to.DimensionId,
            DimensionPlaneId = to.DimensionPlaneId,
            X = to.MinBlockX + mappedLocalX + options.OffsetX,
            Y = mappedLocalY + options.OffsetY,
            Z = to.MinBlockZ + mappedLocalZ + options.OffsetZ,
        };
    }
}
