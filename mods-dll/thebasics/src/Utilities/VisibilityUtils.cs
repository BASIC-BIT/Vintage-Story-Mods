using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace thebasics.Utilities;

public static class VisibilityUtils
{
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

            // For our purposes, we only want to know if any non-air block blocks the segment
            // between observer and target.
            // We intentionally ignore entities as potential blockers.
            BlockSelection blockSel = null;
            EntitySelection entitySel = null;
            world.RayTraceForSelection(fromPos, toPos, ref blockSel, ref entitySel, efilter: _ => false);

            // RayTraceForSelection sets blockSel to null when no block is hit.
            // Line of sight is clear when nothing was intersected.
            if (blockSel?.Block == null)
            {
                return true;
            }

            // If a block was hit, LOS is only clear if it's air (Id 0).
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
}
