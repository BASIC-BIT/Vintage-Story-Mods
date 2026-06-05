namespace DimensionLib.Api;

public sealed class DimensionMapping
{
    private readonly DimensionMappingTransform _transform;

    public DimensionMapping(
        string mappingId,
        string ownerModId,
        string sourceDimensionId,
        string targetDimensionId,
        bool bidirectional = true,
        DimensionMappingTransform transform = null)
    {
        MappingId = mappingId;
        OwnerModId = ownerModId;
        SourceDimensionId = sourceDimensionId;
        TargetDimensionId = targetDimensionId;
        Bidirectional = bidirectional;
        _transform = (transform ?? DimensionMappingTransform.Identity()).Clone();
    }

    public string MappingId { get; }

    public string OwnerModId { get; }

    public string SourceDimensionId { get; }

    public string TargetDimensionId { get; }

    public bool Bidirectional { get; }

    public DimensionMappingTransform Transform => _transform.Clone();
}
