using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Ropeway;

/// <summary>
/// The bullwheel: the big sheave the haul rope wraps, on the crossarm's centre cell in place of the pylon
/// head. It is PURELY DECORATION - no <c>MPConsumer</c>, no network membership, no resistance, nothing on
/// the mechanical network at all. The intake is <see cref="BlockDriveHousing"/>, off the tower entirely.
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
/// tower with its throat and station rails running across the line rather than along it. ACCEPTED, not
/// fixed: the pylon head has had exactly the same looseness since the pattern was written, so the real fix
/// is to orient the crossarm's centre cell from the footing below it for BOTH blocks, in one place - and
/// bolting a private rule onto the decorative half of the pair would leave the bug and add a rule.
/// </para>
/// </summary>
public class BEBullwheel : BlockEntity
{
    /// <summary>The turning half of the wheel. Client only; the rest of the shape is chunk mesh.</summary>
    public const string RimShape = "shapes/block/bullwheelrim.json";

    private BullwheelRenderer renderer;

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

        renderer = new BullwheelRenderer(capi, Pos, RimMesh(capi), BullwheelRenderer.YawFor(Block?.Variant["side"]));
        capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "ropewaybullwheel");

        // Polled rather than read per frame: LineSpeed walks the tower chain and the housing table, and a
        // wheel that picks up half a second late is a wheel nobody can tell picked up late.
        RegisterGameTickListener(_ => renderer.Speed = LineSpeed(), 500, 0);
    }

    private MeshData RimMesh(ICoreClientAPI capi)
    {
        try
        {
            var shape = Shape.TryGet(capi, new AssetLocation("ropeway", RimShape));
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

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        dsc.AppendLine(Tower == null ? Lang.Get("ropeway:bullwheel-orphan") : Lang.Get("ropeway:bullwheel-what"));
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
    }
}
