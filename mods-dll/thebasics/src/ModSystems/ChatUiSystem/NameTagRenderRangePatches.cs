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
/// This patch syncs the field from the property before each render frame so that
/// server-configured <c>NametagRenderRange</c> actually takes effect.
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
            if (configuredRange > 0)
            {
                RenderRangeFieldRef(__instance) = configuredRange;
            }

            if (ChatUiSystem.DoNametagsRequireLineOfSight())
            {
                var capi = ___entity?.World?.Api as ICoreClientAPI;
                var localPlayerEntity = capi?.World?.Player?.Entity;
                if (localPlayerEntity != null && ___entity != null && localPlayerEntity.EntityId != ___entity.EntityId &&
                    !CanSeeCached(___entity.World, localPlayerEntity, ___entity))
                {
                    return false;
                }
            }
        }
        catch
        {
            // Crash-safe: never break nametag rendering.
        }

        return true;
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
            var canSee = VisibilityUtils.HasLineOfSight(world, observer, target);
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
