using System;
using System.Collections.Generic;
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

    public static bool HasLineOfSight(
        IWorldAccessor world,
        Entity observer,
        Entity target,
        bool failOpen,
        bool useMultiPointTargets = false)
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

            foreach (var targetPos in GetEntityLineOfSightTargetPositions(toBase, target, useMultiPointTargets))
            {
                if (IsRayClear(world, fromPos, targetPos, failOpen))
                {
                    return true;
                }
            }

            return false;
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

            return IsRayClear(world, fromPos, targetPos, failOpen);
        }
        catch (Exception ex)
        {
            world.Logger?.Debug("THEBASICS VisibilityUtils: LOS raytrace to Vec3d threw: {0}", ex.Message);
            return failOpen;
        }
    }

    private static IEnumerable<Vec3d> GetEntityLineOfSightTargetPositions(
        Vec3d targetBase,
        Entity target,
        bool useMultiPointTargets)
    {
        yield return targetBase.AddCopy(target.LocalEyePos);

        if (!useMultiPointTargets)
        {
            yield break;
        }

        var height = GetEntityHeight(target);
        if (height <= 0)
        {
            yield break;
        }

        yield return targetBase.AddCopy(0, height * 0.55, 0);
        yield return targetBase.AddCopy(0, height * 0.2, 0);
    }

    private static double GetEntityHeight(Entity target)
    {
        var height = target.CollisionBox?.YSize ?? target.SelectionBox?.YSize ?? 0;
        if (height > 0)
        {
            return height;
        }

        return target.LocalEyePos?.Y * 1.2 ?? 0;
    }

    private static bool IsRayClear(IWorldAccessor world, Vec3d fromPos, Vec3d targetPos, bool failOpen)
    {
        try
        {
            // For our purposes, we only want to know if any opaque block blocks the segment.
            // Visually transparent blocks are skipped and entities are ignored as blockers.
            BlockSelection blockSel = null;
            EntitySelection entitySel = null;
            world.RayTraceForSelection(fromPos, targetPos, ref blockSel, ref entitySel,
                bfilter: LosBlockFilter, efilter: _ => false);

            return blockSel?.Block == null || blockSel.Block.Id == 0;
        }
        catch
        {
            return failOpen;
        }
    }
}
