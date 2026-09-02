using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using thebasics.Configs;
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
    private static readonly ConditionalWeakTable<IWorldAccessor, SightPolicyHolder> SightPolicies = new();
    private static readonly SightBlockPolicy DefaultSightPolicy = SightBlockPolicy.Resolve([], [], []);

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
    internal static readonly BlockFilter StrictSightBlockFilter = DefaultSightPolicy.StrictFilter;

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
    internal static readonly BlockFilter SightBlockFilter = DefaultSightPolicy.GeneralFilter;

    public static void ConfigureSightBlockOverrides(IWorldAccessor world, ModConfig config)
    {
        if (world == null || config == null)
        {
            return;
        }

        var policy = SightBlockPolicy.Resolve(
            world.Blocks,
            config.SightPassThroughBlockCodePatterns,
            config.SightBlockingBlockCodePatterns);

        var holder = SightPolicies.GetValue(world, _ => new SightPolicyHolder());
        Volatile.Write(ref holder.Policy, policy);

        if (world.Side != EnumAppSide.Server)
        {
            return;
        }

        foreach (var pattern in policy.UnmatchedPatterns)
        {
            world.Logger?.Warning("THEBASICS: sight block override pattern '{0}' matched no registered blocks.", pattern);
        }

        foreach (var blockCode in policy.ConflictingBlockCodes)
        {
            world.Logger?.Warning(
                "THEBASICS: block '{0}' matches both sight override lists. The blocking override takes precedence.",
                blockCode);
        }
    }

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
        var policy = GetSightPolicy(world);
        return HasClearPath(world, observer, target, failOpen, useMultiPointTargets, policy.GeneralFilter, policy);
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
        var policy = GetSightPolicy(world);
        return HasClearPath(world, observer, target, failOpen, useMultiPointTargets: false, policy.StrictFilter, policy);
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

            var policy = GetSightPolicy(world);
            return IsRayClear(world, fromPos, targetPos, failOpen, policy.GeneralFilter, policy);
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
        BlockFilter filter,
        SightBlockPolicy sightPolicy = null)
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
                .Any(targetPos => IsRayClear(world, fromPos, targetPos, failOpen, filter, sightPolicy));
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

    private static bool IsRayClear(
        IWorldAccessor world,
        Vec3d fromPos,
        Vec3d targetPos,
        bool failOpen,
        BlockFilter filter,
        SightBlockPolicy sightPolicy = null)
    {
        try
        {
            if (world is not IWorldIntersectionSupplier supplier)
            {
                return failOpen;
            }

            var blockSel = RayTraceBlocksForSelection(supplier, fromPos, targetPos, filter);

            return (blockSel?.Block == null || blockSel.Block.Id == 0) &&
                   !HasExplicitBlockingBlockWithoutBoxes(supplier, fromPos, targetPos, sightPolicy);
        }
        catch
        {
            return failOpen;
        }
    }

    private static SightBlockPolicy GetSightPolicy(IWorldAccessor world)
    {
        return world != null && SightPolicies.TryGetValue(world, out var holder)
            ? Volatile.Read(ref holder.Policy) ?? DefaultSightPolicy
            : DefaultSightPolicy;
    }

    private sealed class SightPolicyHolder
    {
        public SightBlockPolicy Policy;
    }

    internal static bool HasExplicitBlockingBlockWithoutBoxes(
        IWorldIntersectionSupplier supplier,
        Vec3d fromPos,
        Vec3d targetPos,
        SightBlockPolicy policy)
    {
        if (supplier == null || policy?.HasBlockingOverrides != true)
        {
            return false;
        }

        var traversal = new VoxelTraversal(fromPos, targetPos);
        var pos = new BlockPos(0);

        do
        {
            pos.SetAndCorrectDimension(traversal.X, traversal.Y, traversal.Z);
            if (IsExplicitBlockingBlockWithoutBoxesAt(supplier, pos, policy))
            {
                return true;
            }
        }
        while (traversal.Advance());

        return false;
    }

    private static bool IsExplicitBlockingBlockWithoutBoxesAt(
        IWorldIntersectionSupplier supplier,
        BlockPos pos,
        SightBlockPolicy policy)
    {
        var block = supplier.blockAccessor.GetBlock(pos, 2);
        var selectionBlock = block.SideSolid.Any ? block : supplier.GetBlock(pos);
        if (!policy.IsExplicitlyBlocking(block) && !policy.IsExplicitlyBlocking(selectionBlock))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(block.EntityClass) &&
            string.IsNullOrWhiteSpace(selectionBlock.EntityClass) &&
            supplier.blockAccessor.GetBlockEntity(pos) == null)
        {
            return false;
        }

        if (block.SideSolid.Any)
        {
            return block.GetSelectionBoxes(supplier.blockAccessor, pos) is not { Length: > 0 };
        }

        return supplier.GetBlockIntersectionBoxes(pos) is not { Length: > 0 };
    }

    private struct VoxelTraversal
    {
        private readonly int _endX;
        private readonly int _endY;
        private readonly int _endZ;
        private readonly int _stepX;
        private readonly int _stepY;
        private readonly int _stepZ;
        private readonly double _tDeltaX;
        private readonly double _tDeltaY;
        private readonly double _tDeltaZ;
        private double _tMaxX;
        private double _tMaxY;
        private double _tMaxZ;

        public VoxelTraversal(Vec3d fromPos, Vec3d targetPos)
        {
            var delta = targetPos.SubCopy(fromPos);
            X = (int)Math.Floor(fromPos.X);
            Y = (int)Math.Floor(fromPos.Y);
            Z = (int)Math.Floor(fromPos.Z);
            _endX = (int)Math.Floor(targetPos.X);
            _endY = (int)Math.Floor(targetPos.Y);
            _endZ = (int)Math.Floor(targetPos.Z);
            _stepX = Math.Sign(delta.X);
            _stepY = Math.Sign(delta.Y);
            _stepZ = Math.Sign(delta.Z);
            _tDeltaX = _stepX == 0 ? double.PositiveInfinity : 1 / Math.Abs(delta.X);
            _tDeltaY = _stepY == 0 ? double.PositiveInfinity : 1 / Math.Abs(delta.Y);
            _tDeltaZ = _stepZ == 0 ? double.PositiveInfinity : 1 / Math.Abs(delta.Z);
            _tMaxX = InitialBoundaryT(fromPos.X, delta.X, X, _stepX);
            _tMaxY = InitialBoundaryT(fromPos.Y, delta.Y, Y, _stepY);
            _tMaxZ = InitialBoundaryT(fromPos.Z, delta.Z, Z, _stepZ);
        }

        public int X { get; private set; }

        public int Y { get; private set; }

        public int Z { get; private set; }

        public bool Advance()
        {
            if (X == _endX && Y == _endY && Z == _endZ)
            {
                return false;
            }

            var nextT = Math.Min(_tMaxX, Math.Min(_tMaxY, _tMaxZ));
            if (_tMaxX <= nextT)
            {
                X += _stepX;
                _tMaxX += _tDeltaX;
            }
            if (_tMaxY <= nextT)
            {
                Y += _stepY;
                _tMaxY += _tDeltaY;
            }
            if (_tMaxZ <= nextT)
            {
                Z += _stepZ;
                _tMaxZ += _tDeltaZ;
            }

            return true;
        }

        private static double InitialBoundaryT(double origin, double delta, int cell, int step)
        {
            if (step == 0)
            {
                return double.PositiveInfinity;
            }

            var boundary = step > 0 ? cell + 1 : cell;
            return (boundary - origin) / delta;
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
