using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Ropeway;

/// <summary>
/// Per-tower state: the multiblock validation tick and the 0-2 spans this tower carries.
/// Modelled on BlockEntityBeeHiveKiln - Initialize loads the structure, Init sets up the tick and the
/// rotation, FromTreeAttributes re-Inits on the client.
/// <para>
/// This lives on the GROUND-PLACED footing, which is the tower's one canonical position: LoadedTowers,
/// LineCache, RopewayLine.Towers, every persisted span and the cabin's LineKey are all footing positions.
/// The sheave up on the crossarm is an inert cell of the pattern, and <see cref="SpanMath.AnchorOf"/> is
/// the only thing that turns a footing position into the height the rope actually runs at.
/// </para>
/// </summary>
public class BEPylonBase : BlockEntity
{
    public const int MaxSpansPerTower = 2;

    /// <summary>Longest name a tower keeps. A place name, not a paragraph - it has to fit a picker row.</summary>
    public const int MaxNameLength = 24;

    public bool StructureComplete;

    /// <summary>Player-set label, or null when unnamed. Sanitised on the way in, synced with the tree.</summary>
    public string TowerName;

    /// <summary>Peer towers this one is linked to. Unordered, 0-2 entries, mirrored on the peer.</summary>
    public readonly List<BlockPos> Spans = new();

    private MultiblockStructure structure;
    private MultiblockStructure highlightedStructure;
    private IPlayer highlightFor;
    private string side;

    /// <summary>The structure's block number for the intake cell, or 0 on a footing that names none.</summary>
    private int intakeNumber;

    public double MaxSpan => Block?.Attributes?["maxSpan"].AsDouble(48) ?? 48;

    public double MaxLineLength => Block?.Attributes?["maxLineLength"].AsDouble(512) ?? 512;

    public int MaxCandidates => Block?.Attributes?["maxCandidates"].AsInt(16) ?? 16;

    public double RopePerBlock => Block?.Attributes?["ropePerBlock"].AsDouble(0.25) ?? 0.25;

    /// <summary>
    /// The axis the cabin threads through the frame on, as one of its two facings. The crossarm is the
    /// structure's +/-X offsets and the frame is one block deep, so the passage runs along the tower's
    /// local Z - which the side variant names directly. With the rear gantry gone this is the only thing
    /// orientation still decides, and getting it 90 degrees out is a tower the line cannot pass through.
    /// </summary>
    public BlockFacing PassageFacing => BlockFacing.FromCode(side ?? "north") ?? BlockFacing.NORTH;

    /// <summary>A tower is an END tower iff it is complete and carries exactly one span.</summary>
    public bool IsEndpoint => StructureComplete && Spans.Count == 1;

    /// <summary>
    /// Whether this footing's own structure wants a bullwheel in the middle of its crossarm rather than a
    /// pylon head - which is exactly what makes it a station. Read once in <see cref="Initialize"/> off the
    /// blocktype, because <see cref="OnTesselation"/> runs on the tesselation thread and must not go near
    /// the world; <c>AStationWearsTheBullwheelAndAPlainTowerWearsTheHead</c> pins the relationship both ways,
    /// so this cannot quietly start meaning something else.
    /// </summary>
    public bool WearsABullwheel { get; private set; }

    /// <summary>
    /// The horizontal unit vector pointing AWAY from the line at a tower carrying exactly one span, and null
    /// at every other tower. The one side of a terminal nothing ever passes: the cabin's travel is clamped
    /// to the anchors, so past the last one there is only the platform.
    /// <para>
    /// <see cref="Spans"/>.Count and deliberately not <see cref="IsEndpoint"/>. Gating on
    /// <see cref="StructureComplete"/> would make the bullwheel jump a block sideways the moment somebody
    /// broke a brace, and put it back when they replaced it.
    /// </para>
    /// </summary>
    public Vec3d DeadSide
    {
        get
        {
            if (Spans.Count != 1 || Pos == null) return null;

            var peer = Spans[0];
            if (peer == null) return null;

            var dx = Pos.X - peer.X;
            var dz = Pos.Z - peer.Z;
            var plan = Math.Sqrt(dx * dx + dz * dz);
            return plan < 1e-9 ? null : new Vec3d(dx / plan, 0, dz / plan);
        }
    }

    /// <summary>
    /// Whether this footing is the tension-station kind, read once out of its own block's attributes.
    /// <para>
    /// This one flag replaced the whole tensioner subsystem. The rule used to be "any loaded weight standing
    /// within its own eight-block radius of any tower on the line", which needed a position table kept in
    /// step with chunk loads, a squared-distance helper with a dimension term, and a block entity on the
    /// weight whose only job was to put itself in that table. A tensioner is now a CELL of a station, so the
    /// question "which line does this weight tension" has one answer by construction and is asked by walking
    /// the tower list the caller is already holding.
    /// </para>
    /// <para>
    /// Publicly settable so <see cref="HasTensioner"/> can be tested over a fake tower list: a BEPylonBase
    /// built without a world has no Block to read this out of. Nothing in the mod writes it.
    /// </para>
    /// </summary>
    public bool IsTensioner { get; set; }

    public RopewayModSystem ModSystem => Api?.ModLoader?.GetModSystem<RopewayModSystem>();

    /// <summary>
    /// The block entity in the cell this footing's own structure names as its mechanical intake, or null on
    /// any footing that names none - which is every plain tower and every tension station.
    /// <para>
    /// EXACT, not near. <c>TransformedOffsets</c> is already rotated for this tower's facing, so the intake
    /// is one block-accessor call at a known offset rather than a scan of every loaded footing with a radius,
    /// an acceptance predicate and a positional tie-break. The cell is found by BLOCK NUMBER out of the
    /// station's own <c>driveIntakeCell</c> attribute rather than by a hardcoded offset, so moving the leg in
    /// JSON moves the lookup with it.
    /// </para>
    /// </summary>
    private BEDriveHousing Intake
    {
        get
        {
            var offsets = structure?.TransformedOffsets;
            if (intakeNumber == 0 || offsets == null || Api == null) return null;

            for (var i = 0; i < offsets.Count; i++)
            {
                if (offsets[i].W != intakeNumber) continue;

                return Api.World.BlockAccessor.GetBlockEntity(
                    Pos.AddCopy(offsets[i].X, offsets[i].Y, offsets[i].Z)) as BEDriveHousing;
            }

            return null;
        }
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        // AsObject<T> on a missing key yields null - a variant that forgot the attribute must not NRE.
        structure = Block?.Attributes?["multiblockStructure"]?.AsObject<MultiblockStructure>();
        side ??= Block?.Variant["side"];

        // All read once: a station's kind is a property of its blocktype and cannot change under it.
        IsTensioner = Block?.Attributes?["tensioner"].AsBool() ?? false;
        intakeNumber = Block?.Attributes?["driveIntakeCell"].AsInt() ?? 0;
        WearsABullwheel = WantsABullwheel(structure);

        // BEFORE Init, which is where InitForUse builds BlockCodes out of BlockNumbers.
        OwnTheHeadCell(structure, side);

        if (structure != null && side != null) Init();

        var modSystem = ModSystem;
        if (modSystem == null) return;

        modSystem.LoadedTowers[Pos.Copy()] = this;

        // A line resolved while this tower's chunk was unloaded is a truncated line. Drop it.
        modSystem.InvalidateLine(Pos);
        modSystem.LineCache.Remove(Pos);
        foreach (var peer in Spans)
        {
            modSystem.InvalidateLine(peer);
            modSystem.LineCache.Remove(peer);
        }
    }

