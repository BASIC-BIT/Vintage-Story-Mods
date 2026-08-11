using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
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
    /// Which side of the tower this footing's MACHINE LEG stands on - the drive housing and its shaft column,
    /// or the tension weight and its guides - and null on a plain tower, which has a post column on both
    /// sides and no machinery on either.
    /// <para>
    /// A DERIVATION AND NOT A CHOICE, and this property exists because the choice was already there and
    /// nothing said so. Every station's leg is the crossarm's local +X, the passage is its local Z, and
    /// <c>InitForUse(RotationFor(side))</c> turns the whole cell list together - so the leg is one quarter
    /// turn clockwise of <see cref="PassageFacing"/>, and the two variants that share a line's bearing
    /// (north/south for a line running north-south, east/west for one running east-west) put it on OPPOSITE
    /// sides of the posts. The mirrored station has shipped since stations did; what had not shipped was any
    /// way for a player to know which of the two he was about to place, because both read
    /// "the cabin will pass through north to south" and nothing else.
    /// <c>AStationsMachineLegMirrorsWhenTheFootingIsPlacedFromTheOtherSide</c> holds the derivation against
    /// the shipped offsets at all four facings, so this cannot start lying if the leg ever moves.
    /// </para>
    /// <para>
    /// A SECOND blocktype, or a second variant group crossed with <c>side</c>, was the other way to offer
    /// this and is what it would have cost: eight placements per station instead of four for
    /// <c>ASharedMachineLegSatisfiesAtMostOneStation</c> to keep apart, a second code for
    /// <see cref="OwnTheHeadCell"/> to narrow, and every recipe, handbook group and creative entry doubled -
    /// to reach placements the four existing variants already reach.
    /// </para>
    /// <para>
    /// Gated on <see cref="WearsABullwheel"/>, which is not a coincidence dressed as a predicate: the drive
    /// and tension stations are exactly the two footings whose structure names <c>bullwheel-*</c> for the
    /// crossarm centre (a plain tower names <c>pylonhead-*</c> and a shaft head <c>shaftsheave-*</c>), and
    /// <c>AStationWearsTheBullwheelAndAPlainTowerWearsTheHead</c> pins that both ways. The SHAFT head is left
    /// out deliberately even though it carries the same leg: its facing also lays the counterweight's lane
    /// and <c>SpanMath.ShaftLinkFits</c> demands both footings share it, so a shaft's leg is not free to
    /// mirror and telling a player it is would be worse than saying nothing.
    /// </para>
    /// </summary>
    public BlockFacing MachineLegSide => WearsABullwheel ? PassageFacing.GetCW() : null;

    /// <summary>
    /// Which way the LINE runs at this tower - the unit tangent of the drawn path at this tower's own anchor,
    /// which at a terminal is its single leg and at a through station is the corner's bisector. Null when
    /// nothing is linked.
    /// <para>
    /// The SAME expression <see cref="OnTesselation"/> already takes the brackets and both rail cheeks across,
    /// lifted to a property because the wheel needs it too: <see cref="BullwheelRenderer"/> took its
    /// translation from the line and its rotation from the block's own <c>side</c> variant, which put the hub
    /// on the rope and the groove plane on the nearest cardinal, up to 90 degrees apart. Pure -
    /// <see cref="LocalLine"/> reads nothing but <see cref="Spans"/> and <see cref="Pos"/> - so it is as safe
    /// on the tesselation thread as it is on the client tick that polls it.
    /// </para>
    /// <para>
    /// Vertical on a SHAFT, where the peer is directly below and there is no bearing at all;
    /// <see cref="BullwheelRenderer.YawAlong"/> is what refuses that rather than letting <c>Atan2(0, 0)</c>
    /// answer due south.
    /// </para>
    /// </summary>
    public Vec3d LineTangent
    {
        get
        {
            var line = LocalLine(out var me);
            return line?.DirectionAt(line.Cumulative[me]);
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

    /// <summary>
    /// Which end of a SHAFT this footing is, or null on every ropeway tower - read once out of the blocktype's
    /// own <c>shaft</c> attribute, beside <see cref="IsTensioner"/>, because a station's kind is a property of
    /// its blocktype and cannot change under it.
    /// <para>
    /// A STRING and not a bool, because the two shaft footings are not interchangeable and exactly one thing
    /// has to say which is which: the HEAD wears the sheave, owns the drive leg, and its facing is the machine's
    /// only heading. Everything else in the mod that needs to tell two station kinds apart already reads an
    /// attribute (<c>tensioner</c>, <c>driveIntakeCell</c>); this is the same idiom with one more state, and it
    /// keeps <see cref="IsShaft"/> and <see cref="IsShaftHead"/> from being two flags that can disagree.
    /// </para>
    /// <para>
    /// Publicly settable for the same reason <see cref="IsTensioner"/> is: a BEPylonBase built without a world
    /// has no Block to read it off. Nothing in the mod writes it.
    /// </para>
    /// </summary>
    public string ShaftRole { get; set; }

    /// <summary>Whether this footing is either end of a shaft. See <see cref="ShaftRole"/>.</summary>
    public bool IsShaft => ShaftRole != null;

    /// <summary>Whether this footing is the TOP of a shaft - the one carrying the sheave and the drive.</summary>
    public bool IsShaftHead => ShaftRole == "head";

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
        ShaftRole = Block?.Attributes?["shaft"].AsString();
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
    /// The three ASYMMETRIC cells a footing's structure names by family - <c>drivehead</c>,
    /// <c>tensionhead</c> and <c>shaftsheave</c> - narrowed in this footing's OWN copy of the structure from
    /// the <c>-*</c> wildcard to the footing's own side.
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
    /// WHY only these three, when M4's looseness covers five blocks. The refusal M4 defers is the one that
    /// would bite <c>pylonhead</c> and <c>bullwheel</c>: those are symmetric along the rope axis, so a player
    /// who placed one from the other side of the tower has a geometrically identical block that would stop
    /// validating, and an incomplete tower is un-clickable - a saved world would lose its picker, its call
    /// and its rename over a block that looks perfectly right. Neither applies here.
    /// <c>ropeway:drivehead</c>, <c>ropeway:tensionhead</c> and <c>ropeway:shaftsheave</c> are NEW and
    /// untracked, so no saved world can hold a wrongly-faced one, and all three are visibly asymmetric -
    /// <c>drivehead.shaftwest</c> is x 0..4, <c>tensionhead</c>'s tie rod is x 0..12 against a sheave at
    /// x 8..16 - so a wrong facing is self-evident to the player rather than invisible. The other two keep the
    /// wildcard until M4's placement half lands.
    /// </para>
    /// <para>
    /// <c>shaftsheave</c> WAS MISSING FROM THIS LIST AND THAT WAS THE WHOLE OF THE BUG. It is a
    /// <c>HorizontalOrientable</c>, so it takes the PLAYER's facing at placement, and it is the least
    /// symmetric block in the mod: headframe columns at z 9.5..15.5, a beam and hangers reaching HUB_Z = -16,
    /// a chain case on the east face and an authored 180 degree wrap arc that turns in ONE vertical plane.
    /// Three of the four variants complete <c>shafthead.json</c>'s structure while pointing somewhere the rest
    /// of the machine does not: <see cref="BullwheelRenderer"/>'s yaw reads the SHEAVE's own side while
    /// <c>BEBullwheel.WrapOffset</c> and <c>ShaftRenderer</c> read the FOOTING's, so the wheel, the headframe
    /// and the rope end up in three different orientations, the chain case no longer meets the lay shafts it
    /// is drawn to meet - and the overlay says complete, the link succeeds and the lift runs.
    /// <c>AnAsymmetricHeadCellIsNarrowedToItsOwnFootingsFacing</c> pins all three families and fails on a
    /// mis-faced sheave.
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
        foreach (var head in new[] { "drivehead", "tensionhead", "shaftsheave" })
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

        // A SHAFT declares no climb, and the term is CANCELLED rather than discounted: the player has built the
        // machine that removes it. `ClimbLoad * climb` is the cost of lifting the car's own mass up the grade,
        // and a counterweight is precisely the car's own mass hung on the other strand - so on a shaft the drive
        // lifts only the imbalance, which is `cargo`, which is 0 until cargo weight lands. No new constant, no
        // relief factor, and no ropeway span's number moves: on every line that is not a shaft the climb term is
        // untouched and fully legible, which is the whole objection to a global discount answered.
        // The one thing it buys is the bottom rung: a sheltered 3-sail wooden mill in good wind goes from a hard
        // stall on a vertical line to 1.20 blocks a second. See docs/POWER-AND-STORAGE.md.
        consumer.Resistance = RopewayPower.Resistance(
            cabin?.IsHauling == true,
            line?.IsShaft == true ? 0 : cabin?.ClimbOn(line) ?? 0,
            0);
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
    /// Blocks from the footing at which a tower stops showing its own build guidance. Walking away is the
    /// only way a player ever stops asking for it - nothing clears the overlay of a tower he did not go back
    /// and click a second time - and before the ghosts landed that cost nothing, because a highlight cuboid
    /// has no depth test and reads as a distant marker. A GHOST is a solid block standing in the air, so a
    /// stale one reads as a tower somebody actually built. 24 is comfortably past the ~14 a player needs to
    /// stand back far enough to see a whole 7-wide, 5-tall pattern at once.
    /// </summary>
    public const double GuidanceRange = 24;

    /// <summary>
    /// The overlay is the primary build-guidance channel, so it has to follow the blocks the player is
    /// placing - a one-shot snapshot leaves ghost cells glowing on top of blocks that are already there.
    /// HighlightBlocks replaces the whole slot, so re-issuing it is the entire update. Idle until someone
    /// has actually asked for highlights on this tower, and done with it as soon as he finishes the tower or
    /// walks out of <see cref="GuidanceRange"/>.
    /// <para>
    /// The plain <c>Pos.Y</c> and not <c>InternalY</c>: <c>EntityPos.Y</c> is a position WITHIN the entity's
    /// own dimension while <c>BlockPos.InternalY</c> carries the dimension's 32768-block offset, so comparing
    /// the two would put every tower in a pocket dimension permanently out of range. A player in a different
    /// dimension from the tower cannot be looking at it anyway, and the horizontal terms still hold.
    /// </para>
    /// </summary>
    private void OnClientTick500ms(float dt)
    {
        if (highlightFor == null) return;

        var eye = highlightFor.Entity?.Pos;
        if (eye == null
            || eye.SquareDistanceTo(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5) > GuidanceRange * GuidanceRange
            || IncompleteCount() == 0)
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
            return;
        }

        Ghost();
    }

    /// <summary>
    /// The ghosts, refreshed off the same walk and the same tick as the coloured overlay. EMPTY cells only:
    /// a wrong block is already standing there, so the wanted one drawn in the same cell would z-fight it
    /// face for face and neither would be legible - vanilla reddens those, and red on a block you can see is
    /// the whole message there.
    /// <para>
    /// Created lazily. A tower nobody has clicked never allocates a renderer, and a plain line of a dozen
    /// finished towers has none at all.
    /// </para>
    /// </summary>
    private void Ghost()
    {
        if (Api is not ICoreClientAPI capi) return;

        try
        {
            var cells = new List<WantedCell>();
            foreach (var cell in MissingCells())
            {
                if (cell.Empty && cell.Ghost != null) cells.Add(cell);
            }

            ghosts ??= new StructureGhostRenderer(capi, Pos);
            ghosts.Cells = cells;
        }
        catch (Exception e)
        {
            // Same rule as the highlight above: a cosmetic aid must never take the client down. Dropping the
            // renderer leaves the player with exactly the overlay he had before this shipped, and clearing
            // highlightFor is what stops the 500 ms tick coming straight back for a second helping - a
            // wildcard that resolves to nothing will fail every time it is asked.
            DropGhosts();
            highlightFor = null;
            capi.Logger.Warning("Ropeway: could not ghost the tower at {0}: {1}", Pos, e.Message);
        }
    }

    private void DropGhosts()
    {
        ghosts?.Dispose();
        ghosts = null;
    }

    public void ClearHighlights(IPlayer byPlayer)
    {
        highlightFor = null;
        DropGhosts();
        if (Api is ICoreClientAPI && byPlayer != null) highlightedStructure?.ClearHighlights(Api.World, byPlayer);
    }

    /// <summary>A cell of this tower's pattern that is not built yet, and the block that goes in it.</summary>
    /// <param name="Pos">World position of the cell.</param>
    /// <param name="Ghost">The one block to draw here, or null when there is no one block - see <see cref="Wanted"/>.</param>
    /// <param name="Name">What to call that cell in the panel. Null when the wildcard resolves to nothing.</param>
    /// <param name="Empty">Whether the cell is air. False means a WRONG block is standing in it.</param>
    public readonly record struct WantedCell(BlockPos Pos, Block Ghost, string Name, bool Empty);

    /// <summary>
    /// Block number to (the block to ghost, the name to call it), resolved once per footing. Both halves are
    /// a <c>SearchBlocks</c> wildcard scan, which is why this is cached rather than asked per cell per tick.
    /// </summary>
    private Dictionary<int, (Block Ghost, string Name)> wantedByNumber;

    private StructureGhostRenderer ghosts;

    /// <summary>
    /// Every cell of this tower's pattern that does not hold what it wants, with the block that does.
    /// <para>
    /// Its own walk of <c>TransformedOffsets</c> rather than
    /// <see cref="MultiblockStructure.InCompleteBlockCount"/>, and the reason is the engine's signature:
    /// <c>PositionMismatchDelegate</c> is <c>(Block haveBlock, AssetLocation wantCode)</c> and never says
    /// WHERE, which is the one thing a ghost needs. The wildcard it matches against comes out of
    /// <see cref="MultiblockStructure.BlockNumbers"/> inverted, which is byte for byte what
    /// <c>InitForUse</c> builds its own private <c>BlockCodes</c> from - so this walk and the engine's cannot
    /// disagree about which cells are short, only about which of two wildcards sharing a number won, which
    /// nothing in this mod has.
    /// </para>
    /// <para>
    /// Client-side in practice (the ghosts and the block info panel), but it reads nothing client-only, so
    /// nothing here has to branch on side.
    /// </para>
    /// </summary>
    private List<WantedCell> MissingCells()
    {
        var cells = new List<WantedCell>();
        var offsets = structure?.TransformedOffsets;
        if (offsets == null || Api?.World == null) return cells;

        var codes = new Dictionary<int, AssetLocation>();
        foreach (var pair in structure.BlockNumbers) codes[pair.Value] = pair.Key;

        foreach (var offset in offsets)
        {
            if (!codes.TryGetValue(offset.W, out var wildcard)) continue;

            var pos = Pos.AddCopy(offset.X, offset.Y, offset.Z);
            var here = Api.World.BlockAccessor.GetBlockRaw(pos.X, pos.InternalY, pos.Z);
            if (WildcardUtil.Match(wildcard, here.Code)) continue;

            var wanted = Wanted(offset.W, wildcard);
            cells.Add(new WantedCell(pos, wanted.Ghost, wanted.Name, here.Id == 0));
        }

        return cells;
    }

    /// <summary>
    /// What to DRAW in a cell wanting <paramref name="wildcard"/>, and what to CALL it.
    /// <para>
    /// ONE NAME OR NO GHOST, and that is the whole of the rule. A wildcard whose matches all share a name is
    /// one block wearing four facings - the four <c>brace-*</c> variants are all "Ropeway Brace" - so there
    /// is something honest to draw. A wildcard whose matches do NOT share a name is a cell the player
    /// chooses the material for, which in this mod is exactly the post columns: an alternation over every
    /// log, plank and dressed stone in the game. Drawing whichever acacia log the registry happened to
    /// return first would read as a requirement rather than an example, so those cells get no ghost and keep
    /// the coloured cuboid the vanilla overlay puts there - which is what "your material here" looks like -
    /// and the panel names them "log, planks or dressed stone" instead of naming one wood. Decided by
    /// walking the matches until two names differ, which for that wildcard is the second entry.
    /// </para>
    /// <para>
    /// The ghost prefers this footing's own <c>side</c>, and it matters only because it is what gets drawn:
    /// <c>SearchBlocks("ropeway:brace-*")[0]</c> is whichever variant registered first, and a north brace
    /// ghosted on an east-facing tower stands a quarter turn out of the crossarm it is showing you how to
    /// build. The three asymmetric heads are already narrowed to one variant by
    /// <see cref="OwnTheHeadCell"/>, so for those this changes nothing.
    /// </para>
    /// <para>
    /// ponytail: the ghost is the block's PLACED mesh and nothing else, so a cell whose block draws part of
    /// itself per frame ghosts without that part - a bullwheel shows its cheeks and bearings and no rim,
    /// because the rim is <see cref="BullwheelRenderer"/>'s and there is no block entity in an empty cell to
    /// own one. Spinning ghost wheels is not worth a second renderer; the silhouette already says bullwheel.
    /// </para>
    /// </summary>
    private (Block Ghost, string Name) Wanted(int number, AssetLocation wildcard)
    {
        wantedByNumber ??= new Dictionary<int, (Block, string)>();
        if (wantedByNumber.TryGetValue(number, out var cached)) return cached;

        var matches = Api.World.SearchBlocks(wildcard);
        var resolved = ((Block)null, (string)null);

        if (matches.Length > 0)
        {
            var ghost = matches[0];
            foreach (var match in matches)
            {
                if (match?.Variant["side"] != side) continue;
                ghost = match;
                break;
            }

            var name = new ItemStack(ghost).GetName();
            foreach (var match in matches)
            {
                if (new ItemStack(match).GetName() == name) continue;
                name = Lang.Get("ropeway:cell-any");
                ghost = null;
                break;
            }

            resolved = (ghost, name);
        }

        wantedByNumber[number] = resolved;
        return resolved;
    }

    /// <summary>
    /// What the player still has to place, as counts by block name. The panel used to print the bare number
    /// <c>InCompleteBlockCount</c> returns - "15 blocks missing" - and left him to read fifteen coloured
    /// boxes and guess which. Ordered by the structure's own cell list, so a station's crossarm reads left to
    /// right and its leg top to bottom, the order the overlay is standing in.
    /// </summary>
    private List<(string Name, int Count)> MissingByName()
    {
        var order = new List<(string, int)>();
        var index = new Dictionary<string, int>();

        foreach (var cell in MissingCells())
        {
            if (cell.Name == null) continue;

            if (index.TryGetValue(cell.Name, out var at)) order[at] = (cell.Name, order[at].Item2 + 1);
            else
            {
                index[cell.Name] = order.Count;
                order.Add((cell.Name, 1));
            }
        }

        return order;
    }

    /// <summary>
    /// Half-thickness of the drawn cable, in blocks. Public because the cabin's jaw is authored closed on
    /// this surface with 0.04 unit of clearance, so the two drift apart silently if only one of them moves -
    /// <c>RopewayAssetContractTests.TheCabinFitsThroughTheTower</c> is what notices.
    /// </summary>
    public const float CableRadius = 0.06f;

    /// <summary>
    /// Blocks between the haul rope's two strands - the going strand the cabin hangs on and the return strand
    /// stacked above it. DERIVED off the wheel and nothing else, which is the point of writing it as an
    /// expression: <see cref="WrapPath"/> puts the groove tangent to the going strand at the BOTTOM of the
    /// bullwheel, so a rope that wraps 180 degrees round it leaves at the TOP, one wheel DIAMETER up.
    /// 1.3263 blocks.
    /// <para>
    /// A literal here is the whole failure mode this constant exists to prevent - re-author
    /// <c>bullwheelrim.json</c> and the loop has to follow the rim rather than drift off it.
    /// <c>TheTwoStrandsAreOneCurveAWheelApart</c> is what holds that, and it reads the rim rather than this.
    /// </para>
    /// <para>
    /// There is no second cabin, no second <c>Travelled</c> and no second corridor. The return strand is the
    /// half a loop that nothing hangs on: the same polyline, plus this in Y, drawn one more time. Everything
    /// that decides where a cabin is - <see cref="RopewayLine.PositionAt"/>, <c>IsSpanClear</c>, the rail,
    /// the jaw - is untouched by it.
    /// </para>
    /// <para>
    /// AT A PLAIN END TOWER the strand comes in at this same height and ENDS ON THE SHOE, which is the
    /// element <c>pylonhead.json</c> already carries for it - <c>returnshoe</c>'s top face is
    /// <c>ReturnLift - CableRadius</c> by construction, so the rope lands on its saddle and stops. It used to
    /// converge onto the sheave instead, over the <see cref="SpanMath.TrimForTowers"/> window, and that ramp
    /// is what drove the loop through a parked cabin's grip: <see cref="OnTesselation"/> carries the numbers
    /// and <c>TheReturnStrandStaysAWheelAboveTheCabinOnEveryTopology</c> is what holds it shut. The two
    /// answers the pinch was chosen over are unchanged and still cost what they cost - a smaller sheave has
    /// to have a 0.663-block radius against a 0.325-block throat and would be a new block, and asking whether
    /// the LINE has a terminal anywhere needs persisted state <see cref="OnTesselation"/>'s two- or
    /// three-tower <see cref="LocalLine"/> cannot see. Ending on the shoe needs neither.
    /// </para>
    /// </summary>
    public const double ReturnLift = 2 * BullwheelRenderer.WrapRadius;

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

        // A SHAFT draws none of this, and the reason is that a haul loop's geometry never changes while an
        // elevator's rope has MOVING ENDS. Everything below is chunk mesh, which is only affordable because the
        // cabin is a SLIDER on a rope whose two ends are wheels; stand the machine on end and the car becomes
        // the rope's own end, so the going strand is `H - travelled` long and the return strand `travelled`,
        // both changing every tick. Nor can the rope simply run past the car: on a level span it passes over the
        // cabin in open air, but on a vertical one it is directly above the roof centre, so a strand continuing
        // below the jaw runs down through the roof and out through the passengers.
        // `Lift` is the second half of the same fact - it offsets in +Y, which on a vertical leg is ALONG the
        // rope, so the two strands would be collinear and z-fight for the whole shaft. Generalising it would
        // move the shipped strand separation at every pitch; not calling it is free.
        // ShaftRenderer draws the two strands and the counterweight per frame off the cabin's synced Pos.Y, and
        // the wrap over the head sheave is authored geometry on shaftsheave.json because it never moves.
        if (IsShaft) return replacedDefault;

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

        // Which SIDE of the tower this iteration is drawing, 0 or 1 - not the peer index, which on a
        // three-tower mini-line is 0 and 2 and so has the same parity twice. It only decides which end of
        // JointPhase's alternation each run starts on: both sides leave the sheave from the same point, so
        // their first boxes are coplanar until one of them is drawn a fiftieth of a unit thinner. See BuildRun.
        var phaseFrom = 0;

        for (var peer = 0; peer < line.Towers.Length; peer++)
        {
            if (peer == me) continue;

            // THE LOOP, and it is one more call over the same list. The return strand cannot scissor away
            // from the going one at a corner because there is no second curve to scissor: PositionAt's bend
            // is horizontal, so a path raised by a constant is the SAME plan curve rather than an offset of
            // it. Lateral stacking is what cannot do this - 2*rho on the inside of the tightest bend the mod
            // draws (radius of curvature 1.317 blocks at a right angle) has radius -0.009 and cusps.
            //
            // The constant is CONSTANT, at every tower including a plain end one, and that is a fix rather
            // than a tidy-up. The lift used to ramp back to zero at a tower with one span and no wheel, so
            // the two strands converged onto the sheave - and the cabin parks on that sheave and departs
            // along that ramp. The jaw's top plate stands w +0.0625..+0.15 and the strand carries its own
            // 0.06 either side, so a ramp is inside cabin metal for 0.2075 of its 1.3263 blocks of lift:
            // 0.77 blocks of travel, measured, every trip, both ways. There is no window to start the ramp
            // in either - travel is clamped to the anchor and the plate reaches 0.131 blocks past it, so the
            // cabin sweeps every point a ramp could occupy at any span length. What ends the strand at such
            // a tower instead is the shoe it has been riding all along; see ReturnLift.
            var going = HalfSpanPath(line, me, peer);
            Emit(mesher, BuildRun(going, CableRadius, CableRadius, rope, phaseFrom: phaseFrom));
            Emit(mesher, BuildRun(Lift(going, ReturnLift), CableRadius, CableRadius, rope, phaseFrom: phaseFrom));
            if (metal == null)
            {
                phaseFrom ^= 1;
                continue;
            }

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
            Emit(mesher, BuildRun(RailPath(line, me, peer, RailOffset), RailHalfWidth, depth, metal, phaseFrom: phaseFrom));
            Emit(mesher, BuildRun(RailPath(line, me, peer, -RailOffset), RailHalfWidth, depth, metal, phaseFrom: phaseFrom));
            phaseFrom ^= 1;
        }

        // The wrap, and everything that carries the wheel to wherever the LINE has put it. A property of the
        // TOWER rather than of a span, so it is outside the loop: one arc per terminal, drawn once.
        if (!WearsABullwheel) return replacedDefault;

        var dead = DeadSide;
        Emit(mesher, BuildRun(WrapPath(dead), CableRadius, CableRadius, rope, turnsVertically: true));
        if (metal == null) return replacedDefault;

        // Where the renderer is about to stand the wheel, read from the SAME function the renderer reads, so
        // the brackets end on the hub by construction rather than by two files agreeing. Zero on a tower with
        // no rope on it at all, which draws nothing.
        var hub = BEBullwheel.WrapOffset(dead, Spans.Count);
        if (hub.Length() < 1e-9) return replacedDefault;

        // Across the LINE - the perpendicular of the path's own tangent at this tower, which at a terminal is
        // its single leg and at a through station is the corner's bisector. Same source as the rail's, so a
        // wheel placed a quarter turn out still gets its brackets in the plane the line decides.
        var tangent = line.DirectionAt(line.Cumulative[me]);
        var plan = Math.Sqrt(tangent.X * tangent.X + tangent.Z * tangent.Z);
        if (plan < 1e-9) return replacedDefault;

        var across = new Vec3d(-tangent.Z / plan, 0, tangent.X / plan);

        // A fiftieth of a unit narrower than the cheek column it stands in, and the reason is JointPhase's:
        // the bearing cap, the bearing stand and the sheave cheek all present a face at that exact x, and a
        // plate flush with them would be 11.5 unit^2 of z-fight against the cap alone.
        Emit(mesher, BuildRun(BracketPath(across, hub, RailOffset), RailHalfWidth - JointPhase, RailHalfDepth, metal));
        Emit(mesher, BuildRun(BracketPath(across, hub, -RailOffset), RailHalfWidth - JointPhase, RailHalfDepth, metal));

        return replacedDefault;
    }

    /// <summary>
    /// The same polyline raised by <paramref name="dy"/> - the return strand, which is the going strand and a
    /// constant and nothing else. NO position term: the ramp this used to carry at a plain end tower drew the
    /// strand through the parked cabin's grip, and <see cref="OnTesselation"/> has the numbers.
    /// <para>
    /// Public for the same reason <see cref="HalfSpanPath"/> is: it is pure, and it is the whole of the claim
    /// that there is only one curve here.
    /// </para>
    /// </summary>
    public static List<Vec3d> Lift(IReadOnlyList<Vec3d> points, double dy)
    {
        var lifted = new List<Vec3d>(points.Count);
        foreach (var p in points) lifted.Add(new Vec3d(p.X, p.Y + dy, p.Z));
        return lifted;
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
    /// <para>
    /// Public for the same reason <see cref="WrapPath"/> and <see cref="BuildRun"/> are: it is pure, and the
    /// no-scissoring claim is about the points it hands back at a real corner rather than about anything a
    /// render can show. <c>TheTwoStrandsAreOneCurveAWheelApart</c>.
    /// </para>
    /// </summary>
    public static List<Vec3d> HalfSpanPath(RopewayLine line, int me, int peer)
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
    /// Chords in the FULL turn the wrap's arc is half of. EVEN, and the reason changed when the ring did: it
    /// used to be so <see cref="BuildRun"/>'s alternating joint phase closed round a ring, and there is no
    /// closure any more. What needs even now is the TOP TANGENT - an odd chord count puts no chord midpoint
    /// at pi, so the rope would leave the wheel off the return strand. Sixteen departs from the true circle
    /// by 0.20 units, a tenth of the cable's own thickness.
    /// </summary>
    private const int WrapChords = 16;

    /// <summary>
    /// The haul rope at a terminal: out of the sheave along the dead side, HALF WAY ROUND the bullwheel's
    /// groove and away along the return strand, in blocks relative to the sheave. One polyline, so it is one
    /// <see cref="BuildRun"/> call. Null at a tower with no dead side, which draws no wrap at all.
    /// <para>
    /// A 180-DEGREE ARC, and it used to be a closed ring. The ring's own comment said why - "a second strand
    /// here would be a cable the whole length of the line that nothing hangs on" - and that is now false: the
    /// second strand is drawn, the whole length of the line, <see cref="ReturnLift"/> above the first, and
    /// nothing hangs on it because it is the half of the loop coming back. So the collapse is undone and the
    /// wrap is the real thing: in low, round, out high. It is also CHEAPER - both stubs are collinear with
    /// the chord they meet, so <see cref="OnTheLine"/> merges twelve points into NINE boxes against the
    /// ring's sixteen.
    /// </para>
    /// <para>
    /// CHUNK MESH rather than something the renderer turns with the rim, which costs nothing per frame and
    /// is honest: <see cref="BuildBox"/> flat-samples its UVs, so the cable carries no lengthwise detail at
    /// all and a static arc on a spinning rim is indistinguishable from one that turns with it.
    /// </para>
    /// <para>
    /// The vertices sit on a circle of <c>rho / cos(pi/n)</c> so the chord MIDPOINTS - not the corners -
    /// land on rho. The FIRST chord's midpoint is then exactly on the going strand's centreline and the LAST
    /// chord's exactly on the return strand's, which is what forces <see cref="ReturnLift"/> to be a wheel
    /// diameter and not a number somebody picked. Both end chords come out horizontal - cos(15pi/16) equals
    /// cos(17pi/16) - so both stubs are collinear with them and merge.
    /// </para>
    /// </summary>
    public static List<Vec3d> WrapPath(Vec3d dead)
    {
        if (dead == null) return null;

        var vertex = BullwheelRenderer.WrapRadius / Math.Cos(Math.PI / WrapChords);

        // The sheave itself, exactly, the same way HalfSpanPath starts: the rope leaves the throat here.
        var points = new List<Vec3d>(WrapChords / 2 + 3) { new() };
        for (var k = 0; k <= WrapChords / 2 + 1; k++)
        {
            // Measured from straight down, so the first chord straddles the bottom of the wheel and the last
            // straddles the top - half a turn, which is what a terminal wraps.
            var angle = (2 * k - 1) * Math.PI / WrapChords;
            var along = BullwheelRenderer.WrapOut + vertex * Math.Sin(angle);

            points.Add(new Vec3d(
                dead.X * along,
                BullwheelRenderer.WrapRadius - vertex * Math.Cos(angle),
                dead.Z * along));
        }

        // ...and back to the tower on the return strand, which is where this tower's own lifted half-span
        // starts. No free end anywhere: the loop leaves the wheel and goes back down the line.
        points.Add(new Vec3d(0, ReturnLift, 0));
        return points;
    }

    /// <summary>
    /// One of the two side plates that carry the wheel from its bearing on the sheave cheek to wherever the
    /// line has stood its hub, in blocks relative to the sheave. At a terminal that is one cell out along the
    /// dead side and <c>WrapDrop</c> down - 17.5 units at 23.9 degrees below the lay shaft, which reads as
    /// the chain case down to the sprocket on a drive station and as the carriage tie back to the
    /// counterweight head on a tension one. At a station the line runs THROUGH it is a vertical strut up to
    /// the hold-down sheave. One function, because it is one part: the bracket between the bearing and the
    /// hub.
    /// <para>
    /// <paramref name="hub"/> comes from <see cref="BEBullwheel.WrapOffset"/> - the SAME function the
    /// renderer's matrix reads - so the bracket ends on the axle by construction rather than by two files in
    /// two lanes agreeing about a sign.
    /// </para>
    /// <para>
    /// It takes the RAIL's lateral offset and cross-section because those are the cheeks' own columns:
    /// <c>sheavecheekwest</c> and the bearing stand and cap above it all occupy x 3.2..5.4 on both head
    /// shapes. Offset across the LINE rather than across the block's facing, like everything else drawn here,
    /// so a wheel placed a quarter turn out still gets a bracket in the plane the line decides.
    /// </para>
    /// </summary>
    private static List<Vec3d> BracketPath(Vec3d across, Vec3f hub, double lateral)
    {
        // Blocks above the anchor, which is where the axle rests and the shipped bearings hold it.
        var shaft = BullwheelRenderer.RimPivotY - 0.5;

        return new List<Vec3d>
        {
            new(across.X * lateral, shaft, across.Z * lateral),
            new(hub.X + across.X * lateral, shaft + hub.Y, hub.Z + across.Z * lateral)
        };
    }

    /// <summary>
    /// A chain of boxes along a polyline, in coordinates local to this tower's SHEAVE. The cable, both
    /// station rails, the wrap and both outriggers are all calls to this, which is the point: everything the
    /// cable had to learn the hard
    /// way - the face count the chunk tesselator loops over, the colour maps it indexes per face, the
    /// flat-sampled UV that stopped the striping - is solved once here and inherited rather than copied.
    /// Null when nothing survives the degenerate check.
    /// <para>
    /// <paramref name="phaseFrom"/> is which end of <see cref="JointPhase"/>'s alternation the run STARTS on,
    /// and it exists for the joint the alternation cannot see: the two runs a two-span tower draws leave the
    /// sheave from the same point in different directions, so their first boxes overlap in plan and - both
    /// being box 0 - present their up and down faces in one plane. Measured off the corner tower this
    /// paragraph was written for: four pairs (cable, return strand and both rail cheeks), 0.028 to 0.031
    /// unit^2 each, which is one JointPhase joint's worth apiece. Invisible until the mesh plumbing was fixed,
    /// because a corner tower used to draw nothing at all.
    /// </para>
    /// </summary>
    public static MeshData BuildRun(
        IReadOnlyList<Vec3d> points, float radiusX, float radiusY, TextureAtlasPosition texPos,
        bool turnsVertically = false, int phaseFrom = 0)
    {
        if (points == null) return null;

        MeshData run = null;
        var start = 0;
        var emitted = phaseFrom;
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

        // ...and THIS is what lets a run be more than one box. The three per-face side arrays above are
        // allocated (6 long) and filled, but their *Count fields are still 0: GetCube never sets
        // TextureIndicesCount, SetTexPos writes TextureIndices[0..Length) without touching it, and
        // WithColorMaps sizes the two colour-map arrays without touching ColorMapIdsCount either. Only
        // XyzFaces has a real count, because AddXyzFace maintains one.
        //
        // MeshData.AddMeshData - which BuildRun calls once per extra box - copies each side array by its
        // *Count and NOT by its Length (MeshData.addMeshDataEtc, 1.22.1 MeshData.cs:1028-1046). So a run of
        // N boxes reached the chunk tesselator with XyzFacesCount = 6N and 6-long side arrays, and
        // JsonTesselator.AddJsonModelDataToMesh indexes TextureIndices[l] and Season/ClimateColorMapIds[l]
        // for l < XyzFacesCount - IndexOutOfRangeException at l = 6, on the tesselation thread, caught and
        // logged per block entity by JsonTesselator.Tesselate. Vertices commit per face, so box 1 survived
        // on screen and every later box AND every later run of that OnTesselation call was never built.
        // In the world that was: a corner tower (29-30 boxes per run) drew nothing at all, a terminal lost
        // its wrap past the first stub and both brackets with it, and a straight tower - whose runs are one
        // box each, the one case where 6 faces and a 6-long array line up - looked perfect. Hence "mostly
        // worked", "half of it disappears" on a chain, "the top rope doesn't reach the bullwheel", and a
        // bend that has always been correct and has never once been drawn.
        //
        // Assigned rather than Add*()-ed: the arrays are already populated, and AddColorMapIndex/AddTextureId
        // would append a SECOND six. One box is always six faces, so XyzFacesCount is the count all three
        // share. EveryBoxOfARunCarriesTheSideArrayEntriesTheTesselatorIndexesPerFace is what holds this shut,
        // over 2-, 9- and 30-box runs - the shipped test for this failure class asserted exactly the right
        // thing on BuildHalfCable, which is the ONE-box case and the only case that was ever right.
        mesh.TextureIndicesCount = mesh.XyzFacesCount;
        mesh.ColorMapIdsCount = mesh.XyzFacesCount;
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
        DropGhosts();
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

            // ...and WHICH blocks, which is the half the number never carried. A count sends the player to
            // the overlay, and the overlay is fifteen translucent cuboids in the wanted block's average
            // colour - it says where, it cannot say what, and vanilla's highlight API has no shape option
            // that could (see StructureGhostRenderer). This is the naming channel and the ghosts are the
            // spatial one; either alone still leaves a guess.
            foreach (var (name, count) in MissingByName())
            {
                dsc.AppendLine(Lang.Get("ropeway:tower-missing-cell", count, name));
            }

            // The frame is one block deep and symmetric, so nothing about a placed footing shows which way
            // its crossarm will go until the braces are up. Naming the passage axis is what lets a player
            // face the tower down the line BEFORE building it rather than after.
            dsc.AppendLine(Lang.Get(
                "ropeway:blockinfo-passage",
                Lang.Get("game:facing-" + PassageFacing.Code),
                Lang.Get("game:facing-" + PassageFacing.Opposite.Code)));

            // ...and WHICH SIDE the machinery goes, which the passage line alone cannot say: the two variants
            // that share a bearing read identically there and put the leg on opposite sides of the posts. It
            // is the one build decision a station offers that nothing in the world showed - the answer was
            // "stand on the other side of the footing and place it again", and a player has no way to guess
            // that from a block whose two faces are the same. See MachineLegSide.
            var leg = MachineLegSide;
            if (leg != null)
            {
                dsc.AppendLine(Lang.Get(
                    "ropeway:blockinfo-machineleg",
                    Lang.Get("game:facing-" + leg.Code),
                    Lang.Get("game:facing-" + leg.Opposite.Code)));
            }

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
