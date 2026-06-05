namespace DimensionLib.Api;

/// <summary>
/// Describes how player-local coordinates in one DimensionLib dimension map to another.
/// </summary>
public sealed class DimensionMappingSpec
{
    public string MappingId { get; set; }

    public string OwnerModId { get; set; }

    public string SourceDimensionId { get; set; }

    public string TargetDimensionId { get; set; }

    public bool Bidirectional { get; set; } = true;

    public DimensionMappingTransform Transform { get; set; } = DimensionMappingTransform.Identity();

    public DimensionMapping ToMapping()
    {
        return new DimensionMapping(
            MappingId,
            OwnerModId,
            SourceDimensionId,
            TargetDimensionId,
            Bidirectional,
            Transform?.Clone());
    }
}