    /// <summary>
    /// Whether a footing's structure names a bullwheel for the centre of its crossarm. The RAW offsets, not
    /// the transformed ones: a rotation about Y carries the centre column onto itself, so this answers the
    /// same before <c>InitForUse</c> as after, and it is a question about the blocktype rather than about
    /// where the block happens to be facing. Static, so a fake structure can ask it. Null-tolerant, because
    /// a variant that forgot the attribute must not NRE - it simply wears no wheel.
    /// </summary>
    public static bool WantsABullwheel(MultiblockStructure structure)
    {
        var offsets = structure?.Offsets;
        if (offsets == null) return false;

        foreach (var offset in offsets)
        {
            if (offset.X != 0 || offset.Z != 0 || offset.Y != SpanMath.SheaveHeight) continue;

            foreach (var pair in structure.BlockNumbers)
            {
                if (pair.Value == offset.W) return pair.Key?.Path?.StartsWith("bullwheel") == true;
            }
        }

        return false;
    }

    /// <summary>
    /// The two crossarm-end cells that carry a facing - <c>drivehead</c> and <c>tensionhead</c> - narrowed
    /// in this footing's OWN copy of the structure from the <c>-*</c> wildcard to the footing's own side.
    /// That is the whole of the shared-leg fix, and it is five lines because
    /// <see cref="MultiblockStructure.BlockNumbers"/> is a public dictionary on a per-block-entity
    /// <c>AsObject</c> copy and <c>InitForUse</c> builds <c>BlockCodes</c> out of it - so this has to run
    /// BEFORE <see cref="Init"/> and nothing else has to change.
    /// <para>
    /// WHY it is needed. <c>MultiblockStructure</c> has no notion of ownership: <c>InCompleteBlockCount</c>
    /// asks only whether the block at each offset matches a wildcard, and nothing anywhere asks whether some
    /// other footing is already claiming that cell. Derived off the shipped offsets, a
    /// <c>drivestation-north</c> at the origin shares its ENTIRE machine leg - housing, three shafts and the
    /// head - with a <c>drivestation-east</c> 4.243 blocks away at (3, 0, -3), with a <c>drivestation-west</c>
    /// at (3, 0, +3) and with a <c>drivestation-south</c> at (6, 0, 0). Both structures validated, so
    /// <see cref="DriveSpeedOn"/> resolved the SAME consumer from both lines and ran both at full speed while
    /// <see cref="DeclareLoad"/> wrote <c>Resistance</c> onto it from both footings on a 1 s tick and the last
    /// writer won - free speed AND unpaid load, which is what <c>RopewayPower.PoolSpeed</c>'s own comment
    /// calls the one thing a load model must never do. The head is the only facing-carrying cell of a shared
    /// leg, so narrowing it is enough: a shared head can face one way, so it can satisfy one station.
    /// </para>
    /// <para>
    /// WHY only these two, when M4's looseness covers five blocks. The refusal M4 defers is the one that
    /// would bite <c>pylonhead</c> and <c>bullwheel</c>: those are symmetric along the rope axis, so a player
    /// who placed one from the other side of the tower has a geometrically identical block that would stop
    /// validating, and an incomplete tower is un-clickable - a saved world would lose its picker, its call
    /// and its rename over a block that looks perfectly right. Neither applies here.
    /// <c>ropeway:drivehead</c> and <c>ropeway:tensionhead</c> are NEW and untracked, so no saved world can
    /// hold a wrongly-faced one, and both are visibly asymmetric - <c>drivehead.shaftwest</c> is x 0..4 and
    /// <c>tensionhead</c>'s tie rod is x 0..12 against a sheave at x 8..16 - so a wrong facing is self-evident
    /// to the player rather than invisible. The other three keep the wildcard until M4's placement half lands.
    /// </para>
    /// <para>
    /// Static and null-tolerant so the suite can run the tie-break enumeration over a structure of its own -
    /// <c>ASharedMachineLegSatisfiesAtMostOneStation</c>. A footing whose structure names no head cell (every
    /// plain tower) is left exactly as it was.
    /// </para>
    /// </summary>
    public static void OwnTheHeadCell(MultiblockStructure structure, string side)
    {
        if (structure?.BlockNumbers == null || side == null) return;

        // Two-argument AssetLocation, so the domain is not re-parsed out of a string on every tower - and so
        // these read as BLOCK codes rather than as the lang keys EveryLangKeyTheCodeAsksForIsShipped greps
        // "ropeway:..." literals for.
        foreach (var head in new[] { "drivehead", "tensionhead" })
        {
            var wildcard = new AssetLocation("ropeway", head + "-*");
            if (!structure.BlockNumbers.TryGetValue(wildcard, out var number)) continue;

            structure.BlockNumbers.Remove(wildcard);
            structure.BlockNumbers[new AssetLocation("ropeway", head + "-" + side)] = number;
        }
    }

    /// <summary>
    /// Degrees the cell list is rotated by for a <c>side</c> variant, matching every station blocktype's own
    /// <c>shapeByType</c>. Public so the suite's tie-break enumeration runs the same map the engine is handed
    /// rather than a copy of it.
    /// </summary>
    public static int RotationFor(string side)
    {
        return side switch
        {
            "east" => 270,
            "south" => 180,
            "west" => 90,
            _ => 0
        };
    }

    private void Init()
    {
        if (Api.Side == EnumAppSide.Server)
        {
            RegisterGameTickListener(OnServerTick1s, 1000, 0);
        }
        else
        {
            RegisterGameTickListener(OnClientTick500ms, 500, 0);
        }

        structure.InitForUse(RotationFor(side));
    }

    private void OnServerTick1s(float dt)
    {
        Validate();
        DeclareLoad();
    }

    /// <summary>
    /// The load this station's intake puts on its network: the haul rope is a real mechanical load, so a
    /// station whose line has a cabin trying to move declares one, and every other one idles. Read from
    /// <see cref="EntityRopewayCabin.IsHauling"/> - the cabin TRYING to move - and never from whether it is
    /// actually moving: the load is what slows the network, so keying it on real motion would drop it the
    /// instant a weak mill stalled, speed the network up, start the cabin and stall it again a tick later.
    /// <para>
    /// EVERY drive station on the line declares the SAME load rather than a share of it, because a share
    /// would have to be divided by how many others are powered - a number that changes when somebody walks
    /// away and a chunk unloads. Each drive pulls its own weight and its speed adds.
    /// </para>
    /// <para>
    /// This is the tower's own 1 s tick and not a listener of the housing's, which is a deletion rather than
    /// a move: the housing had a <c>RegisterGameTickListener</c> purely because it had to re-answer "which
    /// line am I on" every second. The station already knows. The early return keeps it free on the plain
    /// towers, which is all but one or two per line - <see cref="Intake"/> is a field compare on a footing
    /// that names no intake cell, and <c>FindOn</c>'s scan of loaded entities never runs there.
    /// </para>
    /// ponytail: no clamp on GearedRatio. Gearing multiplies both this resistance and the speed the intake
    /// reads, so an over-geared rig stalls its own network exactly as an over-geared quern does.
    /// </summary>
    private void DeclareLoad()
    {
        var consumer = Intake?.Consumer;
        if (consumer?.Network == null) return;

        var line = RopewayLine.GetOrBuild(ModSystem, Pos);
        var cabin = line == null ? null : EntityRopewayCabin.FindOn(Api.World, line);

        consumer.Resistance = RopewayPower.Resistance(cabin?.IsHauling == true, cabin?.ClimbOn(line) ?? 0, 0);
    }

