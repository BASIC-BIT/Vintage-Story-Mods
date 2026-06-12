using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Teleportation;

internal static class TeleportBackGlobalRecorder
{
    private const string HarmonyId = "thebasics.teleport-back-recorder";
    private static Harmony _harmony;

    public static void Patch()
    {
        if (_harmony != null)
        {
            return;
        }

        _harmony = new Harmony(HarmonyId);
        var original = AccessTools.Method(typeof(EntityPlayer), nameof(EntityPlayer.TeleportToDouble), [typeof(double), typeof(double), typeof(double), typeof(Action)]);
        var prefix = new HarmonyMethod(typeof(TeleportBackGlobalRecorder), nameof(RecordBeforeTeleport));
        _harmony.Patch(original, prefix: prefix);
    }

    public static void Unpatch()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
    }

    private static void RecordBeforeTeleport(EntityPlayer __instance)
    {
        if (__instance?.World?.Api is not ICoreServerAPI || __instance.Player is not IServerPlayer player)
        {
            return;
        }

        TeleportBackUtil.RecordPreviousLocation(player);
    }
}
