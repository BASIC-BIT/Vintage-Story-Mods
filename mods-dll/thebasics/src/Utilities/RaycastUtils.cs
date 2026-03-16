using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.Utilities;

/// <summary>
/// Shared raycast helpers for server-side look-direction raycasting.
/// </summary>
public static class RaycastUtils
{
    /// <summary>
    /// Raycasts from a player's eye position along their look direction.
    /// Returns the hit position offset slightly back toward the player and above the surface,
    /// or null if nothing was hit within <paramref name="maxDistance"/> blocks.
    /// </summary>
    public static Vec3d RaycastFromPlayerLook(IServerPlayer player, double maxDistance)
    {
        if (player?.Entity == null || maxDistance <= 0)
        {
            return null;
        }

        var serverPos = player.Entity.ServerPos;
        var eyePos = serverPos.XYZ.AddCopy(player.Entity.LocalEyePos);

        // GetViewVector uses ServerPos.Pitch and ServerPos.Yaw to compute a unit direction.
        var viewDir = serverPos.GetViewVector();
        var toPos = eyePos.AddCopy(
            viewDir.X * maxDistance,
            viewDir.Y * maxDistance,
            viewDir.Z * maxDistance
        );

        BlockSelection blockSel = null;
        EntitySelection entitySel = null;

        try
        {
            player.Entity.World.RayTraceForSelection(eyePos, toPos, ref blockSel, ref entitySel, efilter: _ => false);
        }
        catch (System.Exception ex)
        {
            player.Entity.World.Logger.Debug("THEBASICS RaycastUtils: RayTraceForSelection threw: {0}", ex.Message);
            return null;
        }

        if (blockSel?.Position == null)
        {
            // Ray hit nothing (looking at sky / past world boundary).
            return null;
        }

        // Use the precise hit point on the block face.
        var hitPos = blockSel.HitPosition != null
            ? new Vec3d(
                blockSel.Position.X + blockSel.HitPosition.X,
                blockSel.Position.Y + blockSel.HitPosition.Y,
                blockSel.Position.Z + blockSel.HitPosition.Z)
            : blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5);

        // Pull the bubble back along the ray by ~0.3 blocks so it floats
        // in front of the surface rather than clipping into the block.
        const double pullBack = 0.3;
        var rayDir = new Vec3d(viewDir.X, viewDir.Y, viewDir.Z);
        var rayLen = rayDir.Length();
        if (rayLen > 0)
        {
            rayDir.X /= rayLen;
            rayDir.Y /= rayLen;
            rayDir.Z /= rayLen;
        }

        hitPos.X -= rayDir.X * pullBack;
        hitPos.Y -= rayDir.Y * pullBack;
        hitPos.Z -= rayDir.Z * pullBack;

        // Float upward slightly so the bubble sits above the surface.
        hitPos.Y += 0.5;

        return hitPos;
    }
}
