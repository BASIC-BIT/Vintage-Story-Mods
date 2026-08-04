using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Ropeway;

/// <summary>
/// The line's rope tensioner: a mass hanging on the haul rope to keep it taut. It stores NOTHING - no
/// charge, no capacity, no gauge - and its drawn mass is authored in the shape at the height a tensioner
/// hangs at rather than at a height that means a number.
/// <para>
/// It was a gravity battery and is not one any more, because the arithmetic never closed: 0.156 m3 of
/// granite raised 2 m holds 8.3 kJ against roughly 45 kJ for 400 blocks of level travel and 224 kJ for a
/// 40 m climb - short by 5x and 27x. Gravity storage returns as its own block, correctly sized; see
/// <c>docs/POWER-AND-STORAGE.md</c>.
/// </para>
/// <para>
/// All this block entity does is put itself on the map so <see cref="OnLine"/> can answer the one question
/// the tensioner is asked. It is deliberately NOT a mechanical power node: the towers are the consumers.
/// </para>
/// </summary>
public class BETensionWeight : BlockEntity
{
    public RopewayModSystem ModSystem => Api?.ModLoader?.GetModSystem<RopewayModSystem>();

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        // Assign rather than TryAdd, exactly as LoadedTowers does: a chunk that reloads builds a NEW block
        // entity at the same position, and keeping the dead one would answer for a block nobody can see.
        var modSystem = ModSystem;
        if (modSystem != null) modSystem.LoadedWeights[Pos.Copy()] = this;
    }

    /// <summary>
    /// Whether a line has its tensioner: any loaded weight standing within its own <c>towerRadius</c> of any
    /// tower on the line. Proximity at LOOKUP time rather than a binding persisted at placement, which is
    /// what deletes the whole orphan/re-bind/spare family - break the tower a weight was built beside and it
    /// simply serves whichever tower is still in range, with nothing to repair.
    /// <para>
    /// A weight in an unloaded chunk cannot be seen and reads as missing, and the line-length cap does not
    /// rule that out the way this used to claim: <c>MaxChunkRadius</c> 384 is a cap and not the loaded
    /// radius, which is <c>min(MaxChunkRadius, ceil(Viewdistance/32))</c> for a network client - singleplayer
    /// skips the cap - and comes to 256 blocks at the shipped view distance either way (see
    /// <see cref="BEPylonBase.DriveSpeedOn"/> for the whole of it). A tensioner
    /// beyond that window on a 320-block line refuses a cabin placement at the other end. Unlike the drive
    /// there is no exemption for it, and it wants none - hanging a cabin is a one-off act the player can
    /// simply do from the end the tensioner is at, not a control taken away mid-ride.
    /// </para>
    /// ponytail: O(weights x towers), both small, and only asked on a cabin placement or a block-info
    /// refresh. Index by line if a profile ever shows it.
    /// </summary>
    public static bool OnLine(RopewayModSystem modSystem, RopewayLine line)
    {
        if (modSystem == null || line?.Towers == null) return false;

        foreach (var weight in modSystem.LoadedWeights.Values)
        {
            if (weight?.Pos == null) continue;
            var radius = (weight.Block as BlockTensionWeight)?.TowerRadius ?? 0;

            if (BlockTensionWeight.NearAnyTower(weight.Pos, line, radius)) return true;
        }

        return false;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        dsc.AppendLine(Lang.Get("ropeway:blockinfo-tensioner-what"));
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        ModSystem?.LoadedWeights.Remove(Pos);
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        ModSystem?.LoadedWeights.Remove(Pos);
    }
}
