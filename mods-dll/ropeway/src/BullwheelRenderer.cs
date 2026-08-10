using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// Turns the bullwheel's rim. A plain block-entity renderer, and that IS the decision:
/// <c>MechBlockRenderer</c> (the machinery <c>mods-dll/flywheelpower</c> uses) is an INSTANCED renderer
/// registered with <c>MechanicalPowerMod</c> and driven per-device off <c>IMechanicalPowerRenderable</c> -
/// it exists to draw hundreds of axles off one draw call, and every device it draws must be a node on a
/// mechanical network. The bullwheel is deliberately not on the network any more, so there is no device for
/// it to enumerate and no <c>AngleRad</c> for it to read; adopting it would mean putting the wheel back on
/// the network purely to be drawn. One <c>IRenderer</c> per drive tower - of which a line has one or two -
/// is both smaller and the only one of the two that fits. Copied from vanilla's <c>QuernTopRenderer</c>.
/// <para>
/// The rim is a separate shape from the block, so the chunk tesselator keeps drawing the static half
/// (cheeks, rails, bearing housing) with normal chunk lighting and only the wheel is redrawn per frame.
/// </para>
/// </summary>
public sealed class BullwheelRenderer : IRenderer, IDisposable
{
    /// <summary>
    /// Blocks above the block's own bottom face, and the axle the rim turns on. Every element in
    /// <c>bullwheelrim.json</c> is authored about <c>rotationOrigin [8, 25.7, 8]</c> and the tesselator
    /// divides that by 16 unchanged, so the mesh stands a block and a half ABOVE the cell it belongs to and
    /// the block centre is nowhere near its axle. Spinning it about the block centre instead swings the whole
    /// wheel round a circle of radius 1.106 blocks, down through the cabin. The asset contract test ties this
    /// number to the shape, because nothing in the game would say the two had parted.
    /// <para>
    /// 25.7 rather than the 25.2 the wheel was first drawn at, and the extra half unit is the difference
    /// between the pose the shape is authored in and the circle it sweeps. A corner 9.6504 out from the axle
    /// reaches the bottom of the turn a twentieth of a revolution after the octagon's flat does, so an axle
    /// at 25.2 put the rim 0.45 unit inside the drive boss it is mounted on - visible only on a turning wheel,
    /// which is why both the generator and the test that were supposed to catch it measured the flat instead.
    /// </para>
    /// </summary>
    public const float RimPivotY = 25.7f / 16f;

    /// <summary>
    /// Blocks from the axle to the haul rope's own centreline once the rope is IN the groove: the rim's swept
    /// reach (9.6504 units - felloe5's corner, not its flat) plus the cable's own 0.96-unit half-thickness
    /// bedded on it. FORCED, not chosen: bed the rope on the felloe's flats instead and the corners stand
    /// 0.635 units proud through the rope's own cross-section on every quarter turn. Re-derived off the
    /// shipped rim by <c>TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach</c>.
    /// </summary>
    public const float WrapRadius = 10.6104f / 16f;

    /// <summary>
    /// Blocks the wrapped wheel drops so its groove lands on the rope. DERIVED rather than typed: the axle
    /// stands <see cref="RimPivotY"/> above the block's own bottom face, the rope runs through the block's
    /// centre half a block up, and the groove has to be <see cref="WrapRadius"/> above the rope. 0.4431
    /// blocks. Re-author the rim about a different axle and this follows it instead of drifting off it.
    /// </summary>
    public const float WrapDrop = RimPivotY - 0.5f - WrapRadius;

    /// <summary>
    /// Blocks the wrapped wheel stands out along the DEAD side of a terminal - the side of the tower past
    /// which nothing ever runs. ONE CELL, and the two thresholds under it are measured: at 0.641 blocks the
    /// wrap stops cutting a parked cabin's grip, and at 0.854 its near edge stops overhanging that grip at
    /// all, so above 0.854 no vertical margin is load-bearing. One cell is 17% past the second and is the
    /// unit everything else in this mod is measured in; it leaves 0.146 blocks of clearance in plan. Not a
    /// knife edge and not a number that wants tuning.
    /// </summary>
    public const float WrapOut = 1f;

    /// <summary>
    /// Blocks the wheel RISES at a station the line runs THROUGH - the one tower with no dead side to stand
    /// out on, where the return strand would otherwise pass straight through the rim (1.12 blocks of rope
    /// inside the swept circle, every revolution). It becomes a HOLD-DOWN SHEAVE on the strand nothing rides
    /// on: the axle goes to <c>ReturnLift + WrapRadius</c>, i.e. 3 * <see cref="WrapRadius"/>, so the groove
    /// is tangent to that strand from below exactly as it is tangent to the going strand from above at a
    /// terminal. What is left after the wheel's own resting height is 0.8832 blocks.
    /// <para>
    /// DERIVED like <see cref="WrapDrop"/>, and the only other option that clears is the one the CABIN
    /// refuses: dropped in place so it carries both strands, the rim's lowest swept point lands 0.06 blocks
    /// over the rope against a passing grip at 0.15, and the grip is inside the rim for 0.90 blocks of travel
    /// every trip in both directions. Refusing the second span at a station was the third option and is
    /// cheaper than either; it makes a legal, buildable route unbuildable, which this mod has declined to do
    /// twice.
    /// </para>
    /// </summary>
    public const float HoldDownRise = 3 * WrapRadius - (RimPivotY - 0.5f);

