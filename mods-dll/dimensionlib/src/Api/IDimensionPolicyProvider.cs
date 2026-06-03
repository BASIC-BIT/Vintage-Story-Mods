using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace DimensionLib.Api;

/// <summary>
/// Optional owner-mod policy hook for rules DimensionLib cannot infer from dimension metadata alone.
/// </summary>
public interface IDimensionPolicyProvider
{
    bool CanEnter(IServerPlayer player, Dimension dimension, out string reason);

    bool CanUseBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSelection, out string reason);

    /// <summary>
    /// Returns whether a player may mutate a block in a DimensionLib dimension.
    /// </summary>
    /// <remarks>
    /// Vintage Story exposes a combined can-place-or-break preflight without the requested mutation kind, so DimensionLib uses
    /// <see cref="DimensionBlockMutationKind.Place" /> for that coarse gate and calls this again with
    /// <see cref="DimensionBlockMutationKind.Break" /> from the break event before allowing a break to complete.
    /// </remarks>
    bool CanMutateBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSelection, DimensionBlockMutationKind mutationKind, out string reason);
}
