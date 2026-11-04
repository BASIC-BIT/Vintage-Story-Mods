using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// The bullwheel: the big wheel on a STATION's crossarm centre cell, in place of the pylon head. It is on no
/// mechanical network - no <c>MPConsumer</c>, no membership, no resistance - and the intake is
/// <see cref="BlockDriveHousing"/> at the foot of the same station's machine leg. What joins the two is
/// GEOMETRY rather than a network: the shape's <c>hubaxle</c> runs at the rim's own rotation centre out to
/// the cell's east face, where the <c>layshaft</c> next door picks it up, so the wheel reads as driven by the
/// thing that is actually driving the line.
/// <para>
/// AT A TERMINAL IT DOES WRAP THE ROPE, and everywhere else it does not. This comment has said both, wrongly
/// and then honestly, so here is the arithmetic. <c>bullwheelrim.json</c> sweeps a radius of 9.6504 units
/// about its axle, so a wheel standing over the tower has its lowest swept point 0.443 blocks above the
/// rope's own surface - it turns BESIDE the rope and reads its speed. It cannot simply be lowered: a parked
/// cabin's jaw is a clamp closed ON the rope and its top plate stands 0.15 blocks above the rope's
/// centreline, and a wheel tangent to that rope from above cannot share a point with the clamp closed round
/// it. What buys the room is the axis nobody looked at - ALONG the line, past the tower. At a tower carrying
/// exactly one span the far side is DEAD: nothing ever passes there, the parked cabin's grip stops 0.13
/// blocks short of the tower centre and its roof is a full block below. So there the wheel stands one cell
/// out along that dead side and <see cref="BullwheelRenderer.WrapDrop"/> down, its groove lands on the going
/// strand's centreline, and <see cref="BEPylonBase.WrapPath"/> takes the rope half a turn round it and away
/// on the RETURN strand, <see cref="BEPylonBase.ReturnLift"/> above the one it arrived on - 0.146 blocks
/// clear of the parked grip in plan, 0.06 clear of the station's own soffit. That wheel DIAMETER is the
/// separation of the loop's two strands: the wheel decides it, not the other way round.
/// </para>
/// <para>
/// At a station the line runs THROUGH there is no dead side and no wrap is drawn - a ring dropped to the
/// going strand on either side of such a tower would have a passing cabin's grip inside it for a block of
/// travel, every trip, in both directions. It cannot stay where it rests either, because there the RETURN
/// strand runs through the middle of the rim. It goes UP by <see cref="BullwheelRenderer.HoldDownRise"/> and
/// becomes a hold-down sheave on the strand nothing rides on. Still not a wart: a terminal has a bullwheel
/// that turns the loop, and a tower in the middle of a line has a sheave that holds it.
/// </para>
/// <para>
/// It exists for one job: to TURN. A drive tower with a still wheel is what the trial failed on - the
/// wheel's silhouette is close enough to the pylon head's that only motion tells them apart at any real
/// distance - so the whole of this class is finding the line, reading its pooled drive speed, and handing
/// that to <see cref="BullwheelRenderer"/>. Server side it does nothing whatsoever.
/// </para>
/// <para>
/// No position registry of its own, and nothing looks a wheel up. The lookup only ever runs the other way:
/// a wheel is always exactly <see cref="SpanMath.SheaveHeight"/> above its footing, so <see cref="Tower"/>
/// finds the tower with one block-accessor call. The footing carried the mirror of that accessor while the
/// drive was up here, and it went when the drive came down - a tower has no reason to ask whether it is
/// wearing a wheel once the wheel decides nothing. A table keyed by wheel position would be a second thing
/// to keep in step with chunk loads, for a block that only ever draws itself.
/// </para>
/// <para>
/// Its block is a PLAIN <c>Block</c> - no <c>class</c> key at all. It was a <c>BlockMPBase</c> with a
/// connector rule and a placement hookup; with the intake gone there was nothing left in it, and an empty
/// subclass is a file to read at 3am for no reason.
/// </para>
/// <para>
/// ponytail: still <c>HorizontalOrientable</c>, so a wheel placed while facing the wrong way validates the
/// tower with its hub axle pointing at the braces rather than at the lay shaft. ACCEPTED, not fixed, and
/// now genuinely COSMETIC: the count went 2 to 5 when the stations landed and back to 3 when the two heads
/// were narrowed, so what is left loose is <c>pylonhead</c>, this block and <c>layshaft</c>. The real fix is
/// unchanged and is still ONE fix in one place - orient the crossarm cells from the footing below them, for
/// all of them at once. Bolting a private rule onto this one block would leave the bug and add a rule.
/// </para>
/// <para>
/// It used to be more than cosmetic, and that half is closed. The RIM's own pose no longer reads this block's
/// facing at all: <see cref="BullwheelRenderer.Yaw"/> is polled off the tower's
/// <see cref="BEPylonBase.LineTangent"/> on the tick below, so a wheel placed a quarter turn out has its
/// groove in the rope's plane anyway. The variant is left deciding one thing - which of the disc's two
/// 180-degree-symmetric branches the spin runs in - and that is a direction of rotation on a wheel with no
/// marked face.
/// </para>
/// <para>
/// ponytail: it was briefly the only fix for a second, STRUCTURAL bug, and that half is closed without it.
/// <c>MultiblockStructure</c> has no notion of ownership - <c>InCompleteBlockCount</c> asks only whether the
/// block at each offset matches a wildcard - so a second station 4.243 blocks away on a perpendicular facing,
/// or six on the opposite one, shared the whole machine leg, both structures validated, both
/// <c>DeclareLoad</c> calls wrote <c>Resistance</c> onto the SAME consumer so the last tick won, and both
/// lines read that one drive at full speed. <see cref="BEPylonBase.OwnTheHeadCell"/> ends it by narrowing
/// the leg's one facing-carrying cell to the footing's own side: a shared <c>drivehead</c> can face one way,
/// so it can satisfy one station. That refusal is safe on <c>drivehead</c> and <c>tensionhead</c> precisely
/// because they are new and asymmetric, and it is NOT safe here - a pylon head or a bullwheel is symmetric
/// along the rope axis, so a saved world's tower placed from the other side would go incomplete and
/// un-clickable over a block that looks perfectly right. The refusal on these three is only safe once
/// placement can no longer produce a wrong facing, which is M4's other half.
/// </para>
/// </summary>
public class BEBullwheel : BlockEntity
{
    /// <summary>
    /// The turning half of the wheel. Client only; the rest of the shape is chunk mesh. A DEFAULT since the
    /// shaft sheave landed - that block names <c>shapes/block/shaftrim.json</c> in its own <c>rimShape</c>
    /// attribute, because a 1.5-block rope radius is a different wheel and not a different renderer.
    /// </summary>
    public const string RimShape = "shapes/block/bullwheelrim.json";