    /// <summary>
    /// The line's drive speed: every DRIVE STATION on the chain, added ONCE PER NETWORK. THAT is the pooling -
    /// a line may carry more than one drive station and separate drives add up - and it needs no coordination
    /// because addition does not care what order it happens in or whether one of the terms went away. Several
    /// stations tapped off one axle run are one drive, not several; <see cref="RopewayPower.PoolSpeed"/> is
    /// why, and that dedupe is unchanged.
    /// <para>
    /// A WALK of the line's own towers, which is the whole of this change. It used to be a scan of a table of
    /// every loaded housing, asking each one which footing was nearest it and whether that footing was on
    /// this line - because a housing bound by proximity has no tower to be indexed under. A housing is now a
    /// cell of exactly one station, so the question is answered by the structure and the table is gone.
    /// </para>
    /// <para>
    /// The cabin reads this LIVE, which the store design forbade, and the line-length cap does NOT make that
    /// safe the way this used to claim. <c>MaxChunkRadius</c> 384 is a cap on the loaded radius rather than
    /// the radius: <c>ServerMain.GetAllowedChunkRadius</c> is <c>min(MaxChunkRadius, ceil(Viewdistance/32))</c>
    /// for a network client - singleplayer skips the cap and returns the raw <c>ceil(Viewdistance/32)</c>,
    /// which only diverges above a 384-block view distance - and the shipped client default of 256 makes both
    /// 8 chunks = <b>256 blocks</b>. A 320-block line is buildable at that view distance and outruns the
    /// window by 64 blocks, so a station beside the far end really can be dark while a rider is aboard, and
    /// it reads as 0 here.
    /// </para>
    /// <para>
    /// Nothing is corrupted by that - the cabin slows or stops and picks up again when the chunk lands - but
    /// 0 must not be reported to the player as "you have no drive". A dark TOWER is necessarily an end of the
    /// walked chain (<see cref="RopewayLine.WalkChain"/>) and so sets <c>Truncated</c>, which is the state
    /// <see cref="EntityRopewayCabin.MayStart"/> exempts. The intake is a cell of the tower rather than a
    /// block up to eight blocks away from it, so the residual band this used to carry narrowed from eight
    /// blocks to three - but it did not close, because a chunk boundary can still fall between a footing and
    /// its own leg. Accepted: closing it means a second walk over unloaded chunks, which costs more than the
    /// message does.
    /// </para>
    /// </summary>
    public static double DriveSpeedOn(RopewayModSystem modSystem, RopewayLine line)
    {
        if (modSystem == null || line?.Towers == null) return 0;

        var drives = new List<(long, double)>(line.Towers.Length);
        foreach (var tower in line.Towers)
        {
            if (!modSystem.LoadedTowers.TryGetValue(tower, out var station)) continue;

            var consumer = station?.Intake?.Consumer;
            if (consumer?.Network == null) continue;

            drives.Add((consumer.Network.networkId, consumer.TrueSpeed));
        }

        return RopewayPower.PoolSpeed(drives);
    }

