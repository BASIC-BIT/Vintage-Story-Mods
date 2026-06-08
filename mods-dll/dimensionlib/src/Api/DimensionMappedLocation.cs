namespace DimensionLib.Api;

public sealed class DimensionMappedLocation
{
    public string MappingId { get; set; }

    public string SourceDimensionId { get; set; }

    public string TargetDimensionId { get; set; }

    public bool Reverse { get; set; }

    public DimensionLocation Location { get; set; }
}
