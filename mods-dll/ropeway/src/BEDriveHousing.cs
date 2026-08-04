using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Ropeway;

/// <summary>
/// The drive, standing beside the line rather than on it: it carries the <c>MPConsumer</c>, it declares the
/// haul load, and the line's speed pools over these. Everything the bullwheel used to do on the network,
/// minus the four-block climb up the tower.
/// <para>
/// Which line it serves is asked at LOOKUP time from the nearest footing ON A LINE within its own
/// <see cref="BlockDriveHousing.TowerRadius"/> - the tension weight's pattern, reused rather than
/// reinvented - so there is no binding to persist, orphan or repair.
/// </para>
/// </summary>
public class BEDriveHousing : BlockEntity
{
    /// <summary>Vanilla registers <c>MPConsumer</c> itself, so the mod registers nothing for it.</summary>
    private BEBehaviorMPConsumer mpc;

    public RopewayModSystem ModSystem => Api?.ModLoader?.GetModSystem<RopewayModSystem>();

    public MechanicalNetwork Network => mpc?.Network;

    /// <summary>What this housing's own axle is turning at, or 0 with no behaviour, no axle or no network.</summary>
    public double DriveSpeed => mpc?.TrueSpeed ?? 0;

    private double TowerRadius => (Block as BlockDriveHousing)?.TowerRadius ?? 0;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        mpc = GetBehavior<BEBehaviorMPConsumer>();

        // Assign rather than TryAdd, exactly as LoadedTowers and LoadedWeights do: a chunk that reloads
        // builds a NEW block entity at the same position, and keeping the dead one would drive a line
        // through a block nobody can see.
        var modSystem = ModSystem;
        if (modSystem != null) modSystem.LoadedHousings[Pos.Copy()] = this;

        if (api.Side == EnumAppSide.Server) RegisterGameTickListener(DeclareLoad, 1000, 0);
    }

    /// <summary>
    /// The one tower this housing answers to: the nearest loaded footing inside its own radius that is on a
    /// line, or null out in a field and beside an unlinked footing alike - which is why
    /// <c>ropeway:housing-orphan</c> talks about lines and not about towers. EVERY question about which line
    /// the housing belongs to goes through here, and that is the point of it being one accessor rather than
    /// a call at each site.
    /// <para>
    /// It used to be two different questions. <see cref="Serves"/> asked "is there any tower of this line in
    /// range", which is true of EVERY line with a tower nearby, while the load declaration asked for the
    /// single nearest footing overall - so two lines built within eight blocks of one housing both saw its
    /// full speed and only one of them was ever charged for it. One mill hauling two cabins for the price of
    /// one is exactly the free speed <see cref="RopewayPower.PoolSpeed"/> exists to refuse.
    /// <see cref="BlockTensionWeight.NearAnyTower"/> stays - it is still the right question for the
    /// tensioner, which certifies a line rather than driving it.
    /// </para>
    /// <para>
    /// Nearest footing that is ON A LINE, not nearest footing. <c>LoadedTowers</c> takes every footing at
    /// <c>Initialize</c>, before any completeness check and whether or not it carries a span, so a bare one
    /// dropped while scouting the next tower position counts - and one of those landed a few blocks nearer
    /// than the real line's footing would silently take this housing off its line. <c>GetOrBuild</c> is null
    /// below two towers, which is exactly the test, and the answer stays "a drive serves exactly one line".
    /// </para>
    /// ponytail: O(loaded towers) with a chain walk on each stray candidate, per call, and this is asked once
    /// per loaded housing per cabin tick. Both tables are small; index housings by line if a profile says so.
    /// </summary>
    private BlockPos ServingTower
    {
        get
        {
            // Resolved once rather than inside the predicate: ModSystem is a ModLoader lookup and the
            // predicate runs per candidate.
            var modSystem = ModSystem;
            return BlockTensionWeight.NearestTower(modSystem, Pos, TowerRadius,
                tower => RopewayLine.GetOrBuild(modSystem, tower) != null);
        }
    }

    /// <summary>Whether the one tower this housing answers to is on <paramref name="line"/>.</summary>
    public bool Serves(RopewayLine line)
    {
        return line?.IndexOf(ServingTower) >= 0;
    }

    /// <summary>
    /// The load this housing puts on its network: the haul rope is a real mechanical load, so a housing
    /// whose line has a cabin trying to move declares one, and every other one idles. Read from
    /// <see cref="EntityRopewayCabin.IsHauling"/> - the cabin TRYING to move - and never from whether it is
    /// actually moving: the load is what slows the network, so keying it on real motion would drop it the
    /// instant a weak mill stalled, speed the network up, start the cabin and stall it again a tick later.
    /// <para>
    /// EVERY housing on the line declares the SAME load rather than a share of it, because a share would
    /// have to be divided by how many others are powered - a number that changes when somebody walks away
    /// and a chunk unloads. Each drive pulls its own weight and its speed adds.
    /// </para>
    /// ponytail: no clamp on GearedRatio. Gearing multiplies both this resistance and the speed the housing
    /// reads, so an over-geared rig stalls its own network exactly as an over-geared quern does.
    /// </summary>
    private void DeclareLoad(float dt)
    {
        // Nothing reads Resistance off a housing that is on no network, and FindOn below is a scan of every
        // loaded entity - so an unpowered housing pays nothing for this.
        if (mpc?.Network == null) return;

        var tower = ServingTower;
        var line = tower == null ? null : RopewayLine.GetOrBuild(ModSystem, tower);
        var cabin = line == null ? null : EntityRopewayCabin.FindOn(Api.World, line);

        mpc.Resistance = RopewayPower.Resistance(cabin?.IsHauling == true, cabin?.ClimbOn(line) ?? 0, 0);
    }

    /// <summary>
    /// Deliberately discarding what base returns, and this is load bearing rather than sloppy:
    /// <c>BEBehaviorMPConsumer.OnTesselation</c> returns TRUE for any non-null block, because a vanilla
    /// consumer expects the instanced <c>MechNetworkRenderer</c> to draw it instead. With
    /// <c>mechPartShape</c> null nothing draws it, so honouring that true would make the housing vanish.
    /// </summary>
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        base.OnTesselation(mesher, tessThreadTesselator);
        return false;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        // Naming the tower rather than saying "the line beside it". With two lines passing within eight
        // blocks this housing drives exactly one of them, and "the line beside it" is precisely the sentence
        // a player who built it for the other one reads while nothing moves. DisplayName is what every other
        // tower message in the mod uses, so the label here is the one they saw when they linked the span.
        var tower = ServingTower;
        if (tower == null)
        {
            dsc.AppendLine(Lang.Get("ropeway:housing-orphan"));
        }
        else
        {
            ModSystem.LoadedTowers.TryGetValue(tower, out var be);
            dsc.AppendLine(Lang.Get("ropeway:housing-what",
                RopewayLinkService.DisplayName(be, tower.X - Pos.X, tower.Z - Pos.Z)));
        }

        dsc.AppendLine(mpc?.Network == null
            ? Lang.Get("ropeway:blockinfo-nodrive")
            : Lang.Get("ropeway:blockinfo-drive", Math.Round(mpc.TrueSpeed, 2)));
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        ModSystem?.LoadedHousings.Remove(Pos);
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        ModSystem?.LoadedHousings.Remove(Pos);
    }
}
