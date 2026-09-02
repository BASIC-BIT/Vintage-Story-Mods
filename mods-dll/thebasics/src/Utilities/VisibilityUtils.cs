using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace thebasics.Utilities;

public static class VisibilityUtils
{
    private const double SegmentSampleStep = 0.5;

    // Backstop for the occluder walk: 512 blocks at half-block steps, matching the largest
    // configurable chat range. Beyond this the count is already far past any useful penalty.
    private const int MaxSegmentSamples = 1024;

    private static bool _warnedSegmentSampleCap;

    /// <summary>
    /// Strict sight filter: anything not rendered see-through stops the ray, foliage included.
    /// Returns true for blocks that should STOP the ray, false for blocks it should pass through.
    ///
    /// Default-deny: an unrecognised block occludes rather than silently leaking through.
    ///
    /// Reserved for deliberate close inspection, where reading detail through a hedge would be
    /// wrong. Its only consumer is the character-sheet look-up, which shows one player another's
    /// written description at close range. Everything else uses <see cref="SightBlockFilter"/>.
    /// </summary>
    internal static readonly BlockFilter StrictSightBlockFilter = (BlockPos pos, Block block) =>
    {
        if (block == null || block.Id == 0)
        {
            return false; // Air — ray continues.
        }

        // Blocks rendered in transparent/blended/liquid passes are visually see-through.
        if (block.RenderPass is EnumChunkRenderPass.Transparent   // glass, ice
                             or EnumChunkRenderPass.BlendNoCull   // lattices, cobweb, fallen leaves
                             or EnumChunkRenderPass.Liquid)        // water, lava
        {
            return false; // Visually transparent — ray continues.
        }

        return true; // Opaque — ray stops here.
    };

    /// <summary>
    /// General sight filter. Identical to <see cref="StrictSightBlockFilter"/> except that foliage
    /// does not block: tree leaves and plants declare no render pass, so they default to Opaque and
    /// would otherwise hide a player standing under a canopy.
    ///
    /// Everything that decides whether a player can perceive something reads through this one —
    /// sign language delivery, speech bubbles, nametags, the typing indicator, placed environmental
    /// bubbles — so they cannot disagree. They used to: a signed message delivered through leaves
    /// rendered no bubble, because delivery used this rule and rendering used the strict one.
    /// </summary>
    internal static readonly BlockFilter SightBlockFilter = (BlockPos pos, Block block) =>
    {
        if (block?.BlockMaterial is EnumBlockMaterial.Leaves or EnumBlockMaterial.Plant)
        {
            return false;
        }

        return StrictSightBlockFilter(pos, block);
    };

    /// <summary>
    /// Block filter for sound. Sound and sight occlude differently: glass and water stop speech
    /// but not sight, while foliage stops neither.
    ///
    /// A block stops sound if it has a collision box or is a liquid. Collision boxes default to a
    /// full cube, so blocks this mod has never heard of occlude by default, while non-collidable
    /// decor (tall grass, loose ground cover) does not. Liquids need the extra check because water
    /// has no collision box.
    /// </summary>
    internal static readonly BlockFilter SoundBlockFilter = (BlockPos pos, Block block) => BlocksSound(block);

    private static bool BlocksSound(Block block)
    {
        if (block == null || block.Id == 0)
        {
            return false; // Air.
        }

        if (block.BlockMaterial is EnumBlockMaterial.Leaves or EnumBlockMaterial.Plant)
        {
            return false; // Foliage muffles nothing worth modelling.
        }

        if (block.BlockMaterial is EnumBlockMaterial.Water or EnumBlockMaterial.Lava)
        {
            return true; // No collision box, but speech does not carry through it.
        }

        // ponytail: reads the static collision box rather than GetCollisionBoxes(world, pos), so a
        // block whose collision changes at runtime (an open door) still counts as occluding. Swap to
        // the positional call if that ever matters more than the per-message cost.
        return block.CollisionBoxes is { Length: > 0 };
    }

