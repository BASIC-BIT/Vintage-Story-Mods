using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// The line's energy store: a mass wound up its guide by any powered tower on the line, and spent in one
/// lump when a cabin departs. One per line.
/// <para>
/// It is a physical block and not a number on the line because the whole appeal of gravity storage is
/// that you can look at it and see how much you have left - the drawn mass IS the gauge. It is also NOT a
/// mechanical-power node: nothing about it touches the network. Powered towers push charge into it, so a
/// tower whose chunk is unloaded simply stops pushing rather than stalling anything.
/// </para>
/// </summary>
public class BETensionWeight : BlockEntity
{
    /// <summary>
    /// How finely the drawn mass and the synced charge are quantised. The mass is chunk mesh, so every
    /// step costs a re-tesselation on every client in range; at 32 steps a maxed windmill dirties this
    /// block about once every eight seconds instead of once a second. The displayed number is rounded, so
    /// the coarseness never shows as a lie - only as a gauge that moves in visible increments.
    /// </summary>
    public const int RenderSteps = 32;

    /// <summary>Blocks of travel currently banked. The one piece of state this block has.</summary>
    public double Charge;

    /// <summary>
    /// The tower this weight is bound to, which is how it finds its line: any line containing that tower is
    /// this weight's line. Persisted, because proximity at placement time is a decision and re-deriving it
    /// every lookup would silently re-home the weight to whatever tower happened to be nearest.
    /// <para>
    /// Re-bound in exactly one case, by <see cref="RopewayLinkService.UnlinkAll"/>: the anchor tower being
    /// REMOVED. It re-binds to one of that tower's own peers, which is the same line minus one tower, so it
    /// cannot re-home the weight to somebody else's ropeway. Without it, breaking one tower left the line
    /// with no store at all and no recovery short of breaking and replacing the weight.
    /// </para>
    /// </summary>
    public BlockPos AnchorTower;

    public double Capacity => Block?.Attributes?["capacity"].AsDouble(RopewayPower.DefaultCapacity) ?? RopewayPower.DefaultCapacity;

    /// <summary>
    /// Centre height of the drawn mass at empty, and how far it climbs to full, in blocks above this
    /// block's bottom face. Both are bounded by the guide rails authored in the shape - see the blocktype's
    /// //massFloor note, which owns that arithmetic.
    /// </summary>
    public double MassFloor => Block?.Attributes?["massFloor"].AsDouble(0.5) ?? 0.5;

    /// <summary>See <see cref="MassFloor"/>.</summary>
    public double MassRise => Block?.Attributes?["massRise"].AsDouble(2.0) ?? 2.0;

    public bool Full => Charge >= Capacity - 1e-6;

    public double Fraction => Capacity <= 0 ? 0 : GameMath.Clamp(Charge / Capacity, 0, 1);

    public RopewayModSystem ModSystem => Api?.ModLoader?.GetModSystem<RopewayModSystem>();

    /// <summary>
    /// The store serving a line, or null when it has none. Scans the loaded weights rather than indexing
    /// by line: <see cref="RopewayLine"/> is rebuilt constantly and an index keyed by it would be one more
    /// thing to invalidate, for a dictionary that holds one entry per built ropeway.
    /// ponytail: O(loaded weights) per lookup, called once a second per powered tower. Index by anchor
    /// tower if a profile ever shows it.
    /// <para>
    /// The LOWEST weight in <see cref="RopewayLine.ComparePos"/> order wins, not the first one enumerated:
    /// two lines that each had a weight can be linked into one, and dictionary order is chunk-load order,
    /// so taking the first match let the live weight swap between sessions and the charge appear to move
    /// between two blocks. Position order is the same before and after any reload.
    /// </para>
    /// </summary>
    public static BETensionWeight StoreOn(RopewayModSystem modSystem, RopewayLine line)
    {
        if (modSystem == null || line == null) return null;

        BETensionWeight best = null;
        foreach (var weight in modSystem.LoadedWeights.Values)
        {
            if (weight?.AnchorTower == null || weight.Pos == null) continue;
            if (line.IndexOf(weight.AnchorTower) < 0) continue;

            if (best == null || RopewayLine.ComparePos(weight.Pos, best.Pos) < 0) best = weight;
        }

        return best;
    }

    /// <summary>The store already bound to a specific tower, for the one-weight-per-line placement rule.</summary>
    public static BETensionWeight StoreAt(RopewayModSystem modSystem, BlockPos tower)
    {
        if (modSystem == null || tower == null) return null;

        foreach (var weight in modSystem.LoadedWeights.Values)
        {
            if (tower.Equals(weight?.AnchorTower)) return weight;
        }

        return null;
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        // Assign rather than TryAdd, exactly as LoadedTowers does: a chunk that reloads builds a NEW block
        // entity at the same position, and keeping the dead one would leave the line winding a store
        // nobody can see and spending from one that is never saved.
        var modSystem = ModSystem;
        if (modSystem != null) modSystem.LoadedWeights[Pos.Copy()] = this;
    }

