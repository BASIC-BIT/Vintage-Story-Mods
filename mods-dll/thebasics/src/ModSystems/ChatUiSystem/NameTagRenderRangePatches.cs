using System.Collections.Generic;
using HarmonyLib;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Adds configurable line-of-sight gating to vanilla nametag rendering.
/// Range and target-only settings are applied when player entities are loaded or spawned.
/// </summary>
[HarmonyPatch(typeof(EntityBehaviorNameTag), "OnRenderFrame")]
public static class NameTagRenderRangePatches
{
    private static readonly AccessTools.FieldRef<EntityBehaviorNameTag, int> RenderRangeFieldRef =
        AccessTools.FieldRefAccess<EntityBehaviorNameTag, int>("renderRange");

    private const long PurgeIntervalMs = 10_000;
    private const long StaleThresholdMs = 5_000;
    private static readonly Dictionary<long, (bool canSee, long nextCheckMs)> LosCache = new();
    private static long _nextPurgeMs;

    public static bool Prefix(Entity ___entity)
    {
        try
        {
            if (ShouldSuppressNametag(___entity))
            {
                return false;
            }
        }
        catch
        {
            // Crash-safe: never break nametag rendering.
        }

        return true;
    }

    internal static void ApplyConfiguredNametagSettings(Entity entity, bool showOnlyWhenTargeted, int renderRange)
    {
        var behavior = entity?.GetBehavior<EntityBehaviorNameTag>();
        if (behavior == null)
        {
            return;
        }

        behavior.ShowOnlyWhenTargeted = showOnlyWhenTargeted;
        behavior.RenderRange = renderRange;

        if (renderRange >= 0)
        {
            // Vanilla's render loop checks this private field, not the watched-attribute property.
            RenderRangeFieldRef(behavior) = renderRange;
        }
    }

    private static bool ShouldSuppressNametag(Entity entity)
    {
        if (!ChatUiSystem.DoNametagsRequireLineOfSight())
        {
            return false;
        }

        if (!TryGetRemoteNametagContext(entity, out _, out var localPlayerEntity))
        {
            return false;
        }

        return !CanSeeCached(entity.World, localPlayerEntity, entity);
    }

    private static bool TryGetRemoteNametagContext(Entity entity, out ICoreClientAPI capi, out Entity localPlayerEntity)
    {
        capi = entity?.World?.Api as ICoreClientAPI;
        localPlayerEntity = capi?.World?.Player?.Entity;
        return capi != null && localPlayerEntity != null && entity != null && localPlayerEntity.EntityId != entity.EntityId;
    }

    private static bool CanSeeCached(IWorldAccessor world, Entity observer, Entity target)
    {
        if (world == null || observer == null || target == null)
        {
            return false;
        }

        var nowMs = world.ElapsedMilliseconds;
        if (nowMs >= _nextPurgeMs)
        {
            _nextPurgeMs = nowMs + PurgeIntervalMs;
            PurgeStaleEntries(nowMs);
        }

        if (!LosCache.TryGetValue(target.EntityId, out var entry) || nowMs >= entry.nextCheckMs)
        {
            var canSee = VisibilityUtils.HasLineOfSight(world, observer, target, failOpen: false, useMultiPointTargets: true);
            var refreshMs = canSee ? 250L : 500L;
            entry = (canSee, nowMs + refreshMs);
            LosCache[target.EntityId] = entry;
        }

        return entry.canSee;
    }

    internal static void ClearCache()
    {
        LosCache.Clear();
        _nextPurgeMs = 0;
    }

    private static void PurgeStaleEntries(long nowMs)
    {
        List<long> toRemove = null;
        foreach (var kvp in LosCache)
        {
            if (nowMs - kvp.Value.nextCheckMs > StaleThresholdMs)
            {
                toRemove ??= new List<long>();
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove == null)
        {
            return;
        }

        foreach (var entityId in toRemove)
        {
            LosCache.Remove(entityId);
        }
    }
}
