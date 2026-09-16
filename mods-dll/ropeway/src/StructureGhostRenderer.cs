using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// Draws the block that BELONGS in each still-empty cell of an unfinished tower, ghosted, standing exactly
/// where the player has to put it. The build overlay it stands inside draws a flat translucent cuboid per
/// cell in that block's average colour, which is the only thing vanilla's highlight can draw - see
/// <see cref="BEPylonBase.Highlight"/> - and a five-by-four grid of brown boxes does not tell anybody that
/// the middle one of the top row is a pylon head and the six beside it are braces.
/// <para>
/// WHY A RENDERER AND NOT A HIGHLIGHT. <c>IWorldAccessor.HighlightBlocks</c> takes an
/// <c>EnumHighlightShape</c>, and the whole enum is <c>Arbitrary, Cube, Ball, Cubes, Cylinder</c> - there is
/// no shape option anywhere in the API that draws a BLOCK, and the two vanilla users of
/// <c>MultiblockStructure</c> (<c>BlockEntityBeeHiveKiln</c>, <c>BlockEntityStoneCoffin</c>) both just call
/// <c>HighlightIncompleteParts</c> and get the coloured cuboids. So "show the actual block" is a custom
/// renderer or it is nothing.
/// </para>
/// <para>
/// It is a CHEAP one, which is what made it worth doing. The mesh is
/// <c>ITesselatorManager.GetDefaultBlockMeshRef</c> - the same already-uploaded
/// <see cref="MultiTextureMeshRef"/> the engine hands the inventory renderer, owned and disposed by the
/// engine - so there is nothing to tesselate, upload, cache or free here, unlike
/// <see cref="BullwheelRenderer"/> which owns its rim. The whole per-frame cost is one frustum test, one
/// shader bind and at most fifteen draw calls, on the one tower a player has asked about; nothing is
/// registered until <see cref="BEPylonBase.ShowIncompleteParts"/> runs.
/// </para>
/// <para>
/// The vanilla overlay is deliberately LEFT ON underneath. It is drawn by the block-highlight shader with no
/// depth test, so it shows through terrain and through the ghosts and marks the cells from any angle, which
/// is the half a ghost cannot do; and if this renderer ever draws nothing the player is left exactly where
/// he is today rather than with a tower and no guidance at all.
/// </para>
/// </summary>
public sealed class StructureGhostRenderer : IRenderer, IDisposable
{
    /// <summary>
    /// How solid a ghost is. Enough to read the shape and the texture, little enough that it cannot be
    /// mistaken for a block that is already there - which matters more than it sounds, because the player's
    /// next act is to decide whether the cell is done.
    /// </summary>
    public const float Alpha = 0.45f;

    /// <summary>
    /// Floor under each cell's sampled light. A ghost is guidance rather than scenery: the vanilla overlay it
    /// stands in is drawn unlit, so without this the two disagree at dusk - coloured boxes glowing over
    /// invisible ghosts - and the mod's own answer to "what goes here" would stop working after sunset, which
    /// is when a player is most likely to be reading it.
    /// </summary>
    private const float LightFloor = 0.55f;

    /// <summary>
    /// Blocks from the FOOTING that the whole pattern fits inside. The furthest cell of every shipped
    /// structure is a crossarm end at (+/-3, 4, 0), 5 blocks out, and half a block of block body past that;
    /// 6 is the next whole number. Erring large costs a draw call that was going to happen anyway - erring
    /// small pops the guidance off a tower the player is standing in.
    /// </summary>
    private const float CullRadius = 6f;

    private readonly ICoreClientAPI capi;
    private readonly BlockPos origin;
    private readonly Matrixf modelMat = new();
    private readonly Vec4f tint = new(1, 1, 1, Alpha);

    /// <summary>
    /// The cells to draw. REPLACED wholesale and never mutated in place: <see cref="BEPylonBase"/> rebuilds
    /// it on its 500 ms client tick while <see cref="OnRenderFrame"/> reads it every frame, and a reference
    /// assignment cannot tear where a list being cleared and refilled can be read half empty. Same reason
    /// <see cref="BullwheelRenderer.Offset"/> is a whole Vec3f.
    /// </summary>
    public IReadOnlyList<BEPylonBase.WantedCell> Cells = Array.Empty<BEPylonBase.WantedCell>();

    public StructureGhostRenderer(ICoreClientAPI capi, BlockPos origin)
    {
        this.capi = capi;
        this.origin = origin;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "ropewayghost");
    }

    public double RenderOrder => 0.5;

    /// <summary>Not read by the engine - see <see cref="BullwheelRenderer.RenderRange"/>. The real cull is the
    /// frustum test below.</summary>
    public int RenderRange => 32;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var cells = Cells;
        if (cells.Count == 0) return;

        var render = capi.Render;
        if (!render.DefaultFrustumCuller.SphereInFrustum(
                origin.X + 0.5, origin.InternalY + 0.5, origin.Z + 0.5, CullRadius))
        {
            return;
        }

        var cameraPos = capi.World.Player?.Entity?.CameraPos;
        if (cameraPos == null) return;

        render.GlDisableCullFace();
        render.GlToggleBlend(true);

        // One bind for the whole pattern: only the model matrix and the light change per cell, and both are
        // uniforms. Light is sampled AT each cell rather than at the footing, which is on the ground and in
        // the tower's own shadow - a crossarm ghost lit by the footing's light reads as a different material.
        var prog = render.PreparedStandardShader(origin.X, origin.InternalY, origin.Z);
        prog.RgbaTint = tint;
        prog.ViewMatrix = render.CameraMatrixOriginf;
        prog.ProjectionMatrix = render.CurrentProjectionMatrix;

        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell.Ghost == null) continue;

            var mesh = capi.TesselatorManager.GetDefaultBlockMeshRef(cell.Ghost);
            if (mesh == null || mesh.Disposed) continue;

            var light = capi.World.BlockAccessor.GetLightRGBs(cell.Pos.X, cell.Pos.Y, cell.Pos.Z);
            prog.RgbaLightIn = new Vec4f(
                Math.Max(light.R, LightFloor), Math.Max(light.G, LightFloor), Math.Max(light.B, LightFloor),
                light.A);

            prog.ModelMatrix = modelMat.Identity()
                .Translate(cell.Pos.X - cameraPos.X, cell.Pos.InternalY - cameraPos.Y, cell.Pos.Z - cameraPos.Z)
                .Values;

            // "tex" and not "tex2d": that is the sampler name standard.fsh declares, and the multi-texture
            // form is what binds each atlas page in turn - a block whose textures straddle two atlases draws
            // half of itself with the single-texture call.
            render.RenderMultiTextureMesh(mesh, "tex");
        }

        prog.Stop();
    }

    public void Dispose()
    {
        // The meshes are the ENGINE's - GetDefaultBlockMeshRef hands out the tesselator manager's own cached
        // refs, which every other block in the world is drawn from. Disposing one here would blank that block
        // in every inventory slot and every ground pile until the next atlas rebuild.
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        Cells = Array.Empty<BEPylonBase.WantedCell>();
    }
}