    /// <summary>
    /// Whether the observer can see the target. Foliage does not block; a player under a canopy is
    /// still visible. Used by everything that perceives a person or their live message.
    /// </summary>
    public static bool HasLineOfSight(
        IWorldAccessor world,
        Entity observer,
        Entity target,
        bool failOpen,
        bool useMultiPointTargets = false)
    {
        return HasClearPath(world, observer, target, failOpen, useMultiPointTargets, SightBlockFilter);
    }

    public static bool HasLineOfSight(IWorldAccessor world, Entity observer, Entity target)
    {
        // Visual cues should not leak information through terrain.
        // If LOS checks fail for any reason, prefer to hide the cue.
        return HasLineOfSight(world, observer, target, failOpen: false);
    }

    /// <summary>
    /// Sight for deliberate close inspection, where foliage does block. Reading a character sheet
    /// through a hedge is different from noticing that someone is standing there.
    ///
    /// The character-sheet look-up is the only caller. Everything else that decides whether a
    /// player can perceive something uses <see cref="HasLineOfSight(IWorldAccessor, Entity, Entity, bool, bool)"/>.
    /// </summary>
    public static bool HasStrictLineOfSight(IWorldAccessor world, Entity observer, Entity target, bool failOpen = false)
    {
        return HasClearPath(world, observer, target, failOpen, useMultiPointTargets: false, StrictSightBlockFilter);
    }

    /// <summary>
    /// Sight from an observer entity to an arbitrary world position.
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
            var fromBase = observer.Pos.XYZ;
            var fromPos = fromBase.AddCopy(observer.LocalEyePos);