    /// <summary>
    /// Radius of the frustum sphere the wheel is culled against, in blocks from the block's own centre. The
    /// furthest pose is now the HOLD-DOWN, not the wrap: the axle stands <see cref="HoldDownRise"/> + 1.106 =
    /// 1.989 blocks straight up off that centre against the wrapped pose's 1.200, and a felloe corner reaches
    /// 0.610 further, so the swept rim reaches 2.600 against the wrapped 1.810. 2.75 is the next quarter past
    /// it. Erring large costs at worst a draw call that was going to happen anyway; erring small pops the
    /// wheel off a tower the player is looking at.
    /// <para>
    /// Public only so the asset contract test can measure the shape and check this covers it - and it covers
    /// BOTH poses there, because a number that fits only the tallest is a number that stops meaning anything
    /// when the tallest is deleted. The value is arrived at by hand from a shape the generator can re-author,
    /// and vanilla's own sign renderer passes a literal 1 here - copy that number across and 1.6 blocks of
    /// wheel hang outside the sphere, with nothing in the game or the suite to say so.
    /// </para>
    /// <para>
    /// A DEFAULT rather than the only value since the shaft sheave landed: that wheel's rope radius is 1.5
    /// blocks against this one's 0.663, so its swept rim reaches 2.94 from its own hub and 4.44 from the block
    /// centre it is culled about, and 2.75 would pop it off a headframe the player is standing under. The
    /// constant stays because it is the BULLWHEEL's number and the asset contract test measures the bullwheel's
    /// own shape against it; the instance field is what lets a second wheel carry a second number.
    /// </para>
    /// </summary>
    public const float CullRadius = 2.75f;

    private readonly ICoreClientAPI capi;
    private readonly BlockPos pos;
    private readonly float yawRad;
    private readonly float cullRadius;
    private readonly Matrixf modelMat = new();

    private MeshRef mesh;
    private float angleRad;

    /// <summary>
    /// Revolutions per second, taken from the line's pooled drive speed. Exactly 0 stops the wheel dead
    /// where it is, which is the whole tell: a line with no drive has a still wheel.
    /// </summary>
    public double Speed;

    /// <summary>
    /// Where the wheel stands relative to the cell it belongs to, in blocks: one cell along the dead side
    /// plus <see cref="WrapDrop"/> down at a terminal, where the groove lands on the going strand and the
    /// drawn wrap goes half way round it; <see cref="HoldDownRise"/> straight up at a station the line runs
    /// THROUGH, where the groove lands under the return strand instead; and the zero vector on a tower with
    /// no rope on it at all.
    /// <para>
    /// Replaced wholesale rather than written component by component, and that is the reason it is a Vec3f
    /// and not three floats: <see cref="BEBullwheel"/> writes it on a 500 ms tick and
    /// <see cref="OnRenderFrame"/> reads it every frame, so three separate float writes could be read as a
    /// vector the wheel is never actually at. A reference assignment cannot tear.
    /// </para>
    /// </summary>
    public Vec3f Offset = new();

    public BullwheelRenderer(ICoreClientAPI capi, BlockPos pos, MeshData rim, float yawRad, float cullRadius = CullRadius)
    {
        this.capi = capi;
        this.pos = pos;
        this.yawRad = yawRad;
        this.cullRadius = cullRadius;
        if (rim != null) mesh = capi.Render.UploadMesh(rim);
    }

    public double RenderOrder => 0.5;

    /// <summary>
    /// Required by <c>IRenderer</c> and ignored by the engine - the interface's own summary says "currently
    /// not used!" and nothing in the client reads it, so this is a statement of intent and not a cull. The
    /// real cull is the frustum test at the top of <see cref="OnRenderFrame"/>, which is what vanilla's
    /// <c>BlockEntitySignRenderer</c> does and is a question about the camera rather than about distance: a
    /// drive tower has to read as one from across a valley, which is the whole reason the wheel turns, so
    /// there is no distance at which dropping it would be right. The named constant this used to have went
    /// with the claim: it had no other caller, and a public number for a value nobody reads is worse than none.
    /// </summary>
    public int RenderRange => 64;

