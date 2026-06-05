using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace thebasics.ModSystems.ProximityChat;

internal static class ProximityLifecycleMessageFilter
{
    private static readonly object Sync = new();
    private static ICoreServerAPI _api;
    private static ModConfig _config;
    private static int? _effectiveProximityGroupId;
    private static bool _patchApplied;
    private static string _harmonyId;

    public static void Apply(Harmony harmony, ICoreServerAPI api, ModConfig config, int effectiveProximityGroupId)
    {
        if (harmony == null)
        {
            return;
        }

        lock (Sync)
        {
            _api = api;
            _config = config;
            _effectiveProximityGroupId = effectiveProximityGroupId;

            if (_patchApplied)
            {
                return;
            }

            try
            {
                var original = GetJoinLeaveDeathMessageMethod();
                if (original == null)
                {
                    api?.Logger.Warning("THEBASICS: Could not patch vanilla lifecycle notifications out of proximity chat; Vintage Story joinLeaveDeathMessage was not found.");
                    return;
                }

                var prefix = new HarmonyMethod(typeof(ProximityLifecycleMessageFilter), nameof(PrefixJoinLeaveDeathMessage));
                harmony.Patch(original, prefix: prefix);
                _patchApplied = true;
                _harmonyId = harmony.Id;
            }
            catch (Exception ex)
            {
                api?.Logger.Warning($"THEBASICS: Could not patch vanilla lifecycle notifications out of proximity chat: {ex.GetType().Name}.");
                return;
            }

            api?.Logger.Notification("THEBASICS: Filtering vanilla join, leave, and death notifications out of proximity chat.");
        }
    }

    public static void Unpatch(Harmony harmony)
    {
        if (harmony == null)
        {
            return;
        }

        lock (Sync)
        {
            if (!_patchApplied || _harmonyId != harmony.Id)
            {
                return;
            }

            var original = GetJoinLeaveDeathMessageMethod();
            if (original != null)
            {
                harmony.Unpatch(original, HarmonyPatchType.Prefix, harmony.Id);
            }

            _patchApplied = false;
            _harmonyId = null;
            _effectiveProximityGroupId = null;
            _config = null;
            _api = null;
        }
    }

    private static MethodInfo GetJoinLeaveDeathMessageMethod()
    {
        return AccessTools.Method(typeof(ServerMain), nameof(ServerMain.joinLeaveDeathMessage));
    }

    private static bool PrefixJoinLeaveDeathMessage(ServerMain __instance, string message, EnumChatType chatType, IServerPlayer player)
    {
        if (!ShouldHandle(chatType, player))
        {
            return true;
        }

        try
        {
            SendLifecycleMessageOutsideProximity(__instance, message, chatType, player);
        }
        catch (Exception ex)
        {
            _api?.Logger.Warning($"THEBASICS: Failed while filtering vanilla lifecycle message from proximity chat; falling back to vanilla routing: {ex.GetType().Name}.");
            return true;
        }

        try
        {
            SendNearbyDeathMessageIfEnabled(message, chatType, player);
        }
        catch (Exception ex)
        {
            _api?.Logger.Warning($"THEBASICS: Suppressed vanilla lifecycle message from proximity chat, but failed to re-send nearby death message: {ex.GetType().Name}.");
        }

        return false;
    }

    private static bool ShouldHandle(EnumChatType chatType, IServerPlayer player)
    {
        return player != null &&
               _effectiveProximityGroupId.HasValue &&
               IsLifecycleMessageType(chatType);
    }

    internal static bool IsLifecycleMessageType(EnumChatType chatType)
    {
        return chatType == EnumChatType.JoinLeave || chatType == EnumChatType.Notification;
    }

    private static void SendLifecycleMessageOutsideProximity(ServerMain server, string message, EnumChatType chatType, IServerPlayer player)
    {
        var exceptPlayer = chatType == EnumChatType.JoinLeave ? player : null;
        foreach (var membership in (player.Groups ?? Array.Empty<PlayerGroupMembership>()).Where(ShouldSendToPlayerGroup))
        {
            server.SendMessageToGroup(membership.GroupUid, message, chatType, exceptPlayer);
        }

        if (ShouldSendPublicGeneralMessage(
                server?.Clients.Count ?? int.MaxValue,
                MagicNum.PublicJoinLeaveDeathMessagesThreshold,
                _effectiveProximityGroupId == GlobalConstants.GeneralChatGroup))
        {
            server.SendMessageToGeneral(message, chatType, exceptPlayer);
        }
    }

    internal static bool ShouldSendToPlayerGroup(PlayerGroupMembership membership)
    {
        return membership != null && ShouldSendToGroup(membership.GroupUid);
    }

    internal static bool ShouldSendToGroup(int groupId)
    {
        return !_effectiveProximityGroupId.HasValue || groupId != _effectiveProximityGroupId.Value;
    }

    internal static bool ShouldSendPublicGeneralMessage(int connectedClientCount, int publicMessageThreshold, bool generalIsProximity)
    {
        return !generalIsProximity && connectedClientCount < publicMessageThreshold;
    }

    private static void SendNearbyDeathMessageIfEnabled(string message, EnumChatType chatType, IServerPlayer player)
    {
        if (!TryPrepareNearbyDeathMessage(chatType, player, out var groupId, out var origin, out var range))
        {
            return;
        }

        foreach (var recipient in GetNearbyRecipients(origin, range))
        {
            recipient.SendMessage(groupId, message, chatType);
        }
    }

    private static bool TryPrepareNearbyDeathMessage(EnumChatType chatType, IServerPlayer player, out int groupId, out BlockPos origin, out int range)
    {
        groupId = _effectiveProximityGroupId ?? 0;
        origin = null;
        range = 0;

        if (!ShouldReemitNearbyDeathMessage(chatType, _config?.EnableNearbyDeathMessagesInProximityChat == true) ||
            !_effectiveProximityGroupId.HasValue ||
            _api?.World == null ||
            player?.Entity == null)
        {
            return false;
        }

        origin = player.Entity.Pos.AsBlockPos;
        range = GetNearbyDeathMessageRange(_config);
        return true;
    }

    private static IEnumerable<IServerPlayer> GetNearbyRecipients(BlockPos origin, int range)
    {
        return _api.World.AllOnlinePlayers
            .OfType<IServerPlayer>()
            .Where(recipient => IsNearbyRecipient(recipient, origin, range));
    }

    private static bool IsNearbyRecipient(IServerPlayer recipient, BlockPos origin, int range)
    {
        return recipient?.Entity?.Pos != null &&
               recipient.Entity.Pos.AsBlockPos.ManhattanDistance(origin) < range;
    }

    internal static bool ShouldReemitNearbyDeathMessage(EnumChatType chatType, bool enabled)
    {
        return enabled && chatType == EnumChatType.Notification;
    }

    private static int GetNearbyDeathMessageRange(ModConfig config)
    {
        if (config?.ProximityChatModeDistances != null &&
            config.ProximityChatModeDistances.TryGetValue(ProximityChatMode.Normal, out var range) &&
            range > 0)
        {
            return range;
        }

        return 35;
    }

    internal static void ConfigureForTests(int? effectiveProximityGroupId)
    {
        _effectiveProximityGroupId = effectiveProximityGroupId;
    }
}
