using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// The shaft's rope and its counterweight. Two strands and one mass, drawn per frame off the cabin's own
/// synced <c>Pos.Y</c>, on the head sheave's block entity.
/// <para>
/// WHY THIS EXISTS AT ALL, in one line: a ropeway's rope has FIXED geometry and an elevator's rope has MOVING
/// ENDS. <c>BEPylonBase.OnTesselation</c> can put a haul loop in the chunk mesh because the cabin is a slider
/// on a rope whose two ends are wheels and nothing about the curve ever changes; on a hoistway the car IS the
/// rope's end, so the going strand is <c>H - travelled</c> long and the return strand <c>travelled</c>, both
/// changing every tick. You cannot re-tesselate a chunk per tick, and you cannot let the rope run past the
/// car either - on a level span it passes over the cabin in open air, but on a vertical one it is directly
/// above the roof centre and a strand continuing below the jaw goes down through the roof and the seats.
/// </para>
/// <para>
/// THE ROPE IS OPEN, which is what a 1:1 traction elevator actually has: car - head sheave - counterweight. It
/// is not a loop, and that is a proof rather than a preference. A 180 degree wheel closing the loop at the
/// bottom spans the lane, so its radius is <c>lane/2</c> and its centre sits at the bottom anchor - which puts
/// its rim <c>r</c> BELOW that anchor over a plan range containing the parked car's roof. It clears the roof
/// only if its centre is past the car's nose (<c>r &gt;= 2.0</c>) and fits under the strand only if
/// <c>r &lt;= 1.0</c>. Both cannot hold, at any radius. So the strand terminates on the counterweight instead
/// of coming back round, and the return strand stops being the half of a loop that nothing hangs on.
/// </para>
/// <para>
/// THE COUNTERWEIGHT IS GEOMETRY WITH NO OWNERSHIP - not a block, not an entity, no <c>Travelled</c>, no
/// persistence, no despawn path, no corridor and no seat. Its rope point is the pure function
/// <c>anchorTop + anchorBot - carRopeY</c>, so it is the car's exact mirror and the two meet level at the
/// shaft's midpoint by construction rather than by tuning. Nothing has to keep it in step with anything,
/// because there is nothing to keep.
/// ponytail: if cargo weight ever lands and the imbalance has to be SIZED, the knob is the drawn mass and
/// <c>RopewayPower.Resistance</c>'s existing <c>cargo</c> argument, not a new block.
/// </para>
/// <para>
/// Copied from <see cref="BullwheelRenderer"/>, including the frustum cull and the shader path, and it is a
/// second <c>IRenderer</c> on the same block entity rather than more work inside that one: the rim spins and
/// these do not, the rim is culled about its own wheel and these span the whole shaft, and one class that did
/// both would carry a mode flag through every line of it.
/// </para>
/// </summary>
public sealed class ShaftRenderer : IRenderer, IDisposable
{
    /// <summary>
    /// Blocks from the shaft axis to the counterweight's lane - twice <see cref="BEBullwheel.ShaftWrapOut"/>,
    /// because a 180 degree wrap leaves the two strands a wheel DIAMETER apart. The wheel decides it, exactly
    /// as <c>BEPylonBase.ReturnLift</c> is decided by the bullwheel on a ropeway.
    /// </summary>
    public const double Lane = 2 * BEBullwheel.ShaftWrapOut;

    /// <summary>
    /// Blocks from the counterweight's rope point down to the bottom of the mass. The CAR's own figure -
    /// <see cref="SpanMath.ShaftCarDrop"/> = 3.5 - so the two bodies are exact mirrors: with the car parked at
    /// one stop the weight sits at the other, its shoe on the guide, and neither number is tuned. The shape is
    /// authored with its rope point at the top of a 56-unit column for the same reason. THE SAME constant
    /// rather than the same arithmetic written twice, since <c>IsSpanClear</c> certifies the car's body with
    /// it too: an equality between three bodies that has to be re-derived by hand at each of them is an
    /// equality that drifts.
    /// </summary>
    public const double WeightDrop = SpanMath.ShaftCarDrop;

    /// <summary>
    /// Blocks from the wheel's own tangent point back down the strand to where the AUTHORED wrap starts -
    /// half of its first chord. <c>gen_shaftsheave.py</c> lays the arc's chord MIDPOINTS on the rope circle,
    /// exactly as <c>BEPylonBase.WrapPath</c> does, so the end chords straddle the two tangent points and
    /// half of each hangs below them. A strand run all the way to the hub would lie inside that half for
    /// 0.298 blocks with both boxes on the same two planes - 9.2 unit^2 of z-fight, per strand, measured by
    /// the render's own coplanar audit. Ending here butts the two instead.
    /// </summary>
    public static readonly double WrapChord = BEBullwheel.ShaftWrapOut * Math.Tan(Math.PI / 16);

    private readonly ICoreClientAPI capi;
    private readonly BlockPos pos;
    private readonly Matrixf modelMat = new();