    /// <summary>
    /// Whether a line has its tensioner: any tower ON THE CHAIN that is a completed tension station. Pure
    /// apart from the tower table, and therefore unit-tested.
    /// <para>
    /// <see cref="StructureComplete"/> gates it, which the proximity rule could not: a half-built tension
    /// station is not a tensioner, and the tower's own overlay says which cells are missing. That is a better
    /// failure than a block standing in a field being silently out of range.
    /// </para>
    /// <para>
    /// A tower in an unloaded chunk still reads as absent, and "no tensioner" does NOT coincide exactly with
    /// <c>line.Truncated</c> - an earlier version of this comment claimed it did, and it is the same band
    /// <see cref="DriveSpeedOn"/> is honest about sixty lines up. The tower table is only half the question:
    /// <see cref="StructureComplete"/> is fifteen <c>GetBlockRaw</c> reads and
    /// <c>BlockAccessorRelaxed.GetBlockId</c> returns 0 - air - for an unloaded chunk, so a LOADED footing
    /// whose own leg is three blocks away across a chunk boundary that is not loaded reads incomplete while
    /// <c>MarkLoadedEnds</c>, which only inspects the two ends of the walked chain, sees nothing wrong. The
    /// band narrowed from eight blocks to three when the weight became a cell of the tower. It did not close.
    /// </para>
    /// <para>
    /// So no caller may treat a false from here as proof that the player has not built one.
    /// <c>RopewayLinkService.TryPlaceCabin</c> asks <c>line.Truncated</c> BEFORE this and says "part of that
    /// line is not loaded" there, which is the honest answer whenever it applies; what is left underneath is
    /// the three-block residue, where the message is wrong and cheap to be wrong - the player is standing at
    /// the tower and its own overlay names the missing cell.
    /// </para>
    /// </summary>
    public static bool HasTensioner(RopewayModSystem modSystem, RopewayLine line)
    {
        if (modSystem == null || line?.Towers == null) return false;

        foreach (var tower in line.Towers)
        {
            if (modSystem.LoadedTowers.TryGetValue(tower, out var station)
                && station is { IsTensioner: true, StructureComplete: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The overlay is the primary build-guidance channel, so it has to follow the blocks the player is
    /// placing - a one-shot snapshot leaves ghost cells glowing on top of blocks that are already there.
    /// HighlightBlocks replaces the whole slot, so re-issuing it is the entire update. Idle until someone
    /// has actually asked for highlights on this tower.
    /// </summary>
    private void OnClientTick500ms(float dt)
    {
        if (highlightFor == null) return;

        if (IncompleteCount() == 0)
        {
            ClearHighlights(highlightFor);
            return;
        }

        Highlight(highlightFor);
    }

    /// <summary>Re-runs the multiblock check and syncs the result if it changed. Server side.</summary>
    public bool Validate()
    {
        if (structure?.TransformedOffsets == null) return StructureComplete;

        var before = StructureComplete;
        StructureComplete = structure.InCompleteBlockCount(Api.World, Pos) == 0;
        if (before != StructureComplete) MarkDirty();
        return StructureComplete;
    }

    /// <summary>How many cells are missing or wrong. -1 when the structure attribute is absent.</summary>
    public int IncompleteCount()
    {
        if (structure?.TransformedOffsets == null) return -1;
        return structure.InCompleteBlockCount(Api.World, Pos);
    }

    /// <summary>
    /// Client-only build guidance: the wanted block's own colour for missing cells, red for wrong ones.
    /// HighlightIncompleteParts dereferences <c>world.Api as ICoreClientAPI</c>, so it NREs server side.
    /// </summary>
    public void ShowIncompleteParts(IPlayer byPlayer)
    {
        if (Api is not ICoreClientAPI || byPlayer == null || structure?.TransformedOffsets == null) return;

        highlightFor = byPlayer;
        Highlight(byPlayer);
    }

    private void Highlight(IPlayer byPlayer)
    {
        highlightedStructure = structure;
        try
        {
            highlightedStructure.HighlightIncompleteParts(Api.World, byPlayer, Pos);
        }
        catch (Exception e)
        {
            // HighlightIncompleteParts indexes SearchBlocks(wildcard)[0] on every missing cell, so a
            // blockNumbers key that resolves to nothing is an IndexOutOfRangeException. Never crash the
            // client over build guidance.
            highlightFor = null;
            Api.Logger.Error("Ropeway: could not highlight tower at {0}: {1}", Pos, e.Message);
        }
    }

    public void ClearHighlights(IPlayer byPlayer)
    {
        highlightFor = null;
        if (Api is ICoreClientAPI && byPlayer != null) highlightedStructure?.ClearHighlights(Api.World, byPlayer);
    }

    /// <summary>
    /// Half-thickness of the drawn cable, in blocks. Public because the cabin's jaw is authored closed on
    /// this surface with 0.04 unit of clearance, so the two drift apart silently if only one of them moves -
    /// <c>RopewayAssetContractTests.TheCabinFitsThroughTheTower</c> is what notices.
    /// </summary>
    public const float CableRadius = 0.06f;

    /// <summary>
    /// The station rail's cross-section, in blocks: two boxes 2.2 units wide and 4 units deep with their
    /// inner faces 2.6 units off the centre line, hanging so the band runs 0.75 to 0.50 blocks under the
    /// sheave. The cabin's guide rollers reach 2.5 units, so the fit is 0.1 unit - 0.00625 blocks - per
    /// side, which is the tightest in the machine and the reason the rail cannot be left on a cardinal
    /// while the cabin turns off it.
    /// <para>
    /// These four numbers used to be a copy of <c>pylonhead.json</c>'s authored <c>railwest</c>, which was
    /// the one cell of rail under the sheave that stayed cardinal. That plate is gone and the run starts at
    /// the tower centre instead, so the rail is now ENTIRELY a runtime cross-section and the rollers are
    /// what it is pinned against - <c>TheDrawnRailIsTheBarTheGuideRollersRideIn</c>.
    /// </para>
    /// </summary>
    public const double RailOffset = 3.7 / 16;

    /// <summary>See <see cref="RailOffset"/>. Half the 2.2-unit width.</summary>
    public const float RailHalfWidth = 1.1f / 16;

    /// <summary>See <see cref="RailOffset"/>. Half the 4-unit depth.</summary>
    public const float RailHalfDepth = 2f / 16;

    /// <summary>See <see cref="RailOffset"/>. Blocks from the sheave down to the band's centre line.</summary>
    public const double RailDrop = 0.625;

    /// <summary>
    /// Sample spacing through the bend window, in blocks. A chain of straight boxes cuts the corner by the
    /// chord's sagitta, and the tightest curvature the bend ever reaches is a 1.317-block radius (a 90
    /// degree turn at the full 4-block window), so this is set by <c>step^2 / (8 * 1.317)</c> against the
    /// 0.0025 blocks of play the jaw is authored with: 0.125 measures 0.0015 blocks, so the drawn rope stays
    /// inside the clamp that is closed on it. 0.25 measures 0.0054 and does not. Both are invisible beside
    /// the 0.419 blocks the cable was out by when it was one straight box per half span; the jaw's number is
    /// used because it is the one the assets already agree on.
    /// </summary>
    private const double RunStep = 0.125;

    /// <summary>
    /// How far off its own chord a sample may sit and still be dropped, in blocks. Twenty times under
    /// <see cref="RunStep"/>'s sagitta, so it never fires on a real bend - it exists for the towers that
    /// bend by nothing at all. See <see cref="BuildRun"/>.
    /// </summary>
    private const double CollapseTolerance = 0.0005;

    /// <summary>
    /// Every other box of a run is drawn a fiftieth of a unit thinner. Two butt-jointed boxes on a curve
    /// overlap on the INSIDE of the joint with two of their faces in one plane - about 0.03 unit^2 of
    /// z-fight per joint on the cable, and a corner tower has a hundred and sixty joints. This is the same
    /// phase trick <c>gen_bullwheelrim.py</c> already uses on the felloe: 0.02 units is 1/800 of a block,
    /// invisible on a two-pixel cable, and it puts the two faces in different planes so neither the depth
    /// buffer nor the renderer's coplanar audit has anything to argue about. The alternative is mitred
    /// joints with shared vertices, which means not using <c>CubeMeshUtil.GetCube</c> at all.
    /// <para>
    /// WHICH of the two faces is a question about the plane the run curves in, which is why
    /// <see cref="BuildRun"/> has to be told. <see cref="BuildBox"/> aims the box by pitch then yaw, so its
    /// x half-extent ends up horizontal and across the run and its y half-extent in the vertical plane that
    /// contains it. A cable or a rail bends in PLAN, so its side faces swing apart at every joint and its up
    /// and down faces are the ones left in one plane. The wrap turns in a VERTICAL plane, so it is the exact
    /// opposite - the sixteen chords are all in the two planes x = +/-CableRadius, and phasing their depth
    /// leaves 4.65 unit^2 of z-fight per joint, measured.
    /// </para>
    /// </summary>
    private const float JointPhase = 0.02f / 16;

    /// <summary>
    /// Draws this tower's half of every span it carries - the haul rope AND the station rail, both sampled
    /// off <see cref="RopewayLine.PositionAt"/>, which is the same function the cabin's own position comes
    /// from. That is the whole of this: rope, rail and cabin are one curve because they are one call. At a
    /// terminal wearing a bullwheel it also draws the wrap - see <see cref="WrapPath"/>.
    /// <para>
    /// Each end draws only to the midpoint rather than one end drawing the whole cable, which means there is
    /// never coincident geometry to z-fight and a span whose far chunk is unloaded still shows the half you
    /// are standing next to.
    /// </para>
    /// ponytail: drawn by the footing and pushed up <see cref="SpanMath.SheaveHeight"/>, not by the sheave.
    /// The sheave has no block entity since the controller moved to the ground, and giving it one purely to
    /// hold a mesh would mean a second position to key spans by - which is the exact class of bug the one
    /// canonical position exists to prevent. One offset, the same constant AnchorOf uses.
    /// ponytail: straight, not sagging. The cabin travels the chord between anchors (bent at the towers, and
    /// the rope is bent with it), and IsSpanClear certifies that corridor, so a drawn catenary would still be
    /// a cable that lies about where the cabin goes. Sag needs the cabin to sag too.
    /// ponytail: a long cable is one mesh hanging off one block entity, so it vanishes when its own chunk
    /// leaves the view frustum even while the cable is still on screen. Emitting per-chunk segments is the
    /// fix if that reads badly in play.
    /// </summary>
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        // Deliberately discarding what base returns: the footing is a static structure the chunk tesselator
        // must keep drawing, and nothing on this block entity has any business replacing the default. The
        // trap that made this explicit lives on BEDriveHousing.OnTesselation now, with the MP behaviour.
        base.OnTesselation(mesher, tessThreadTesselator);
        const bool replacedDefault = false;
        if (Spans.Count == 0 || Block == null) return replacedDefault;

        var line = LocalLine(out var me);
        if (line == null) return replacedDefault;

        TextureAtlasPosition rope;
        TextureAtlasPosition metal;
        try
        {
            var textures = tessThreadTesselator.GetTextureSource(Block);
            rope = textures?["rope"];
            metal = textures?["metal"];
        }
        catch (Exception e)
        {
            // Runs on the tesselation thread - never take the chunk mesher down over a missing texture.
            Api?.Logger.Warning("Ropeway: no rope texture for the cable at {0}: {1}", Pos, e.Message);
            return replacedDefault;
        }

        if (rope == null) return replacedDefault;

        for (var peer = 0; peer < line.Towers.Length; peer++)
        {
            if (peer == me) continue;

            Emit(mesher, BuildRun(HalfSpanPath(line, me, peer), CableRadius, CableRadius, rope));
            if (metal == null) continue;

            // Both cheeks of the slot, from one function, off the same curve. Every authored rail element is
            // gone from both head shapes and none is replaced: a rail drawn ON the path opens by exactly the
            // half turn the cabin arrives crooked by, and at a straight tower it opens by nothing, which is
            // correct - there is nothing to guide in.
            //
            // A hair shallower than nominal, and the reason survived the plate it was first written for. The
            // band's top face is EXACTLY the crossarm's soffit, so at a badly-faced corner - where the rail
            // runs along under the braces rather than out through the archway - the two would be one plane
            // for the whole 3.5-block reach of the crossarm. Same trick and same reason as JointPhase.
            var depth = RailHalfDepth - JointPhase;
            Emit(mesher, BuildRun(RailPath(line, me, peer, RailOffset), RailHalfWidth, depth, metal));
            Emit(mesher, BuildRun(RailPath(line, me, peer, -RailOffset), RailHalfWidth, depth, metal));
        }

        // The wrap, and everything that carries the wheel out to it. A property of the TOWER rather than of
        // a span, so it is outside the loop: one ring per terminal, drawn once.
        var dead = WearsABullwheel ? DeadSide : null;
        if (dead == null) return replacedDefault;

        Emit(mesher, BuildRun(WrapPath(dead), CableRadius, CableRadius, rope, turnsVertically: true));
        if (metal == null) return replacedDefault;

        // A fiftieth of a unit narrower than the cheek column it stands in, and the reason is JointPhase's:
        // the bearing cap, the bearing stand and the sheave cheek all present a face at that exact x, and a
        // plate flush with them would be 11.5 unit^2 of z-fight against the cap alone.
        Emit(mesher, BuildRun(OutriggerPath(dead, RailOffset), RailHalfWidth - JointPhase, RailHalfDepth, metal));
        Emit(mesher, BuildRun(OutriggerPath(dead, -RailOffset), RailHalfWidth - JointPhase, RailHalfDepth, metal));

        return replacedDefault;
    }

    private static void Emit(ITerrainMeshPool mesher, MeshData mesh)
    {
        if (mesh != null) mesher.AddMeshData(mesh);
    }

    /// <summary>
    /// This tower and its peers as a two- or three-tower <see cref="RopewayLine"/>, which is all the line
    /// anything drawn here needs. <see cref="RopewayLine.FromTowers"/> is pure - no world access, no cache -
    /// so this is safe on the tesselation thread, and it is the same builder the cabin's line comes out of.
    /// Null when nothing usable is linked.
    /// <para>
    /// A LOCAL line is enough, and that is what makes drawing the real curve affordable. The bend window is
    /// <c>TrimForTowers(L) &lt;= (L-1)/2 &lt; L/2</c>, so a tower's bend never reaches the midpoint where its
    /// half of the cable stops. The mini-line's far anchor is an END, so its tangent is its single leg and
    /// its bend term is identically zero - exactly the straight half the peer draws over from its own side.
    /// Nothing has to read the peer's block entity, which is what would have made this a cross-chunk read on
    /// the wrong thread.
    /// </para>
    /// <para>
    /// The curve does not care which way the chain runs: reversing it negates both the leg and the bisector,
    /// so their difference flips sign at the same time as the end it is applied at swaps. That is why a
    /// mini-line built peer-Pos-peer agrees with the real line whichever way <c>WalkChain</c> canonicalised.
    /// </para>
    /// </summary>
    private RopewayLine LocalLine(out int me)
    {
        me = 0;

        var peers = new List<BlockPos>(MaxSpansPerTower);
        foreach (var peer in Spans)
        {
            if (peer != null && !peer.Equals(Pos)) peers.Add(peer);
        }

        if (peers.Count == 0) return null;

        var chain = new List<BlockPos>(3);
        if (peers.Count > 1) chain.Add(peers[0]);
        chain.Add(Pos);
        chain.Add(peers[peers.Count - 1]);

        me = chain.Count - 2;
        return RopewayLine.FromTowers(chain);
    }

    /// <summary>
    /// The path from this tower's sheave to the midpoint of one span, in blocks relative to the sheave:
    /// sampled every <see cref="RunStep"/> through the bend window, then ONE straight box for the middle,
    /// which the bend leaves untouched.
    /// </summary>
    private static List<Vec3d> HalfSpanPath(RopewayLine line, int me, int peer)
    {
        var origin = line.Anchors[me];
        var start = line.Cumulative[me];
        var half = (line.Cumulative[peer] - start) / 2;
        var window = SpanMath.TrimForTowers(Math.Abs(half) * 2);
        var sign = half < 0 ? -1 : 1;

        // The anchor itself, and it is exact: BendWeight is 0 at distance 0, so PositionAt(Cumulative[me])
        // IS Anchors[me]. Written as the zero vector rather than sampled so a short span with no window at
        // all still starts at the sheave instead of dividing by a step count of zero.
        var points = new List<Vec3d> { new() };

        var steps = window <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(window / RunStep));
        for (var i = 1; i <= steps; i++)
        {
            points.Add(line.PositionAt(start + sign * window * i / steps).Sub(origin));
        }

        points.Add(line.PositionAt(start + half).Sub(origin));
        return points;
    }

    /// <summary>
    /// One cheek of the station rail, from the tower centre out to the edge of the bend window, in blocks
    /// relative to the sheave. <paramref name="lateral"/> is the signed offset across the path; the sign of
    /// the perpendicular follows the tangent's own direction, which only decides which cheek is which and
    /// both are drawn. Null when the span is too short to have a window worth railing.
    /// <para>
    /// It starts AT the tower now rather than half a block out. The head used to author one cardinal cell of
    /// rail directly under the sheave, kept because a parked cabin squares to the tower's cardinal rather
    /// than to the path - but a cabin PASSING a corner tower rode the drawn run and went straight through
    /// that plate, 1.37 units deep at a right angle, and the parked argument survives the plate's deletion
    /// anyway: the drawn rail's inner face is 2.6 units from the path everywhere and is never closer than
    /// that to the anchor, against a roller whose worst corner reach about the anchor is 2.571 at any yaw.
    /// So the run covers the head cell too, and the two cheeks a two-span tower draws meet end to end under
    /// the sheave on the bisector both of them start from.
    /// </para>
    /// </summary>
    private static List<Vec3d> RailPath(RopewayLine line, int me, int peer, double lateral)
    {
        var origin = line.Anchors[me];
        var start = line.Cumulative[me];
        var half = (line.Cumulative[peer] - start) / 2;
        var window = SpanMath.TrimForTowers(Math.Abs(half) * 2);
        if (window <= 0) return null;

        var sign = half < 0 ? -1 : 1;
        var steps = Math.Max(1, (int)Math.Ceiling(window / RunStep));

        var points = new List<Vec3d>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var travelled = start + sign * window * i / steps;
            var point = line.PositionAt(travelled).Sub(origin);
            var dir = line.DirectionAt(travelled);

            // The rail hangs UNDER the rope and beside it, so the offset is the horizontal normal of the
            // path's own tangent. A purely vertical tangent has no bearing to be beside, and a span cannot
            // be vertical (LegOf refuses one), so the guard is belt and braces rather than a real case.
            var plan = Math.Sqrt(dir.X * dir.X + dir.Z * dir.Z);
            if (plan < 1e-9) return null;

            points.Add(new Vec3d(
                point.X + lateral * -dir.Z / plan,
                point.Y - RailDrop,
                point.Z + lateral * dir.X / plan));
        }

        return points;
    }

    /// <summary>
    /// Chords in the wrap. EVEN, so <see cref="BuildRun"/>'s alternating joint phase closes round the ring
    /// instead of putting two full-depth boxes in one plane where it meets itself. Sixteen departs from the
    /// true circle by 0.20 units, a tenth of the cable's own thickness.
    /// </summary>
    private const int WrapChords = 16;

    /// <summary>
    /// The haul rope at a terminal: out of the sheave along the dead side and round the bullwheel's groove,
    /// in blocks relative to the sheave. One polyline, so it is one <see cref="BuildRun"/> call.
    /// <para>
    /// A CLOSED RING, not the 180-degree arc a real terminal wraps. A real one sends its second strand back
    /// down the line 2*rho above the first; this mod draws ONE strand for a loop everywhere else - the cabin
    /// hangs on it, <c>IsSpanClear</c> certifies one corridor, one run is drawn per span - so a second strand
    /// here would be a cable the whole length of the line that nothing hangs on. The two strands of the loop
    /// collapse onto one ring, which is the same collapse the rest of the mod already makes, and it is the
    /// only version with no free end: a true arc stops in mid air where the second strand would leave, which
    /// reads as a snapped rope.
    /// </para>
    /// <para>
    /// CHUNK MESH rather than something the renderer turns with the rim, which costs nothing per frame and
    /// is honest: <see cref="BuildBox"/> flat-samples its UVs, so the cable carries no lengthwise detail at
    /// all and a static ring on a spinning rim is indistinguishable from one that turns with it.
    /// </para>
    /// <para>
    /// The vertices sit on a circle of <c>rho / cos(pi/n)</c> so the chord MIDPOINTS - not the corners -
    /// land on rho; the bottom midpoint is then exactly on the rope's centreline, which is the whole point
    /// of the number. The straight stub out of the sheave is collinear with that bottom chord and
    /// <see cref="OnTheLine"/> merges the two, so what comes back is sixteen boxes and not seventeen, and
    /// the phase alternates all the way round.
    /// </para>
    /// </summary>
    public static List<Vec3d> WrapPath(Vec3d dead)
    {
        if (dead == null) return null;

        var vertex = BullwheelRenderer.WrapRadius / Math.Cos(Math.PI / WrapChords);

        // The sheave itself, exactly, the same way HalfSpanPath starts: the rope leaves the throat here.
        var points = new List<Vec3d>(WrapChords + 2) { new() };
        for (var k = 0; k <= WrapChords; k++)
        {
            // Measured from straight down, so k = 0 and k = WrapChords are the same vertex and the ring
            // closes on itself with no join to explain.
            var angle = (2 * k - 1) * Math.PI / WrapChords;
            var along = BullwheelRenderer.WrapOut + vertex * Math.Sin(angle);

            points.Add(new Vec3d(
                dead.X * along,
                BullwheelRenderer.WrapRadius - vertex * Math.Cos(angle),
                dead.Z * along));
        }

        return points;
    }

    /// <summary>
    /// One of the two side plates that carry the wheel out to where it wraps, in blocks relative to the
    /// sheave: from the bearing cap standing on that cheek to the wheel's own hub, 17.5 units long at 23.9
    /// degrees below the lay shaft. On a drive station it reads as the chain case down to the sprocket and
    /// on a tension station as the carriage tie back to the counterweight head, which is exactly the call
    /// <c>ropeway:layshaft</c> already makes - at 16 pixels they are the same forged bar.
    /// <para>
    /// It takes the RAIL's lateral offset and cross-section because those are the cheeks' own columns:
    /// <c>sheavecheekwest</c> and the bearing stand and cap above it all occupy x 3.2..5.4 on both head
    /// shapes. Offset across the DEAD SIDE rather than across the block's facing, like everything else drawn
    /// here, so a wheel placed a quarter turn out still gets a bracket in the plane it turns in.
    /// </para>
    /// </summary>
    private static List<Vec3d> OutriggerPath(Vec3d dead, double lateral)
    {
        // Blocks above the anchor, which is where the axle stands and the shipped bearings hold it.
        var shaft = BullwheelRenderer.RimPivotY - 0.5;

        return new List<Vec3d>
        {
            new(-dead.Z * lateral, shaft, dead.X * lateral),
            new(dead.X * BullwheelRenderer.WrapOut - dead.Z * lateral,
                BullwheelRenderer.WrapRadius,
                dead.Z * BullwheelRenderer.WrapOut + dead.X * lateral)
        };
    }

    /// <summary>
    /// A chain of boxes along a polyline, in coordinates local to this tower's SHEAVE. The cable, both
    /// station rails, the wrap and both outriggers are all calls to this, which is the point: everything the
    /// cable had to learn the hard
    /// way - the face count the chunk tesselator loops over, the colour maps it indexes per face, the
    /// flat-sampled UV that stopped the striping - is solved once here and inherited rather than copied.
    /// Null when nothing survives the degenerate check.
    /// </summary>
    public static MeshData BuildRun(
        IReadOnlyList<Vec3d> points, float radiusX, float radiusY, TextureAtlasPosition texPos,
        bool turnsVertically = false)
    {
        if (points == null) return null;

        MeshData run = null;
        var start = 0;
        var emitted = 0;
        for (var i = 1; i < points.Count; i++)
        {
            // Collinear samples become ONE longer box. Not a micro-optimisation: the window is sampled
            // unconditionally, and it is exactly straight at every end tower (its tangent is its single leg,
            // so the bend term is identically zero) and at every interior tower whose two spans share a
            // bearing - which on a line a player actually builds is most of them. Without this a dead
            // straight run pays thirty-two boxes per span end to draw a straight line, and with it the
            // corners pay for themselves and nothing else does.
            if (i + 1 < points.Count && OnTheLine(points[start], points[i], points[i + 1])) continue;

            var phase = emitted % 2 == 0 ? 0 : JointPhase;
            var box = BuildBox(
                points[start], points[i],
                turnsVertically ? radiusX - phase : radiusX,
                turnsVertically ? radiusY : radiusY - phase,
                texPos);

            // Only advance past a segment that produced something: a degenerate pair must merge into the
            // next one rather than silently drop the geometry between them.
            if (box == null) continue;
            start = i;
            emitted++;

            if (run == null) run = box;
            else run.AddMeshData(box);
        }

        return run;
    }

    /// <summary>
    /// Whether <paramref name="b"/> sits on the segment from <paramref name="a"/> to <paramref name="c"/>
    /// within <see cref="CollapseTolerance"/> - the cross product's magnitude over the base length, which is
    /// the perpendicular distance. Self-limiting on a curve: the deviation it measures grows with every
    /// sample merged, so a real bend fails the test on its first step and never accumulates.
    /// </summary>
    private static bool OnTheLine(Vec3d a, Vec3d b, Vec3d c)
    {
        var dx = c.X - a.X;
        var dy = c.Y - a.Y;
        var dz = c.Z - a.Z;
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (length < 1e-9) return false;

        var ex = b.X - a.X;
        var ey = b.Y - a.Y;
        var ez = b.Z - a.Z;

        var cx = ey * dz - ez * dy;
        var cy = ez * dx - ex * dz;
        var cz = ex * dy - ey * dx;

        return Math.Sqrt(cx * cx + cy * cy + cz * cz) <= CollapseTolerance * length;
    }

    /// <summary>
    /// A thin box from this tower's sheave to the midpoint of the span, in coordinates local to the FOOTING
    /// block that draws it. Kept as the two-point degenerate case of <see cref="BuildRun"/> because it is
    /// the shape of the call the unit tests pin the mesh's failure modes with - the cable's failure mode is
    /// that it renders nothing at all, silently. Null when the peer lands on top of us.
    /// </summary>
    public static MeshData BuildHalfCable(double dx, double dy, double dz, TextureAtlasPosition texPos)
    {
        return BuildBox(new Vec3d(), new Vec3d(dx, dy, dz), CableRadius, CableRadius, texPos);
    }

    /// <summary>
    /// One box of a run, between two points given in blocks relative to the sheave - hence the
    /// <see cref="SpanMath.SheaveHeight"/> in the translate, which is what makes the drawn geometry and
    /// <see cref="SpanMath.AnchorOf"/> agree. Static and therefore unit-tested.
    /// </summary>
    private static MeshData BuildBox(
        Vec3d from, Vec3d to, float radiusX, float radiusY, TextureAtlasPosition texPos)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dz = to.Z - from.Z;

        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (length < 0.01) return null;

        // ScaleCubeMesh does `xyz = xyz * scale + scale`, so the box lands corner-at-origin; the translate
        // argument is what puts it back around the origin the rotations below turn about.
        var mesh = CubeMeshUtil.GetCube(
            radiusX, radiusY, (float)(length / 2),
            new Vec3f(-radiusX, -radiusY, (float)(-length / 2)));

        // GetCube leaves XyzFaces empty, and the chunk tesselator emits geometry only inside
        // `for (l = 0; l < sourceMesh.XyzFacesCount; l++)` (JsonTesselator.cs:709) - so without this the
        // cable copies zero vertices into the chunk mesh, silently, with no exception and no log line.
        CubeMeshUtil.SetXyzFacesAndPacketNormals(mesh);

        // ...and once that loop runs it indexes Season/ClimateColorMapIds per face (JsonTesselator.cs:834),
        // which GetCube leaves zero-length. Without this, fixing the face count only trades an invisible
        // cable for an IndexOutOfRangeException.
        mesh.WithColorMaps();

        // Aim the box's +Z down the segment: pitch about X, then yaw about Y. Two calls rather than one
        // Rotate(rx, ry, 0) so the composition order is explicit here instead of a property of Mat4f.RotateXYZ.
        SpanMath.CableAngles(dx, dy, dz, out var radX, out var radY);
        mesh.Rotate(new Vec3f(0, 0, 0), radX, 0, 0);
        mesh.Rotate(new Vec3f(0, 0, 0), 0, radY, 0);

        mesh.Translate(
            (float)(0.5 + (from.X + to.X) / 2),
            (float)(0.5 + SpanMath.SheaveHeight + (from.Y + to.Y) / 2),
            (float)(0.5 + (from.Z + to.Z) / 2));

        // Flat-sample the texture instead of letting the cube's own UVs through. ScaleCubeMesh multiplies
        // the UVs by the axis scale (CubeMeshUtil.cs:230-251), so a half-span 24 blocks long leaves them
        // running 0..48 - and SetTexPos maps u through `x1 + u * (x2 - x1)`, which puts everything past 1
        // OUTSIDE this texture's sub-region of the block atlas, sampling whatever sprites are next to it.
        // That is the striping. 0.5 lands on the middle of the sprite, far from its edges and therefore
        // safe under mipmapping; a 2-pixel-thick cable has nowhere to show lengthwise detail anyway, and
        // normalising to 0..1 instead would smear one 32x32 sprite over the whole span.
        Array.Fill(mesh.Uv, 0.5f);
        mesh.SetTexPos(texPos);
        return mesh;
    }

