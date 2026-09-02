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

    public static bool Prefix(EntityBehaviorNameTag __instance, Entity ___entity)
    {
        try
        {
            if (ShouldSuppressNametag(__instance, ___entity))
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

        var localPlayerEntityId = (entity.World?.Api as ICoreClientAPI)?.World?.Player?.Entity?.EntityId;
        behavior.ShowOnlyWhenTargeted = ResolveShowOnlyWhenTargeted(
            showOnlyWhenTargeted,
            localPlayerEntityId,
            entity.EntityId);
        behavior.RenderRange = renderRange;

        if (renderRange >= 0)
        {
            // Vanilla's render loop checks this private field, not the watched-attribute property.
            RenderRangeFieldRef(behavior) = renderRange;
        }
    }

    private static bool ShouldSuppressNametag(EntityBehaviorNameTag behavior, Entity entity)
    {
        if (!TryGetNametagContext(entity, out var capi, out var localPlayerEntity))
        {
            return false;
        }

        if (localPlayerEntity.EntityId == entity.EntityId)
        {
            // Target-only is a remote-player identity rule. A player cannot target themselves, so
            // leaving the synced flag set would permanently hide their own nametag in third person.
            // Clear it defensively here because later server-side identity refreshes can rewrite the
            // watched attribute after the client initially applies its config.
            if (behavior.ShowOnlyWhenTargeted)
            {
                behavior.ShowOnlyWhenTargeted = false;
            }

            return false;
        }

        if (!ChatUiSystem.DoNametagsRequireLineOfSight())
        {
            return false;
        }

        var isTargeted = capi.World.Player.CurrentEntitySelection?.Entity == entity;
        var distanceSquared = localPlayerEntity.Pos.SquareDistanceTo(entity.Pos);
        if (!ShouldEvaluateLineOfSight(
                localPlayerEntity.EntityId,
                entity.EntityId,
                behavior.ShowOnlyWhenTargeted,
                isTargeted,
                RenderRangeFieldRef(behavior),
                distanceSquared))
        {
            // Vanilla will suppress the nametag without needing an LOS result. Always delegate
            // self-rendering too, because vanilla deliberately allows it in third person.
            return false;
        }

        return !CanSeeCached(entity.World, localPlayerEntity, entity);
    }

    /// <summary>
    /// Mirrors vanilla's final target/range gate so LOS work only runs when its answer can affect
    /// rendering. Returning false means "delegate to vanilla", not "hide the nametag".
    /// </summary>
    internal static bool ShouldEvaluateLineOfSight(
        long localPlayerEntityId,
        long targetEntityId,
        bool showOnlyWhenTargeted,
        bool isTargeted,
        int renderRange,
        double distanceSquared)
    {
        if (localPlayerEntityId == targetEntityId)
        {
            return false;
        }

        return (!showOnlyWhenTargeted || isTargeted)
               && (double)(renderRange * renderRange) > distanceSquared;
    }

    internal static bool ResolveShowOnlyWhenTargeted(
        bool configuredShowOnlyWhenTargeted,
        long? localPlayerEntityId,
        long targetEntityId)
    {
        return configuredShowOnlyWhenTargeted && localPlayerEntityId != targetEntityId;
    }

    private static bool TryGetNametagContext(Entity entity, out ICoreClientAPI capi, out Entity localPlayerEntity)
    {
        capi = entity?.World?.Api as ICoreClientAPI;
        localPlayerEntity = capi?.World?.Player?.Entity;
        return capi != null && localPlayerEntity != null && entity != null;
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