    public void Bind(BlockPos tower)
    {
        AnchorTower = tower?.Copy();
        MarkDirty();
    }

    /// <summary>
    /// Banks one tower's contribution. Every powered tower on the line calls this against the same
    /// instance, which is the pooling - see <see cref="RopewayPower.Wind"/>.
    /// </summary>
    public void Wind(double trueSpeed, double dt)
    {
        Apply(RopewayPower.Wind(Charge, Capacity, trueSpeed, dt));
    }

    /// <summary>
    /// Pays for a trip up front, or refuses and changes nothing. Refusing is the ONLY way this fails - a
    /// partial payment would be a cabin that left with half a trip's worth of energy, which is the
    /// stranding case the whole store exists to make impossible.
    /// </summary>
    public bool TrySpend(double cost)
    {
        if (!RopewayPower.CanAfford(Charge, cost)) return false;

        Apply(Charge - Math.Max(0, cost));
        return true;
    }

    /// <summary>
    /// Writes a new charge and syncs only when the gauge actually moved a step. <see cref="RenderSteps"/>
    /// explains why this is not an unconditional MarkDirty.
    /// </summary>
    private void Apply(double next)
    {
        var capacity = Capacity;
        next = GameMath.Clamp(next, 0, capacity);
        if (Math.Abs(next - Charge) < 1e-9) return;

        var moved = Step(Charge, capacity) != Step(next, capacity);
        Charge = next;

        // redrawOnClient: the mass is chunk mesh, so without it the gauge sits where it was until
        // something else dirties the block.
        if (moved) MarkDirty(true);
    }

    private static int Step(double charge, double capacity)
    {
        return capacity <= 0 ? 0 : (int)Math.Round(GameMath.Clamp(charge / capacity, 0, 1) * RenderSteps);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        Charge = tree.GetDouble("charge");
        AnchorTower = BEPylonBase.ReadPos(tree, "anchor");

        // The mass is chunk mesh, so a charge that arrives after the chunk has already been tesselated
        // stays at its old height until something else dirties the block. Same idiom as the cable.
        if (Api is ICoreClientAPI) Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetDouble("charge", Charge);
        if (AnchorTower != null) BEPylonBase.WritePos(tree, "anchor", AnchorTower);
    }

    /// <summary>
    /// Draws the raised mass at the height its charge earns. Straight copy of the cable's shape in
    /// <see cref="BEPylonBase.BuildHalfCable"/>, including both of the traps documented there - GetCube
    /// leaves the face count and the colour maps empty, and its UVs have to be flattened or they sample
    /// the neighbouring atlas sprite.
    /// </summary>
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        var replacedDefault = base.OnTesselation(mesher, tessThreadTesselator);
        if (Block == null) return replacedDefault;

        TextureAtlasPosition texPos;
        try
        {
            texPos = tessThreadTesselator.GetTextureSource(Block)?["mass"];
        }
        catch (Exception e)
        {
            // Runs on the tesselation thread - never take the chunk mesher down over a missing texture.
            Api?.Logger.Warning("Ropeway: no mass texture for the tension weight at {0}: {1}", Pos, e.Message);
            return replacedDefault;
        }

        if (texPos == null) return replacedDefault;

        var mesh = BuildMass((float)(MassFloor + MassRise * Fraction), texPos);
        if (mesh != null) mesher.AddMeshData(mesh);

        return replacedDefault;
    }

    /// <summary>
    /// The mass block, centred in the guide at the given height above the block's own bottom. Static and
    /// therefore unit-tested: its failure mode is a gauge that renders nothing at all, silently.
    /// </summary>
    public static MeshData BuildMass(float height, TextureAtlasPosition texPos)
    {
        // Half a block across, so it runs 4..12 in shape units and the two guide rails bracket it exactly.
        const float half = 0.25f;
        const float halfHeight = 0.3125f;

        var mesh = CubeMeshUtil.GetCube(half, halfHeight, half, new Vec3f(-half, -halfHeight, -half));
        if (mesh == null) return null;

        CubeMeshUtil.SetXyzFacesAndPacketNormals(mesh);
        mesh.WithColorMaps();
        mesh.Translate(0.5f, height, 0.5f);

        Array.Fill(mesh.Uv, 0.5f);
        mesh.SetTexPos(texPos);
        return mesh;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        dsc.AppendLine(Lang.Get("ropeway:blockinfo-store", (int)Math.Round(Charge), (int)Math.Round(Capacity)));

        // A weight nothing spends from looks identical to a working one, so it has to say so - otherwise the
        // player stares at a full gauge next to a cabin that refuses. Two ways to end up here: the tower it
        // was built beside is gone, or two lines that each had a weight were linked into one.
        var line = RopewayLine.GetOrBuild(ModSystem, AnchorTower);
        if (line == null) dsc.AppendLine(Lang.Get("ropeway:blockinfo-weight-orphan"));
        else if (StoreOn(ModSystem, line) != this) dsc.AppendLine(Lang.Get("ropeway:blockinfo-weight-spare"));
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
