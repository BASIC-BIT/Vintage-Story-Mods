using HarmonyLib;
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

    public static void Prefix(EntityBehaviorNameTag __instance)
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
        }
        catch
        {
            // Crash-safe: never break nametag rendering.
        }
    }
}
