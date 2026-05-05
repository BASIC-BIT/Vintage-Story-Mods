using System.Collections.Generic;
using HarmonyLib;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Workaround for a vanilla VS bug: <c>EntityBehaviorNameTag.OnRenderFrame</c> checks the
/// <c>renderRange</c> field (hardcoded to 999, never updated) instead of the <c>RenderRange</c>
/// property (which reads from synced <c>WatchedAttributes</c>).
/// This patch syncs the field from the property before each render frame and
/// explicitly suppresses rendering when target-only, range, or LOS checks fail.
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
            // Sync the field from the WatchedAttributes-backed property so the
            // vanilla distance check uses the server-configured value.
            var configuredRange = __instance.RenderRange;
            if (configuredRange >= 0)
            {
                // Vanilla defaults this to 999; 0 is an intentional hide-all configuration.
                RenderRangeFieldRef(__instance) = configuredRange;
            }

            if (ShouldSuppressNametag(__instance, ___entity, configuredRange))
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

    private static bool ShouldSuppressNametag(EntityBehaviorNameTag instance, Entity entity, int configuredRange)
    {
        if (!TryGetRemoteNametagContext(entity, out var capi, out var localPlayerEntity))
        {
            return false;
        }

        return IsMissingRequiredTarget(instance, capi, entity) ||
               IsOutsideRenderRange(configuredRange, localPlayerEntity, entity) ||
               IsMissingRequiredLineOfSight(localPlayerEntity, entity);
    }

    private static bool TryGetRemoteNametagContext(Entity entity, out ICoreClientAPI capi, out Entity localPlayerEntity)
    {
        capi = entity?.World?.Api as ICoreClientAPI;
        localPlayerEntity = capi?.World?.Player?.Entity;
        return capi != null && localPlayerEntity != null && entity != null && localPlayerEntity.EntityId != entity.EntityId;
    }

    private static bool IsMissingRequiredTarget(EntityBehaviorNameTag instance, ICoreClientAPI capi, Entity target)
    {
        return instance.ShowOnlyWhenTargeted && capi.World.Player.CurrentEntitySelection?.Entity != target;
    }

    private static bool IsOutsideRenderRange(int renderRange, Entity localPlayerEntity, Entity target)
    {
        return renderRange >= 0 && localPlayerEntity.Pos.SquareDistanceTo(target.Pos) >= (double)renderRange * renderRange;
    }

    private static bool IsMissingRequiredLineOfSight(Entity localPlayerEntity, Entity target)
    {
        return ChatUiSystem.DoNametagsRequireLineOfSight() &&
               !CanSeeCached(target.World, localPlayerEntity, target);
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
