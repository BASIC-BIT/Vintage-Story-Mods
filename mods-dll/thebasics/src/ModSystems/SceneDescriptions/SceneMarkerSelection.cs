using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace thebasics.ModSystems.SceneDescriptions;

// Vanilla walks only blocks along the ray, so a symbol raised out of its block needs a supplemental hit test.
internal sealed class SceneMarkerSelection : IDisposable
{
    private const string PatchId = "thebasics.scene-marker-selection";
    private static SceneMarkerSelection _active;
    [ThreadStatic] private static int _pickingDepth;
    private readonly ICoreClientAPI _api;
    private readonly HashSet<SceneDescriptionBlockEntity> _markers = new();
    private readonly Harmony _harmony = new(PatchId);

    internal SceneMarkerSelection(ICoreClientAPI api)
    {
        _api = api;
        _active = this;
        _harmony.Patch(AccessTools.Method(typeof(SystemMouseInWorldInteractions), "UpdateCurrentSelection"),
            prefix: new HarmonyMethod(typeof(SceneMarkerSelection), nameof(BeginPicking)),
            finalizer: new HarmonyMethod(typeof(SceneMarkerSelection), nameof(EndPicking)));
        _harmony.Patch(AccessTools.Method(typeof(AABBIntersectionTest), nameof(AABBIntersectionTest.GetSelectedBlock),
            new[] { typeof(float), typeof(BlockFilter), typeof(bool) }),
            postfix: new HarmonyMethod(typeof(SceneMarkerSelection), nameof(AfterSelection)));
    }

    internal void Register(SceneDescriptionBlockEntity marker) => _markers.Add(marker);
    internal void Unregister(SceneDescriptionBlockEntity marker) => _markers.Remove(marker);
    private static void BeginPicking() => _pickingDepth++;
    private static void EndPicking() => _pickingDepth--;
    internal static bool ShouldSupplement(bool picking, bool collision, bool sameWorld) => picking && !collision && sameWorld;

    private static void AfterSelection(AABBIntersectionTest __instance, float maxDistance, BlockFilter filter, bool testCollide, ref BlockSelection __result)
    {
        var active = _active;
        if (active == null || !ShouldSupplement(_pickingDepth > 0, testCollide, ReferenceEquals(__instance.bsTester, active._api.World))) return;
        active.Select(__instance.ray, maxDistance, filter, ref __result);
    }

    private void Select(Ray ray, float range, BlockFilter filter, ref BlockSelection result)
    {
        var player = _api.World.Player?.Entity;
        if (player == null) return;
        var nearest = (double)range;
        if (result != null)
            nearest = Math.Min(nearest, ray.origin.DistanceTo(result.Position.ToVec3d().Add(result.HitPosition)));
        foreach (var marker in _markers)
        {
            if (marker.Pos.dimension != player.Pos.Dimension || (filter != null && !filter(marker.Pos, marker.Block))) continue;
            var center = marker.Pos.ToVec3d().Add(0.5, 0.65 + marker.Data.HeightOffset, 0.5);
            if (marker.Data.GetIconOpacity(player.Pos.XYZ.DistanceTo(marker.Pos.ToVec3d().Add(0.5, 0.65, 0.5))) <= 0) continue;
            var hit = IntersectBox(ray, center, nearest, marker.Data.SelectionHalfExtent);
            if (hit == null) continue;
            nearest = ray.origin.DistanceTo(hit);
            result = new BlockSelection { Position = marker.Pos.Copy(), Block = marker.Block, Face = BlockFacing.UP,
                HitPosition = hit.SubCopy(marker.Pos.ToVec3d()), SelectionBoxIndex = 0 };
        }
    }

    internal static Vec3d IntersectBox(Ray ray, Vec3d center, double maxDistance, double halfExtent = 0.48)
    {
        var length = ray.Length;
        if (!double.IsFinite(length) || length <= 0) return null;
        var near = 0.0;
        var far = maxDistance;
        for (var axis = 0; axis < 3; axis++)
        {
            var origin = ray.origin[axis] - center[axis];
            var direction = ray.dir[axis] / length;
            if (Math.Abs(direction) < 1e-9)
            {
                if (Math.Abs(origin) > halfExtent) return null;
                continue;
            }
            var a = (-halfExtent - origin) / direction;
            var b = (halfExtent - origin) / direction;
            near = Math.Max(near, Math.Min(a, b));
            far = Math.Min(far, Math.Max(a, b));
            if (near > far) return null;
        }
        return ray.origin.AddCopy(ray.dir.X * near / length, ray.dir.Y * near / length, ray.dir.Z * near / length);
    }

    public void Dispose()
    {
        _harmony.UnpatchAll(PatchId);
        if (ReferenceEquals(_active, this)) _active = null;
        _markers.Clear();
    }
}
