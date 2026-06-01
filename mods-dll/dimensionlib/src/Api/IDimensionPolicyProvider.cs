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

    bool CanMutateBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSelection, DimensionBlockMutationKind mutationKind, out string reason);
}