    /// <summary>
    /// Blocks from the shaft axis to the head sheave's HUB - half the separation of the two strands, because a
    /// 180 degree wrap puts them a diameter apart. It is 1.5 and not the bullwheel's own 0.663 for a reason
    /// that has nothing to do with the wheel: the car and the counterweight have to pass each other, and the
    /// car's half-length is 2.0, so the lane has to be at least 2.0 plus half the weight plus clearance. 3.0 is
    /// the first value that puts the lane on a block centre, and this is half of it.
    /// </summary>
    public const double ShaftWrapOut = 1.5;

    private BullwheelRenderer renderer;
    private ShaftRenderer shaftRenderer;

    public RopewayModSystem ModSystem => Api?.ModLoader?.GetModSystem<RopewayModSystem>();

    /// <summary>The footing this wheel sits over, or null when it is standing on nothing.</summary>
    public BEPylonBase Tower =>
        Api?.World?.BlockAccessor.GetBlockEntity(Pos.DownCopy(SpanMath.SheaveHeight)) as BEPylonBase;

    /// <summary>
    /// Revolutions per second the wheel should be turning at: the whole LINE's pooled drive speed, not one
    /// housing's, because the wheel is a read-out of the rope and the rope is one loop. Every drive tower on
    /// a line therefore turns at the same rate, which is correct - they are all watching the same rope.
    /// </summary>
    public double LineSpeed()
    {
        var tower = Tower;
        if (tower == null) return 0;

        return BEPylonBase.DriveSpeedOn(ModSystem, RopewayLine.GetOrBuild(ModSystem, tower.Pos));
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (api is not ICoreClientAPI capi) return;

        var rim = Block?.Attributes?["rimShape"].AsString(RimShape) ?? RimShape;
        var cull = Block?.Attributes?["cullRadius"].AsFloat(BullwheelRenderer.CullRadius)
                   ?? BullwheelRenderer.CullRadius;

        renderer = new BullwheelRenderer(
            capi, Pos, RimMesh(capi, rim), BullwheelRenderer.YawFor(Block?.Variant["side"]), cull);
        capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "ropewaybullwheel");

