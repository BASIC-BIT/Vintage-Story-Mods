using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace thebasics.Patches;

/// <summary>
/// Crash guard for a vanilla SurvivalMod NRE observed on VS 1.20.12 when connecting to a server.
/// The underlying issue appears to be a missing handbook behavior on some collectibles.
/// 
/// We patch defensively: ensure the behavior exists before the game tries to write extra handbook sections.
/// </summary>
[HarmonyPatch(typeof(TradeHandbookInfo), "AddTraderHandbookInfo")]
public static class TradeHandbookInfoPatches
{
    // Target method is private; Harmony can still patch it by name.
    public static bool Prefix(TradeHandbookInfo __instance, TradeItem val, string traderName, string title)
    {
        try
        {
            var capi = AccessTools.Field(typeof(TradeHandbookInfo), "capi").GetValue(__instance) as ICoreClientAPI;
            if (capi?.World == null || val == null)
            {
                // Let vanilla run.
                return true;
            }

            if (!val.Resolve(capi.World, "tradehandbookinfo " + (traderName ?? "")))
            {
                return false;
            }

            var collobj = val.ResolvedItemstack?.Collectible;
            if (collobj == null)
            {
                return false;
            }

            // Ensure handbook behavior exists (vanilla assumes it does).
            var bh = collobj.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>();
            if (bh == null)
            {
                bh = new CollectibleBehaviorHandbookTextAndExtraInfo(collobj);
                bh.OnLoaded(capi);

                if (collobj.CollectibleBehaviors != null)
                {
                    collobj.CollectibleBehaviors = collobj.CollectibleBehaviors.Append(bh);
                }
                else
                {
                    collobj.CollectibleBehaviors = new CollectibleBehavior[] { bh };
                }
            }

            var section = bh.ExtraHandBookSections?.FirstOrDefault(ele => ele.Title == title);
            if (section == null)
            {
                section = new ExtraHandbookSection { Title = title, TextParts = Array.Empty<string>() };
                bh.ExtraHandBookSections = (bh.ExtraHandBookSections ?? Array.Empty<ExtraHandbookSection>()).Append(section);
            }

            section.TextParts = (section.TextParts ?? Array.Empty<string>()).Append(traderName ?? "");
            return false;
        }
        catch
        {
            // Fail closed: never crash the client due to our safety patch.
            return false;
        }
    }
}
