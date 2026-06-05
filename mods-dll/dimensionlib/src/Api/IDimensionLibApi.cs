using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Api;

/// <summary>
/// Server-side entry point exposed by DimensionLib for consuming mods.
/// </summary>
public interface IDimensionLibApi
{
    int PrimaryDimensionPlaneId { get; }

    IReadOnlyCollection<Dimension> Dimensions { get; }

    IReadOnlyCollection<DimensionMapping> Mappings { get; }

    IReadOnlyCollection<string> GeneratorIds { get; }

    DimensionLibResult<Dimension> RegisterDimension(DimensionSpec spec);

    DimensionLibResult<Dimension> GetDimension(string dimensionId);

    DimensionLibResult<Dimension> GetDimensionAt(BlockPos pos);

    DimensionLibResult<DimensionMapping> RegisterMapping(DimensionMappingSpec spec);

    DimensionLibResult<DimensionMapping> GetMapping(string mappingId);

    bool IsDimensionPrepared(string dimensionId);

    bool IsDimensionOrphaned(string dimensionId);

    DimensionLibResult RegisterPolicyProvider(string ownerModId, IDimensionPolicyProvider provider);

    DimensionLibResult RegisterGenerator(IDimensionGenerator generator);

    DimensionLibResult PrepareDimension(string dimensionId, IBlockVolumeSource source = null, IServerPlayer sendToPlayer = null, CancellationToken token = default);

    DimensionLibResult PrepareGeneratedDimension(string dimensionId, IServerPlayer sendToPlayer = null, CancellationToken token = default);

    DimensionLibResult<string> ValidateDimension(string dimensionId);

    DimensionLibResult<DimensionLocation> CaptureLocation(IServerPlayer player);

    DimensionLibResult TeleportToDimension(IServerPlayer player, string dimensionId, DimensionTeleportOptions options = null);

    DimensionLibResult TeleportToLocation(IServerPlayer player, DimensionLocation location);

    DimensionLibResult TeleportAcrossMapping(IServerPlayer player, string mappingId, DimensionMappingTeleportOptions options = null);

    DimensionLibResult ReturnPlayer(IServerPlayer player);

    DimensionLibResult ReleaseDimension(string dimensionId, DimensionReleaseMode mode = DimensionReleaseMode.ForgetOnly);
}