        // A SHAFT sheave carries the machine's whole rope, because on a hoistway the rope has moving ends and
        // cannot be chunk mesh (BEPylonBase.OnTesselation says why). It lives here rather than on the footing
        // for the plain reason that this block entity is the only one in the mod that already registers a
        // renderer, disposes it, and polls the line on a tick - the shaft's rope needs all three and nothing
        // else.
        // An `if` and NOT an early return, which is what this was for one revision: everything below is the
        // BULLWHEEL's tick, so returning here would have left every wheel on every ropeway standing still.
        if (Block?.Attributes?["shaft"].AsBool() == true)
        {
            shaftRenderer = new ShaftRenderer(
                capi, Pos, ShaftMesh(capi, "shaftstrand"), ShaftMesh(capi, "shaftweight"));
            capi.Event.RegisterRenderer(shaftRenderer, EnumRenderStage.Opaque, "ropewayshaft");
        }

        // Polled rather than read per frame: LineSpeed walks the tower chain and the housing table, and a
        // wheel that picks up half a second late is a wheel nobody can tell picked up late. The pose rides
        // the same tick for the same reason and needs no listener of its own - the footing re-tesselates its
        // own chunk when a span is linked or cut, so the drawn wrap and the wheel can disagree for at most
        // half a second on the one tick a terminal stops being one.
        RegisterGameTickListener(
            _ =>
            {
                var tower = Tower;
                renderer.Speed = LineSpeed();

                // FAIL CLOSED: hold the last good pose rather than answering for a footing nobody could read.
                // WrapOffset's last branch is the zero vector, and on a SHAFT that is not a small error - the
                // shaft branch is the only one that can fire there (DeadSide is null by construction, Spans is
                // 1), so a single missed lookup snapped the rim 1.5 blocks off its hub and onto the going
                // strand while the authored arc, the hangers and both strands stayed put. `Tower` is a
                // block-accessor read of the footing SheaveHeight below, and a sheave and its footing can
                // straddle a chunk boundary, so null here is an ordinary chunk-load window rather than an
                // impossible state. A cut span still moves the wheel: the tower is there and its DeadSide
                // changed, which is the case this must NOT skip.
                if (tower == null) return;

                renderer.Offset = WrapOffset(
                    tower.DeadSide, tower.Spans.Count, tower.IsShaft ? tower.PassageFacing : null);

                // ...and the same source for the ORIENTATION, which is the half that never followed. On a
                // shaft the tangent is vertical and YawAlong falls back to the block's own facing - which is
                // the right answer there and not a degradation, because OwnTheHeadCell narrows
                // `shaftsheave-*` to the footing's side, so the sheave's variant IS the machine's heading.
                renderer.Yaw = BullwheelRenderer.YawAlong(
                    tower.LineTangent, BullwheelRenderer.YawFor(Block?.Variant["side"]));

                // The HANDLE only. The cabin's position is read per frame off Pos.Y - which is synced, unlike
                // Travelled - so the rope never lags the car; what is polled here is the O(loaded entities)
                // scan that finds it, which a rope cannot afford to do every frame.
                if (shaftRenderer == null) return;

                var line = RopewayLine.GetOrBuild(ModSystem, tower.Pos);
                shaftRenderer.Track(line, line == null ? null : EntityRopewayCabin.FindOn(Api.World, line));
            },
            500, 0);
    }

    /// <summary>
    /// Where the wheel stands, given the tower's dead side and how many spans it carries. Three poses, one
    /// per shape of rope over the tower, and each of them tangent to a strand:
    /// <list type="bullet">
    /// <item>a TERMINAL (one span, so there is a dead side): out along it by
    /// <see cref="BullwheelRenderer.WrapOut"/> and down by <see cref="BullwheelRenderer.WrapDrop"/>, groove
    /// on the going strand, and the drawn wrap goes half a turn round it and leaves on the return strand;</item>
    /// <item>a station the line runs THROUGH (two spans, no dead side): straight up by
    /// <see cref="BullwheelRenderer.HoldDownRise"/>, groove under the RETURN strand. It has to move, because
    /// where it rests the return strand runs through the middle of the rim;</item>
    /// <item>a tower with no rope on it: the zero vector.</item>
    /// </list>
    /// Pure, and therefore the one part of the pose the suite can look at. <see cref="BEPylonBase"/> reads it
    /// too, so the brackets it draws end on the axle by construction.
    /// </summary>
    /// <param name="shaftAxis">
    /// The head's own passage facing on a SHAFT, and null everywhere else - so every shipped caller passes
    /// nothing and gets the three poses above unchanged. It is a fourth pose and it has to come from the
    /// facing rather than from <c>DeadSide</c>, which is null at a vertical terminal by construction
    /// (<c>plan &lt; 1e-9</c>) and so cannot express it: the shaft's wheel stands OUT along the lane and not
    /// down, because the going strand is tangent to it at the shaft axis and the return strand
    /// <see cref="ShaftWrapOut"/> x 2 along the lane. It does not drop, because the hub is already on the drive
    /// bar's own line - <see cref="BullwheelRenderer.RimPivotY"/> is the height <c>layshaft</c> and
    /// <c>drivehead</c> run their bar at, so the shaft is one unbroken bar from the gearbox to the wheel.
    /// </param>
    public static Vec3f WrapOffset(Vec3d deadSide, int spans, BlockFacing shaftAxis = null)
    {
        if (shaftAxis != null)
        {
            return new Vec3f(
                (float)(shaftAxis.Normalf.X * ShaftWrapOut), 0, (float)(shaftAxis.Normalf.Z * ShaftWrapOut));
        }

        if (deadSide != null)
        {
            return new Vec3f(
                (float)(deadSide.X * BullwheelRenderer.WrapOut),
                -BullwheelRenderer.WrapDrop,
                (float)(deadSide.Z * BullwheelRenderer.WrapOut));
        }

        return spans >= 2 ? new Vec3f(0, BullwheelRenderer.HoldDownRise, 0) : new Vec3f();
    }

    private MeshData RimMesh(ICoreClientAPI capi, string path)
    {
        try
        {
            var shape = Shape.TryGet(capi, new AssetLocation("ropeway", path));
            if (shape == null) return null;

            capi.Tesselator.TesselateShape(Block, shape, out var mesh);
            return mesh;
        }
        catch (Exception e)
        {
            // A decorative wheel must never take the chunk down with it.
            capi.Logger.Warning("Ropeway: could not build the bullwheel rim at {0}: {1}", Pos, e.Message);
            return null;
        }
    }

    /// <summary>
    /// One of the shaft's two drawn parts, tesselated against THIS block's texture map - the same handshake
    /// the rim has, and the same trap: <c>TesselateShape(Block, ...)</c> builds a BLOCK texture source and
    /// never consults the shape's own, so every key these two shapes name has to be declared on
    /// <c>shaftsheave.json</c> or they render as the magenta checker.
    /// </summary>
    private MeshData ShaftMesh(ICoreClientAPI capi, string name)
    {
        return RimMesh(capi, "shapes/block/" + name + ".json");
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        // The block-info panel is this mod's handbook-on-the-block, and a wheel that describes a crossarm it
        // does not have is the one place a shaft would read as a mis-built ropeway.
        var shaft = Block?.Attributes?["shaft"].AsBool() == true;
        dsc.AppendLine(Tower == null
            ? Lang.Get(shaft ? "ropeway:shaftsheave-orphan" : "ropeway:bullwheel-orphan")
            : Lang.Get(shaft ? "ropeway:shaftsheave-what" : "ropeway:bullwheel-what"));
    }

    public override void OnBlockRemoved()
    {
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
        renderer?.Dispose();
        renderer = null;
        shaftRenderer?.Dispose();
        shaftRenderer = null;
    }
}
