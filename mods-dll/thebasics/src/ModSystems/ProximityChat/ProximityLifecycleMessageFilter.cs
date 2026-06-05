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
    private readonly record struct FilterState(ICoreServerAPI Api, ModConfig Config, int? EffectiveProximityGroupId);

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
        var state = GetStateSnapshot();
        if (!ShouldHandle(chatType, player, state))
        {
            return true;
        }

        try
        {
            SendLifecycleMessageOutsideProximity(__instance, message, chatType, player, state.EffectiveProximityGroupId.GetValueOrDefault());
        }
        catch (Exception ex)
        {
            state.Api?.Logger.Warning($"THEBASICS: Failed while filtering vanilla lifecycle message from proximity chat; falling back to vanilla routing: {ex.GetType().Name}.");
            return true;
        }

        try
        {
            SendNearbyDeathMessageIfEnabled(message, chatType, player, state);
        }
        catch (Exception ex)
        {
            state.Api?.Logger.Warning($"THEBASICS: Suppressed vanilla lifecycle message from proximity chat, but failed to re-send nearby death message: {ex.GetType().Name}.");
        }

        return false;
    }

    private static FilterState GetStateSnapshot()
    {
        lock (Sync)
        {
            return new FilterState(_api, _config, _effectiveProximityGroupId);
        }
    }

    private static bool ShouldHandle(EnumChatType chatType, IServerPlayer player, FilterState state)
    {
        return player != null &&
               state.EffectiveProximityGroupId.HasValue &&
               IsLifecycleMessageType(chatType);
    }

    internal static bool IsLifecycleMessageType(EnumChatType chatType)
    {
        return chatType == EnumChatType.JoinLeave || chatType == EnumChatType.Notification;
    }

    private static void SendLifecycleMessageOutsideProximity(ServerMain server, string message, EnumChatType chatType, IServerPlayer player, int effectiveProximityGroupId)
    {
        var exceptPlayer = chatType == EnumChatType.JoinLeave ? player : null;
        foreach (var membership in (player.Groups ?? Array.Empty<PlayerGroupMembership>()).Where(membership => ShouldSendToPlayerGroup(membership, effectiveProximityGroupId)))
        {
            server.SendMessageToGroup(membership.GroupUid, message, chatType, exceptPlayer);
        }

        if (ShouldSendPublicGeneralMessage(
                server?.Clients.Count ?? int.MaxValue,
                MagicNum.PublicJoinLeaveDeathMessagesThreshold,
                effectiveProximityGroupId == GlobalConstants.GeneralChatGroup))
        {
            server.SendMessageToGeneral(message, chatType, exceptPlayer);
        }
    }

    internal static bool ShouldSendToPlayerGroup(PlayerGroupMembership membership, int? effectiveProximityGroupId)
    {
        return membership != null && ShouldSendToGroup(membership.GroupUid, effectiveProximityGroupId);
    }

    internal static bool ShouldSendToGroup(int groupId, int? effectiveProximityGroupId)
    {
        return !effectiveProximityGroupId.HasValue || groupId != effectiveProximityGroupId.Value;
    }

    internal static bool ShouldSendPublicGeneralMessage(int connectedClientCount, int publicMessageThreshold, bool generalIsProximity)
    {
        return !generalIsProximity && connectedClientCount < publicMessageThreshold;
    }

    private static void SendNearbyDeathMessageIfEnabled(string message, EnumChatType chatType, IServerPlayer player, FilterState state)
    {
        if (!TryPrepareNearbyDeathMessage(chatType, player, state, out var groupId, out var origin, out var range))
        {
            return;
        }

        foreach (var recipient in GetNearbyRecipients(state.Api, groupId, origin, range))
        {
            recipient.SendMessage(groupId, message, chatType);
        }
    }

    private static bool TryPrepareNearbyDeathMessage(EnumChatType chatType, IServerPlayer player, FilterState state, out int groupId, out BlockPos origin, out int range)
    {
        groupId = state.EffectiveProximityGroupId ?? 0;
        origin = null;
        range = 0;

        if (!ShouldReemitNearbyDeathMessage(chatType, state.Config?.EnableNearbyDeathMessagesInProximityChat == true) ||
            !state.EffectiveProximityGroupId.HasValue ||
            state.Api?.World == null ||
            player?.Entity == null)
        {
            return false;
        }

        origin = player.Entity.Pos.AsBlockPos;
        range = GetNearbyDeathMessageRange(state.Config);
        return true;
    }

    private static IEnumerable<IServerPlayer> GetNearbyRecipients(ICoreServerAPI api, int groupId, BlockPos origin, int range)
    {
        return api.World.AllOnlinePlayers
            .OfType<IServerPlayer>()
            .Where(recipient => IsNearbyRecipient(recipient, origin, range) && IsRecipientInGroup(recipient, groupId));
    }

    private static bool IsNearbyRecipient(IServerPlayer recipient, BlockPos origin, int range)
    {
        return recipient?.Entity?.Pos != null &&
               recipient.Entity.Pos.AsBlockPos.ManhattanDistance(origin) < range;
    }

    internal static bool IsRecipientInGroup(IServerPlayer recipient, int groupId)
    {
        if (recipient == null)
        {
            return false;
        }

        if (groupId == GlobalConstants.GeneralChatGroup)
        {
            return true;
        }

        return recipient.Groups?.Any(membership => membership?.GroupUid == groupId) == true;
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
}
