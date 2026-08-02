using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// The sheave at the top of the crossarm. Inert since the controller moved to the ground: it is one cell
/// of the multiblock pattern and nothing else, and it exists as its own block rather than a brace because
/// its throat is the slot the cabin's mast threads through. No block entity, no state, no verbs.
/// <para>
/// Its one job is the line below. A world saved before the footing existed still has one of these sitting
/// at head height with nothing under it - and towers built that way lost their block entity on load, so
/// there is nothing left to say why the ropeway stopped working. This says it, and it says the same
/// useful thing to anyone who places a sheave first and wonders why nothing happened.
/// </para>
/// </summary>
public class BlockPylonHead : Block
{
    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        // The footing is directly below, always: the crossarm's centre cell is the sheave and the offset
        // is purely vertical, so this needs no rotation term.
        var attached = world.BlockAccessor.GetBlockEntity(pos.DownCopy(SpanMath.SheaveHeight)) is BEPylonBase;

        return Lang.Get(attached ? "ropeway:sheave-attached" : "ropeway:sheave-orphan") + "\n"
            + base.GetPlacedBlockInfo(world, pos, forPlayer);
    }
}