    public bool HasSpanTo(BlockPos peer)
    {
        if (peer == null) return false;
        for (var i = 0; i < Spans.Count; i++)
        {
            if (peer.Equals(Spans[i])) return true;
        }

        return false;
    }

    public void AddSpan(BlockPos peer)
    {
        if (peer == null || HasSpanTo(peer) || Spans.Count >= MaxSpansPerTower) return;
        Spans.Add(peer.Copy());

        // redrawOnClient: the cable is chunk mesh, so without this the span is invisible until a reload.
        MarkDirty(true);
    }

    public void RemoveSpan(BlockPos peer)
    {
        if (peer == null) return;
        for (var i = Spans.Count - 1; i >= 0; i--)
        {
            if (peer.Equals(Spans[i])) Spans.RemoveAt(i);
        }

        // redrawOnClient: otherwise a cut cable keeps hanging there until the chunk reloads.
        MarkDirty(true);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        StructureComplete = tree.GetBool("structureComplete");
        side = tree.GetString("side") ?? side;
        TowerName = tree.GetString("towerName");

        Spans.Clear();
        var count = tree.GetInt("spanCount");
        for (var i = 0; i < count; i++)
        {
            var peer = ReadPos(tree, "span" + i);
            if (peer != null) Spans.Add(peer);
        }

        // Operand order matters: Api is null on the first chunk-load call, so the cast check must come first.
        if (Api is ICoreClientAPI && structure != null && structure.TransformedOffsets == null && side != null)
        {
            Init();
        }

        // The cable is chunk mesh, so a Spans list that arrives after the chunk has already been tesselated
        // stays invisible until something else dirties the block. Vanilla's idiom for exactly this is
        // BlockEntityDisplay.cs:119-126. Cheap: MarkBlockDirty only queues a re-tesselation.
        if (Api is ICoreClientAPI) Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetBool("structureComplete", StructureComplete);
        if (side != null) tree.SetString("side", side);
        if (TowerName != null) tree.SetString("towerName", TowerName);

        tree.SetInt("spanCount", Spans.Count);
        for (var i = 0; i < Spans.Count; i++) WritePos(tree, "span" + i, Spans[i]);
    }