    private MeshRef strand;
    private MeshRef weight;

    // The whole of the drawn state, replaced as one reference on the poll tick so a frame can never read half
    // of it - the same reason BullwheelRenderer.Offset is a Vec3f and not three floats.
    private sealed record Rig(
        EntityRopewayCabin Cabin, double AnchorSum, double LaneX, double LaneZ, double MidY, float Reach);

    private Rig rig;

    public ShaftRenderer(ICoreClientAPI capi, BlockPos pos, MeshData strandMesh, MeshData weightMesh)
    {
        this.capi = capi;
        this.pos = pos;
        if (strandMesh != null) strand = capi.Render.UploadMesh(strandMesh);
        if (weightMesh != null) weight = capi.Render.UploadMesh(weightMesh);
    }

    public double RenderOrder => 0.5;

    public int RenderRange => 64;

    /// <summary>
    /// The line and the car this sheave is drawing for, from the block entity's 500 ms poll. Everything the
    /// rope needs that is not the car's live height is fixed for as long as the line is: the two anchors sum
    /// to a constant, so the two strands' lengths always add up to the same number, and the lane is the head's
    /// own facing. Null anything and the shaft simply draws nothing - a line with no cabin on it has no rope,
    /// because an open rope terminates on the car.
    /// </summary>
    public void Track(RopewayLine line, EntityRopewayCabin cabin)
    {
        if (line?.Anchors == null || line.Anchors.Length < 2 || line.ShaftFacing == null || cabin == null)
        {
            rig = null;
            return;
        }

        var top = line.Anchors[line.Anchors.Length - 1];
        var bottom = line.Anchors[0];
        rig = new Rig(
            cabin,
            top.Y + bottom.Y,
            line.ShaftFacing.Normalf.X * Lane,
            line.ShaftFacing.Normalf.Z * Lane,
            (top.Y + bottom.Y) / 2,
            // The whole hoistway, because that is what the two strands span. A sphere on the SHEAVE the way
            // BullwheelRenderer's is would drop the rope for anyone standing at the bottom of a deep shaft
            // with the car parked at the top - neither end in view and forty blocks of rope beside them.
            (float)((top.Y - bottom.Y) / 2 + Lane));
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var held = rig;
        if (held == null || strand == null) return;

        // The rope point the car hangs from, which is the anchor line at the car's own height. Pos is SYNCED
        // and interpolated; Travelled is not (Entity.ToBytes writes Attributes only `if (!forClient)`), which
        // is why this is read off the position rather than off the route state.
        var carRope = held.Cabin.Pos.Y + held.Cabin.HangDropDefault;
        var weightRope = held.AnchorSum - carRope;

        // Both tangents are at the hub's own height and both are vertical: the wheel's centre stands
        // ShaftWrapOut off the shaft axis with a rope radius of exactly that, so the going strand touches it at
        // plan 0 and the return strand at plan Lane, and the arc between them is authored on the block.
        var hub = pos.InternalY + BullwheelRenderer.RimPivotY - WrapChord;

        var render = capi.Render;
        if (!render.DefaultFrustumCuller.SphereInFrustum(pos.X + 0.5, held.MidY, pos.Z + 0.5, held.Reach)) return;

        var camera = capi.World.Player.Entity.CameraPos;
        render.GlDisableCullFace();
        render.GlToggleBlend(true);

        var prog = render.PreparedStandardShader(pos.X, pos.InternalY, pos.Z);
        prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;

        Strand(prog, camera, 0, 0, carRope, hub);
        Strand(prog, camera, held.LaneX, held.LaneZ, weightRope, hub);

        if (weight != null)
        {
            prog.ModelMatrix = Local(camera)
                .Translate((float)held.LaneX, (float)(weightRope - WeightDrop - pos.InternalY), (float)held.LaneZ)
                .Values;
            render.RenderMesh(weight);
        }

        prog.Stop();
    }

    /// <summary>
    /// One strand, as the authored one-block rope column scaled in Y. A box rather than a chain of them
    /// because a vertical run has no bend to follow: <c>BEPylonBase.BuildRun</c>'s sampling exists for the
    /// window <c>PositionAt</c> bends in, and <c>LegOf</c> returns null on a vertical span precisely because
    /// there is no bearing there to bend.
    /// </summary>
    private void Strand(IStandardShaderProgram prog, Vec3d camera, double dx, double dz, double from, double to)
    {
        var length = to - from;
        if (length <= 0.01) return;

        prog.ModelMatrix = Local(camera)
            .Translate((float)dx, (float)(from - pos.InternalY), (float)dz)
            .Scale(1, (float)length, 1)
            .Values;

        capi.Render.RenderMesh(strand);
    }

    private Matrixf Local(Vec3d camera)
    {
        return modelMat.Identity().Translate(pos.X - camera.X, pos.InternalY - camera.Y, pos.Z - camera.Z);
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        strand?.Dispose();
        strand = null;
        weight?.Dispose();
        weight = null;
    }
}
