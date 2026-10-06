using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.Analytics;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat;

public partial class RPProximityChatSystem
{
    private const string SetupWizardSeenKey = "thebasics:setup-invitation-seen-v1";
    private SetupWizardState _setupWizardState;
    private bool _setupWizardRuntimeApplyFailed;
    private readonly SetupWizardInvitationState _setupWizardInvitation = new();
    private readonly Dictionary<string, SetupWizardRun> _setupWizardRuns = new(StringComparer.Ordinal);

    private sealed class SetupWizardRun
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public int Sequence { get; set; }
        public long LastSaveRequestId { get; set; }
        public TheBasicsSetupWizardResultMessage LastSaveResult { get; set; }
    }

    private TextCommandResult HandleOpenSetupWizardCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
            return TextCommandResult.Error("This command can only be used by a player.");
        OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        return TextCommandResult.Success("Opening The BASICs setup.");
    }

    private void TryOfferSetupWizard(IServerPlayer player)
    {
        if (!_setupWizardInvitation.TryOffer(player.PlayerUID,
                player.GetModData(SetupWizardSeenKey, false), player.HasPrivilege(Privilege.root))) return;
        var run = new SetupWizardRun();
        _setupWizardRuns[player.PlayerUID] = run;
        SendSetupWizardResult(player, SetupWizardResultKind.Invitation, 0, run.Id, true);
        TrackSetupWizard(player, run, "invitation", "offered");
    }

    internal void OnSetupWizardRequest(IServerPlayer player, TheBasicsSetupWizardRequestMessage request)
    {
        if (player == null) return;
        if (!player.HasPrivilege(Privilege.root))
        {
            _setupWizardRuns.Remove(player.PlayerUID);
            SendSetupWizardResult(player, SetupWizardResultKind.Denied, request?.RequestId ?? 0,
                request?.RunId?.Length <= 64 ? request.RunId : null, false, "You do not have permission to configure The BASICs.");
            return;
        }
        var requestErrors = SetupWizardState.ValidateRequest(request);
        if (requestErrors.Count > 0)
        {
            SendSetupWizardResult(player, SetupWizardResultKind.Denied, request?.RequestId ?? 0,
                request?.RunId?.Length <= 64 ? request.RunId : null, false, "Invalid setup request.");
            return;
        }
        if (request.Kind == SetupWizardRequestKind.Open)
        {
            if (request.CaptureOnly)
            {
                SendSetupWizardResult(player, SetupWizardResultKind.Open, request.RequestId,
                    Guid.NewGuid().ToString("N"), true);
                return;
            }
            var newRun = _setupWizardRuns.TryGetValue(player.PlayerUID, out var existingRun)
                ? existingRun : new SetupWizardRun();
            _setupWizardRuns[player.PlayerUID] = newRun;
            player.SetModData(SetupWizardSeenKey, true);
            SendSetupWizardResult(player, SetupWizardResultKind.Open, request.RequestId, newRun.Id, true);
            TrackSetupWizard(player, newRun, "start", "opened");
            return;
        }
        if (!_setupWizardRuns.TryGetValue(player.PlayerUID, out var run) || run.Id != request.RunId)
        {
            SendSetupWizardResult(player, SetupWizardResultKind.Denied, request.RequestId, request.RunId,
                false, "This setup session has ended. Open it again with /basic setup.");
            return;
        }
        switch (request.Kind)
        {
            case SetupWizardRequestKind.InviteStart:
                player.SetModData(SetupWizardSeenKey, true);
                SendSetupWizardResult(player, SetupWizardResultKind.Open, request.RequestId, run.Id, true);
                TrackSetupWizard(player, run, "invitation", "started");
                break;
            case SetupWizardRequestKind.InviteDismiss:
                player.SetModData(SetupWizardSeenKey, true);
                TrackSetupWizard(player, run, "invitation", "dismissed");
                _setupWizardRuns.Remove(player.PlayerUID);
                if (player.ConnectionState == EnumClientState.Playing)
                    player.SendMessage(GlobalConstants.GeneralChatGroup,
                        "You can configure The BASICs later with /basic setup.", EnumChatType.Notification);
                break;
            case SetupWizardRequestKind.Save:
                SaveSetupWizard(player, request, run);
                break;
            case SetupWizardRequestKind.Track:
                if (AnalyticsService.IsValidSetupWizardJourney(run.Id, run.Sequence + 1,
                        request.StepId, request.JourneyAction, request.ChoiceId))
                    TrackSetupWizard(player, run, request.StepId, request.JourneyAction, request.ChoiceId);
                break;
            case SetupWizardRequestKind.Closed:
                TrackSetupWizard(player, run, "finish", "closed");
                _setupWizardRuns.Remove(player.PlayerUID);
                break;
        }
    }

    private void SaveSetupWizard(IServerPlayer player, TheBasicsSetupWizardRequestMessage request, SetupWizardRun run)
    {
        if (request.RequestId <= run.LastSaveRequestId)
        {
            if (request.RequestId == run.LastSaveRequestId && run.LastSaveResult != null)
                _serverConfigChannel?.SendPacket(run.LastSaveResult, player);
            return;
        }
        _setupWizardState ??= new SetupWizardState(Config);
        if (!_setupWizardState.TryValidatePatch(Config, request, out var draft, out var changedKeys,
                out var conflicts, out var errors))
        {
            SendSetupWizardResult(player, SetupWizardResultKind.SaveResult, request.RequestId, run.Id,
                false, string.Join("\n", errors), conflicts);
            TrackSetupWizard(player, run, "review", conflicts.Count > 0 ? "conflict" : "save_failed",
                result: conflicts.Count > 0 ? "conflict" : "validation_failed");
            return;
        }
        var sightErrors = ConfigAdminSettingRegistry.ValidateResolvedSightBlockPatterns(draft, API.World.Blocks);
        string saveError = null;
        if (sightErrors.Count > 0 || !TryPersistConfigDraft(draft, out saveError))
        {
            SendSetupWizardResult(player, SetupWizardResultKind.SaveResult, request.RequestId, run.Id,
                false, sightErrors.Count > 0 ? string.Join("\n", sightErrors) : saveError);
            TrackSetupWizard(player, run, "review", "save_failed",
                result: sightErrors.Count > 0 ? "validation_failed" : "write_failed");
            return;
        }
        try
        {
            ApplyConfigChangeSideEffects(changedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase));
            BroadcastClientConfigs();
        }
        catch (Exception exception)
        {
            _setupWizardRuntimeApplyFailed = true;
            API.Logger?.Error($"The BASICs: setup was saved, but live application failed ({exception.GetType().Name}). Restart required.");
        }
        run.LastSaveRequestId = request.RequestId;
        run.LastSaveResult = CreateSetupWizardResult(SetupWizardResultKind.SaveResult, request.RequestId, run.Id, true,
            _setupWizardRuntimeApplyFailed
                ? "Saved The BASICs setup, but live application failed. Restart the server to apply the saved settings."
                : "Saved The BASICs setup.");
        TrackSetupWizard(player, run, "review", "saved",
            result: _setupWizardRuntimeApplyFailed || run.LastSaveResult.RestartRequiredKeys.Count > 0 ? "saved_pending_restart" : "saved_live");
        _serverConfigChannel?.SendPacket(run.LastSaveResult, player);
    }

    private TheBasicsSetupWizardResultMessage SendSetupWizardResult(IServerPlayer player, SetupWizardResultKind kind,
        long requestId, string runId, bool success, string message = null, List<string> conflicts = null)
    {
        var result = CreateSetupWizardResult(kind, requestId, runId, success, message, conflicts);
        _serverConfigChannel?.SendPacket(result, player);
        return result;
    }

    private TheBasicsSetupWizardResultMessage CreateSetupWizardResult(SetupWizardResultKind kind,
        long requestId, string runId, bool success, string message = null, List<string> conflicts = null)
    {
        var includeSnapshot = kind != SetupWizardResultKind.Denied;
        if (includeSnapshot) _setupWizardState ??= new SetupWizardState(Config);
        return new TheBasicsSetupWizardResultMessage
        {
            Kind = kind, RequestId = requestId, RunId = runId, Success = success, Message = message,
            Values = includeSnapshot ? GetConfigAdminValues(Config).Where(value => SetupWizardCatalog.IsSettingKey(value.Key)).ToList() : [],
            RestartRequiredKeys = includeSnapshot ? _setupWizardState.GetPendingRestartKeys(Config).ToList() : [],
            IsDedicated = API.Server?.IsDedicated == true,
            ConflictKeys = conflicts ?? [],
            RuntimeApplyFailed = includeSnapshot && _setupWizardRuntimeApplyFailed
        };
    }

    private void TrackSetupWizard(IServerPlayer player, SetupWizardRun run, string step, string action,
        string choice = null, string result = null)
    {
        if (run.Sequence >= 10000) return;
        AnalyticsService.TrackSetupWizardJourney(run.Id, ++run.Sequence, step, action, choice, result,
            _setupWizardRuntimeApplyFailed || _setupWizardState?.GetPendingRestartKeys(Config).Count > 0, player.PlayerUID);
    }
}
