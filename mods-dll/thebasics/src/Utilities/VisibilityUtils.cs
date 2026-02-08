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
            return false;
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

            var blockSel = new BlockSelection();
            var entitySel = new EntitySelection();

            world.RayTraceForSelection(fromPos, toPos, ref blockSel, ref entitySel, efilter: e => e.EntityId != observer.EntityId);

            return entitySel.Entity != null && entitySel.Entity.EntityId == target.EntityId;
        }
        catch
        {
            return failOpen;
        }
    }

    public static bool HasLineOfSight(IWorldAccessor world, Entity observer, Entity target)
    {
        // Client-side visuals should fail open.
        return HasLineOfSight(world, observer, target, failOpen: true);
    }
}