            return IsRayClear(world, fromPos, targetPos, failOpen, SightBlockFilter);
        }
        catch (Exception ex)
        {
            world.Logger?.Debug("THEBASICS VisibilityUtils: LOS raytrace to Vec3d threw: {0}", ex.Message);
            return failOpen;
        }
    }

    /// <summary>
    /// Whether speech can reach the target unobstructed. Uses the sound occlusion rules, which
    /// differ from sight: glass and water block speech, foliage does not.
    /// </summary>
    public static bool HasLineOfHearing(
        IWorldAccessor world,
        Entity observer,
        Entity target,
        bool failOpen,
        bool useMultiPointTargets = false)
    {
        return HasClearPath(world, observer, target, failOpen, useMultiPointTargets, SoundBlockFilter);
    }

    private static bool HasClearPath(
        IWorldAccessor world,
        Entity observer,
        Entity target,
        bool failOpen,
        bool useMultiPointTargets,
        BlockFilter filter)
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
            var fromBase = observer.Pos.XYZ;
            var toBase = target.Pos.XYZ;

            var fromPos = fromBase.AddCopy(observer.LocalEyePos);

            return GetEntityLineOfSightTargetPositions(toBase, target, useMultiPointTargets)
                .Any(targetPos => IsRayClear(world, fromPos, targetPos, failOpen, filter));
        }
        catch
        {
            return failOpen;
        }
    }

    /// <summary>
    /// Counts distinct sound-occluding blocks on the straight line between two entities, for use as
    /// a muffling penalty. Samples the segment rather than raytracing because
    /// <c>RayTraceForSelection</c> reports only the first hit and cannot count.
    /// </summary>
    public static int CountSoundOccluders(IWorldAccessor world, Entity observer, Entity target)
    {
        if (world?.BlockAccessor == null || observer == null || target == null || observer.EntityId == target.EntityId)
        {
            return 0;
        }

        try
        {
            var fromPos = observer.Pos.XYZ.AddCopy(observer.LocalEyePos);
            var toPos = target.Pos.XYZ.AddCopy(target.LocalEyePos);

            return CountSoundOccludersOnSegment(world, fromPos, toPos);
        }
        catch (Exception ex)
        {
            world.Logger?.Debug("THEBASICS VisibilityUtils: occluder count threw: {0}", ex.Message);
            return 0;
        }
    }

    private static int CountSoundOccludersOnSegment(IWorldAccessor world, Vec3d fromPos, Vec3d toPos)
    {
        var delta = toPos.SubCopy(fromPos);
        var length = delta.Length();
        if (length <= 0)
        {
            return 0;
        }

        // Half-block steps: fine enough that a one-block-thick wall always lands at least one
        // sample inside it, coarse enough to stay cheap on long ranges. Capped so a caller that
        // forgets to bound the distance cannot walk the whole map on the chat hot path.
        //
        // The cap is inert for any range the admin panel accepts (max 512, and Manhattan distance
        // is never below Euclidean), but a hand-edited config skips that validation. When the cap
        // does bind the step size grows past one block and thin walls stop being counted, so say so
        // once rather than letting muffling quietly stop working.
        var uncappedSteps = Math.Ceiling(length / SegmentSampleStep);
        if (uncappedSteps > MaxSegmentSamples && !_warnedSegmentSampleCap)
        {
            _warnedSegmentSampleCap = true;
            world.Logger?.Warning(
                "THEBASICS: chat occlusion sampled a {0:0} block segment, past the {1} sample cap. " +
                "Wall muffling will under-count walls at this range; lower the chat range or disable " +
                "SpeechOcclusionWallPenaltyBlocks.",
                length,
                MaxSegmentSamples);
        }

        var steps = (int)Math.Min(MaxSegmentSamples, uncappedSteps);
        var accessor = world.BlockAccessor;

        var occluders = 0;
        var lastX = int.MinValue;
        var lastY = int.MinValue;
        var lastZ = int.MinValue;
        var samplePos = new BlockPos(0);

        for (var step = 1; step < steps; step++)
        {
            var t = (double)step / steps;
            var x = (int)Math.Floor(fromPos.X + (delta.X * t));
            var y = (int)Math.Floor(fromPos.Y + (delta.Y * t));
            var z = (int)Math.Floor(fromPos.Z + (delta.Z * t));

            // Consecutive samples land in the same block on most steps; only test on entry.
            if (x == lastX && y == lastY && z == lastZ)
            {
                continue;
            }

            lastX = x;
            lastY = y;
            lastZ = z;

            // Entity positions carry a dimension-encoded Y. Plain Set(x, y, z) preserves the
            // BlockPos's existing dimension and writes a raw Y, which samples far above the world
            // (always air) for anyone inside a pocket dimension and silently disables muffling
            // there. SetAndCorrectDimension splits the encoded Y back into Y plus dimension.
            samplePos.SetAndCorrectDimension(x, y, z);

            if (BlocksSound(accessor.GetBlock(samplePos)))
            {
                occluders++;
            }
        }

        return occluders;
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

    private static bool IsRayClear(IWorldAccessor world, Vec3d fromPos, Vec3d targetPos, bool failOpen, BlockFilter filter)
    {
        try
        {
            if (world is not IWorldIntersectionSupplier supplier)
            {
                return failOpen;
            }

            var blockSel = RayTraceBlocksForSelection(supplier, fromPos, targetPos, filter);

            return blockSel?.Block == null || blockSel.Block.Id == 0;
        }
        catch
        {
            return failOpen;
        }
    }

    /// <summary>
    /// Runs the exact block phase used by <c>GameMain.RayTraceForSelection</c>, without its entity
    /// broad-phase search. LOS callers have always rejected every entity, so entity enumeration
    /// cannot change their answer and becomes pathological on long rays.
    /// </summary>
    internal static BlockSelection RayTraceBlocksForSelection(
        IWorldIntersectionSupplier supplier,
        Vec3d fromPos,
        Vec3d targetPos,
        BlockFilter filter)
    {
        var ray = Ray.FromPositions(fromPos, targetPos);
        if (ray == null)
        {
            return null;
        }

        var intersectionTester = new AABBIntersectionTest(supplier);
        intersectionTester.LoadRayAndPos(ray);
        return intersectionTester.GetSelectedBlock((float)ray.Length, filter);
    }
}