    /// <summary>
    /// The rim's spin, appended to whatever <paramref name="m"/> already holds. Move to the wheel's own axle,
    /// spin about it, then swing the whole thing round to the variant's facing. The rim is authored in the
    /// north orientation with its axis along X - along the crossarm, perpendicular to the rope - so the spin
    /// is RotateX and the variant is RotateY, and the order matters: yaw outside, spin inside. The translate
    /// pair is the AXLE and not the block centre. That is the one thing a QuernTopRenderer copy gets wrong: a
    /// quern turns about Y, a Y rotation does not care what height it is handed, and vanilla's lopsided
    /// 0.6875/0 pair is safe only for that reason. Symmetrising it to 0.5/-0.5 read as tidier and made the
    /// wheel orbit the block centre instead of turning on its hub - identical at rest, and a wheel through
    /// the cabin the moment the line ran.
    /// <para>
    /// A method taking no camera offset, rather than five lines inlined at the call site, because the camera
    /// offset was the only thing stopping a test from looking at this. Asserting that <see cref="RimPivotY"/>
    /// and the shape agree pins two INPUTS to the chain and leaves the chain unguarded - put the return
    /// translate back to -0.5 on its own and the wheel orbits again with the constant untouched and every
    /// test still green. Pushing points through the real matrix is the only guard that sees that. The AXLE is
    /// not enough on its own, which is what the guard was at first: it is the fixed point of the chain for
    /// any two rotations in any order about any axes, so it sees the broken translate and neither of the two
    /// failures this paragraph names. What sees those is a point off the axle, checked on all three
    /// components at a non-zero yaw - <c>TheRimTurnsOnItsOwnAxleAtEveryAngleAndEveryFacing</c>.
    /// </para>
    /// <para>
    /// <paramref name="offset"/> goes in the FIRST translate and nowhere else. That is what carries the
    /// wheel out to the rope without changing what it turns about: the return translate is still the axle,
    /// so the pair still cancels and the rotations still happen at the origin the shape is authored around.
    /// Add it to both and the wheel is where it should be and spinning about the cell it left; add it to the
    /// second only and it orbits. The offset is applied OUTSIDE the yaw on purpose - it is a direction the
    /// LINE decides, not the block's own facing, which is what makes a badly-placed wheel wrap correctly
    /// anyway.
    /// </para>
    /// </summary>
    public static Matrixf RimMatrix(Matrixf m, float yawRad, float angleRad, Vec3f offset)
    {
        return m
            .Translate(0.5f + offset.X, RimPivotY + offset.Y, 0.5f + offset.Z)
            .RotateY(yawRad)
            .RotateX(angleRad)
            .Translate(-0.5f, -RimPivotY, -0.5f);
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (mesh == null) return;

        // Wrapped rather than left to grow: a wheel turning for a real-world week accumulates enough
        // radians that a float stops resolving a frame's worth of them and the rotation goes juddery.
        // Advanced ahead of the cull rather than after the draw, so a wheel that spent a while off screen
        // comes back in step: both drive towers of one line are in view together from the far side of a
        // valley, and two wheels on one rope visibly disagreeing is worse than the float multiply it costs.
        angleRad = GameMath.Mod(angleRad + (float)(Speed * GameMath.TWOPI * deltaTime), GameMath.TWOPI);

        var render = capi.Render;

        // The only cull there is, and vanilla's BlockEntitySignRenderer is where the shape comes from.
        // Without it every loaded bullwheel pays a light lookup, a shader bind, a dozen uniform uploads and a
        // draw call every frame, behind the camera included. NOT gated on Speed: a still wheel on a driveless
        // line is the whole tell, so it has to keep being drawn.
        // Still the BLOCK's own centre rather than the wheel's: CullRadius covers the offset pose, so a
        // sphere that moved with the wheel would only be a second thing to keep in step.
        if (!render.DefaultFrustumCuller.SphereInFrustum(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5, cullRadius)) return;

        var cameraPos = capi.World.Player.Entity.CameraPos;

        render.GlDisableCullFace();
        render.GlToggleBlend(true);

        var prog = render.PreparedStandardShader(pos.X, pos.InternalY, pos.Z);
        prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

        prog.ModelMatrix = RimMatrix(
            modelMat.Identity().Translate(pos.X - cameraPos.X, pos.InternalY - cameraPos.Y, pos.Z - cameraPos.Z),
            yawRad,
            angleRad,
            Offset).Values;

        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;
        render.RenderMesh(mesh);
        prog.Stop();
    }

    /// <summary>Degrees of yaw for a <c>side</c> variant, matching the block's own <c>shapeByType</c>.</summary>
    public static float YawFor(string side)
    {
        return (side switch
        {
            "east" => 270f,
            "south" => 180f,
            "west" => 90f,
            _ => 0f
        }) * GameMath.DEG2RAD;
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        mesh?.Dispose();
        mesh = null;
    }
}
