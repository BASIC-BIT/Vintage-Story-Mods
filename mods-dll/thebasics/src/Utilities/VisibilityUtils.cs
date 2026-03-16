using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace thebasics.Utilities;

public static class VisibilityUtils
{
    /// <summary>
    /// Block filter for LOS raycasts that allows rays to pass through visually transparent
    /// blocks (glass, leaves, water, fences, etc.). Returns true for blocks that should
    /// STOP the ray, false for blocks the ray should pass through.
    /// </summary>
    private static readonly BlockFilter LosBlockFilter = (BlockPos pos, Block block) =>
    {
        if (block == null || block.Id == 0)
        {
            return false; // Air — ray continues.
        }

        // Blocks rendered in transparent/blended/liquid passes are visually see-through.
        // Let the ray pass through them so LOS checks behave consistently with what
        // the player can actually see on screen.
        if (block.RenderPass is EnumChunkRenderPass.Transparent   // glass, ice
                             or EnumChunkRenderPass.BlendNoCull   // leaves, lattices, cobweb
                             or EnumChunkRenderPass.Liquid)        // water, lava
        {
            return false; // Visually transparent — ray continues.
        }

        return true; // Opaque — ray stops here.
    };

    public static bool HasLineOfSight(IWorldAccessor world, Entity observer, Entity target, bool failOpen)
    {
        if (world == null || observer == null || target == null)
        {
            return failOpen;
        }

        if (observer.EntityId == target.EntityId)
        {
            return true;
        }

        try
        {
            // RayTraceForSelection expects world-space coordinates.
            // Use the most appropriate position source for the current side.
            var fromBase = world.Side == EnumAppSide.Server ? observer.ServerPos.XYZ : observer.Pos.XYZ;
            var toBase = world.Side == EnumAppSide.Server ? target.ServerPos.XYZ : target.Pos.XYZ;

            var fromPos = fromBase.AddCopy(observer.LocalEyePos);
            var toPos = toBase.AddCopy(target.LocalEyePos);

            // For our purposes, we only want to know if any opaque block blocks the segment
            // between observer and target. Visually transparent blocks (glass, leaves, water)
            // are skipped so LOS matches what the player can actually see.
            // We intentionally ignore entities as potential blockers.
            BlockSelection blockSel = null;
            EntitySelection entitySel = null;
            world.RayTraceForSelection(fromPos, toPos, ref blockSel, ref entitySel,
                bfilter: LosBlockFilter, efilter: _ => false);

            // RayTraceForSelection sets blockSel to null when no block is hit.
            // Line of sight is clear when nothing was intersected.
            if (blockSel?.Block == null)
            {
                return true;
            }

            // If a block was hit (and passed the filter), it's opaque — LOS blocked.
            // The Id == 0 check is a safety net; air should already be filtered out.
            return blockSel.Block.Id == 0;
        }
        catch
        {
            return failOpen;
        }
    }

    public static bool HasLineOfSight(IWorldAccessor world, Entity observer, Entity target)
    {
        // Visual cues should not leak information through terrain.
        // If LOS checks fail for any reason, prefer to hide the cue.
        return HasLineOfSight(world, observer, target, failOpen: false);
    }

    /// <summary>
    /// Checks line of sight from an observer entity to an arbitrary world position.
    /// Used for placed environmental bubbles where the target is a point, not an entity.
    /// </summary>
    public static bool HasLineOfSight(IWorldAccessor world, Entity observer, Vec3d targetPos, bool failOpen = false)
    {
        if (world == null || observer == null || targetPos == null)
        {
            return failOpen;
        }

        try
        {
            var fromBase = world.Side == EnumAppSide.Server ? observer.ServerPos.XYZ : observer.Pos.XYZ;
            var fromPos = fromBase.AddCopy(observer.LocalEyePos);

            BlockSelection blockSel = null;
            EntitySelection entitySel = null;
            world.RayTraceForSelection(fromPos, targetPos, ref blockSel, ref entitySel,
                bfilter: LosBlockFilter, efilter: _ => false);

            if (blockSel?.Block == null)
            {
                return true;
            }

            return blockSel.Block.Id == 0;
        }
        catch (Exception ex)
        {
            world.Logger?.Debug("THEBASICS VisibilityUtils: LOS raytrace to Vec3d threw: {0}", ex.Message);
            return failOpen;
        }
    }
}
