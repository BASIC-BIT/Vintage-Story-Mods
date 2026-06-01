namespace DimensionLib.Api;

/// <summary>
/// Creates a block source for a DimensionLib dimension without coupling DimensionLib to the generator's storage or rules.
/// </summary>
public interface IDimensionGenerator
{
    string GeneratorId { get; }

    IBlockVolumeSource CreateSource(Dimension dimension);
}