    /// <summary>
    /// Server-side rename. False when nothing changed, so the caller can skip the sync. MarkDirty without
    /// redrawOnClient: a name is not geometry, and re-tesselating every cable on a rename would be silly.
    /// </summary>
    public bool Rename(string raw)
    {
        var name = SanitiseName(raw);
        if (name == TowerName) return false;

        TowerName = name;
        MarkDirty();
        return true;
    }

    /// <summary>
    /// Player text arriving over the network, so this is a trust boundary: control characters corrupt the
    /// chat and GUI text renderers, and an unbounded string would be persisted verbatim on every tower and
    /// re-sent to every client in range. Null when nothing readable survives. Pure, and therefore tested.
    /// This is the single chokepoint every display path routes through, which is why the VTML strip lives
    /// here and not per surface.
    /// </summary>
    public static string SanitiseName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;

        var builder = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            // A name reaches two VTML-rendered surfaces - GetBlockInfo's rich-text panel and the
            // span-linked / span-cut chat lines, which HudDialogChat composes with AddRichtext - and 24
            // characters is enough for <font color="red"> or a short <a href>. No tag can survive without
            // its brackets, and a place name has no use for them.
            if (c == '<' || c == '>') continue;

            // Every flavour of whitespace collapses to one plain space - a tab or a newline inside a
            // one-line GUI label is a layout bug, and runs of spaces are just padding to fake a longer name.
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                if (builder.Length > 0 && builder[builder.Length - 1] != ' ') builder.Append(' ');
                continue;
            }

            builder.Append(c);
        }

        var name = builder.ToString().Trim();
        if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength).TrimEnd();

        // Cutting to a fixed length can land between the two halves of a surrogate pair, which renders as a
        // replacement glyph rather than the character the player typed.
        if (name.Length > 0 && char.IsHighSurrogate(name[name.Length - 1])) name = name.Substring(0, name.Length - 1);

        return name.Length == 0 ? null : name;
    }

    // TreeAttributeUtil.SetBlockPos writes InternalY but GetBlockPos reads it back as a plain Y with
    // dimension 0, which silently corrupts any tower inside a pocket dimension. Store the four ints.
    public static void WritePos(ITreeAttribute tree, string key, BlockPos pos)
    {
        tree.SetInt(key + "X", pos.X);
        tree.SetInt(key + "Y", pos.Y);
        tree.SetInt(key + "Z", pos.Z);
        tree.SetInt(key + "D", pos.dimension);
    }

    public static BlockPos ReadPos(ITreeAttribute tree, string key)
    {
        if (!tree.HasAttribute(key + "X")) return null;
        return new BlockPos(tree.GetInt(key + "X"), tree.GetInt(key + "Y"), tree.GetInt(key + "Z"), tree.GetInt(key + "D"));
    }

    public override void OnBlockRemoved()
    {
        // Block.OnBlockBroken is not the only way a tower dies: OnBlockExploded and every SetBlock(0, pos)
        // - worldedit, schematics, other mods - reach here instead, and would leave the peer's persisted
        // Spans naming a tower that no longer exists, forever, with nothing able to repair it.
        // Idempotent: the break path already unlinked, so UnlinkAll early-returns on an empty Spans.
        // Must precede Forget(), which drops this tower out of LoadedTowers that UnlinkAll resolves through
        // - the block itself is already air here, so the BlockAccessor fallback finds nothing.
        // refundTo null: no player on these paths, and nobody gets paid for a bomb.
        if (Api?.Side == EnumAppSide.Server) ModSystem?.LinkService?.UnlinkAll(Pos, null);

        base.OnBlockRemoved();
        Forget();
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        Forget();
    }

    private void Forget()
    {
        highlightFor = null;
        if (Api is ICoreClientAPI capi) highlightedStructure?.ClearHighlights(Api.World, capi.World.Player);

        var modSystem = ModSystem;
        if (modSystem == null) return;

        modSystem.InvalidateLine(Pos);
        modSystem.LineCache.Remove(Pos);
        modSystem.LoadedTowers.Remove(Pos);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        // No "unnamed" placeholder line: from in front of the tower there is no bearing to fall back to that
        // the player does not already have, so the honest fallback is to say nothing.
        if (TowerName != null) dsc.AppendLine(Lang.Get("ropeway:blockinfo-name", TowerName));

        if (!StructureComplete)
        {
            var missing = Math.Max(0, IncompleteCount());
            dsc.AppendLine(Lang.Get("ropeway:tower-incomplete", missing));

            // The frame is one block deep and symmetric, so nothing about a placed footing shows which way
            // its crossarm will go until the braces are up. Naming the passage axis is what lets a player
            // face the tower down the line BEFORE building it rather than after.
            dsc.AppendLine(Lang.Get(
                "ropeway:blockinfo-passage",
                Lang.Get("game:facing-" + PassageFacing.Code),
                Lang.Get("game:facing-" + PassageFacing.Opposite.Code)));
            return;
        }

        dsc.AppendLine(Lang.Get("ropeway:tower-complete"));
        dsc.AppendLine(Lang.Get("ropeway:blockinfo-spans", Spans.Count));

        if (IsEndpoint) dsc.AppendLine(Lang.Get("ropeway:blockinfo-endpoint"));

        var line = RopewayLine.GetOrBuild(ModSystem, Pos);
        if (line != null)
        {
            dsc.AppendLine(Lang.Get("ropeway:blockinfo-line", (int)Math.Round(line.TotalLength), line.Towers.Length));

            // Otherwise the shortfall is invisible: the panel reports a line that stops at a tower which is
            // only the end of the loaded part of it, and a cabin holding there looks broken rather than held.
            if (line.Truncated) dsc.AppendLine(Lang.Get("ropeway:blockinfo-line-truncated"));

            AppendPowerLines(line, dsc);
            AppendCabinLine(line, dsc);
        }
    }

    /// <summary>
    /// The whole power situation, on the block the player already clicks. Vanilla prints nothing about
    /// mechanical power outside EntityDebugMode, so a machine that does not diagnose itself is a machine
    /// nobody can learn - and a cabin standing still because the wind dropped looks exactly like a cabin
    /// standing still because it is broken until this panel says which.
    /// </summary>
    private void AppendPowerLines(RopewayLine line, StringBuilder dsc)
    {
        // The LINE's drive, which is what actually moves the cabin: one drive station here plus one at the
        // far end is a faster cabin, and nothing else on this panel would show that. Deliberately not a
        // per-tower figure even now that a drive belongs to exactly one tower - the rope is one loop, so
        // every tower on it moves at the same speed and a per-tower line would invite the reading that a
        // station drives its own end of the line.
        var speed = RopewayPower.CabinSpeed(DriveSpeedOn(ModSystem, line));
        dsc.AppendLine(speed > 0
            ? Lang.Get("ropeway:blockinfo-linedrive", Math.Round(speed, 1))
            : Lang.Get("ropeway:blockinfo-nolinedrive"));

        if (!HasTensioner(ModSystem, line)) dsc.AppendLine(Lang.Get("ropeway:blockinfo-notensioner"));
    }

    /// <summary>
    /// Where the cabin is relative to THIS tower, now that every tower on the line can call it. Says nothing
    /// at all when no cabin is loaded: the far end of a long line is outside the client's entity tracking
    /// range, so "elsewhere" would be a claim this cannot tell apart from "there is no cabin".
    /// </summary>
    private void AppendCabinLine(RopewayLine line, StringBuilder dsc)
    {
        var cabin = EntityRopewayCabin.FindOn(Api.World, line);
        if (cabin == null) return;

        // Position rather than Travelled: Travelled is on the server-only half of the entity's attributes,
        // so the copy a client holds is whatever it was at spawn time.
        var anchor = SpanMath.AnchorOf(Pos);
        var dx = anchor.X - cabin.Pos.X;
        var dz = anchor.Z - cabin.Pos.Z;
        if (dx * dx + dz * dz <= 1)
        {
            dsc.AppendLine(Lang.Get("ropeway:blockinfo-cabin-here"));
            return;
        }

        // Destination is a distance from the cabin's OWN Towers[0], so it only names a place on this line
        // when this line was walked from that same base. A client whose chunk view is short walks a
        // contiguous run of the chain and no more, so a matching base is enough: every tower it does have
        // then carries the same Cumulative the server computed. A different base means the client is
        // measuring from somewhere else entirely, and "coming here" would be a guess.
        var index = line.IndexOf(Pos);
        var bound = index >= 0 && cabin.HasDestination && line.Towers[0].Equals(cabin.LineKey)
            && Math.Abs(cabin.Destination - line.Cumulative[index]) < EntityRopewayCabin.ArrivalTolerance;

        dsc.AppendLine(Lang.Get(bound ? "ropeway:blockinfo-cabin-coming" : "ropeway:blockinfo-cabin-elsewhere"));
    }
}
