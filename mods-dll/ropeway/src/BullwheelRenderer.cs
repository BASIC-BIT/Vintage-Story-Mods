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
    /// Radius of the frustum sphere the wheel is culled against, in blocks from the block's own centre. The
    /// swept rim reaches 1.712 blocks from there - a felloe corner 0.603 blocks out from an axle that is
    /// itself 1.106 blocks up - and 2 is the next round number past it. Erring large costs at worst a draw
    /// call that was going to happen anyway; erring small pops the wheel off a tower the player is looking at.
    /// <para>
    /// Public only so the asset contract test can measure the shape and check this covers it. The value 2 is
    /// arrived at by hand from a shape the generator can re-author, and vanilla's own sign renderer passes a
    /// literal 1 here - copy that number across and 0.712 blocks of wheel hang outside the sphere, with
    /// nothing in the game or the suite to say so.
    /// </para>
    /// </summary>
    public const float CullRadius = 2f;

    private readonly ICoreClientAPI capi;
    private readonly BlockPos pos;
    private readonly float yawRad;
    private readonly Matrixf modelMat = new();

    private MeshRef mesh;
    private float angleRad;

    /// <summary>
    /// Revolutions per second, taken from the line's pooled drive speed. Exactly 0 stops the wheel dead
    /// where it is, which is the whole tell: a line with no drive has a still wheel.
    /// </summary>
    public double Speed;

    public BullwheelRenderer(ICoreClientAPI capi, BlockPos pos, MeshData rim, float yawRad)
    {
        this.capi = capi;
        this.pos = pos;
        this.yawRad = yawRad;
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
    /// </summary>
    public static Matrixf RimMatrix(Matrixf m, float yawRad, float angleRad)
    {
        return m
            .Translate(0.5f, RimPivotY, 0.5f)
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
        if (!render.DefaultFrustumCuller.SphereInFrustum(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5, CullRadius)) return;

        var cameraPos = capi.World.Player.Entity.CameraPos;

        render.GlDisableCullFace();
        render.GlToggleBlend(true);

        var prog = render.PreparedStandardShader(pos.X, pos.InternalY, pos.Z);
        prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

        prog.ModelMatrix = RimMatrix(
            modelMat.Identity().Translate(pos.X - cameraPos.X, pos.InternalY - cameraPos.Y, pos.Z - cameraPos.Z),
            yawRad,
            angleRad).Values;

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
