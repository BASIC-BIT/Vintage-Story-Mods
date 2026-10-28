using System;
using System.Linq;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.Utilities;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class SetupWizardClientController : IDisposable
{
    private readonly ICoreClientAPI _api;
    private readonly SafeClientNetworkChannel _channel;
    private readonly Action _openAdvanced;
    private readonly DialogRequestTracker _requests = new();
    private readonly long _invitationListener;
    private SetupWizardInvitationDialog _invitation;
    private TheBasicsSetupWizardResultMessage _pendingInvitation;
    private string _runId;
    private bool _disposed;
    private bool _opening;
    private bool _captureRequested;
    private bool _authorized = true;
    private SetupWizardRequestKind? _maintenance;
    private string _destination;
    private bool _helpOpen;

    public SetupWizardClientController(ICoreClientAPI api, SafeClientNetworkChannel channel, Action openAdvanced = null)
    {
        _api = api;
        _channel = channel;
        _openAdvanced = openAdvanced;
        _invitationListener = api.Event.RegisterGameTickListener(_ => { TryShowInvitation(); ReturnFromHelp(); }, 250);
    }

    public SetupWizardDialog CurrentDialog { get; private set; }
    public bool IsCaptureOnly { get; private set; }

    public void OpenForCapture()
    {
        if (_disposed || _opening || CurrentDialog != null)
            throw new InvalidOperationException("Close the existing setup dialog before opening a QA capture.");
        _captureRequested = true;
        RequestOpen(SetupWizardRequestKind.Open, null);
    }

    public void Open(string destination = null)
    {
        _destination = SetupWizardState.ValidDestination(destination);
        if (_disposed) return;
        if (CurrentDialog != null && (_opening || _maintenance.HasValue || CurrentDialog.Draft.IsSavePending))
        {
            NavigateDestination();
            if (!_maintenance.HasValue) CurrentDialog.TryOpen();
            return;
        }
        if (_opening) return;
        if (IsCaptureOnly) Close();
        CurrentDialog?.Suspend();
        RequestOpen(SetupWizardRequestKind.Open, null);
    }

    public void HandleResult(TheBasicsSetupWizardResultMessage result)
    {
        if (_disposed || result == null) return;
        if (result.Kind == SetupWizardResultKind.Invitation)
        {
            if (CurrentDialog == null && _invitation == null) _pendingInvitation = result;
            TryShowInvitation();
            return;
        }
        if (result.Kind == SetupWizardResultKind.Open && result.Success && result.RequestId == 0 && CurrentDialog != null &&
            (_opening || _maintenance.HasValue || CurrentDialog.Draft.IsSavePending))
        {
            _destination = SetupWizardState.ValidDestination(result.Destination);
            NavigateDestination();
            if (!_maintenance.HasValue) CurrentDialog.TryOpen();
            return;
        }
        if (result.Kind == SetupWizardResultKind.SaveResult)
        {
            if (CurrentDialog == null || result.RunId != _runId || !_requests.Accept(result.RequestId)) return;
            CurrentDialog.SetResult(result);
            return;
        }
        if (!_requests.Accept(result.RequestId)) return;
        _opening = false;
        if (_maintenance.HasValue)
        {
            var action = _maintenance.Value;
            _maintenance = null;
            if (CurrentDialog != null && result.Kind != SetupWizardResultKind.Denied)
            {
                if (result.Kind == SetupWizardResultKind.ReloadPartiallyApplied || result.Success && result.Kind == SetupWizardResultKind.MaintenanceResult)
                {
                    if (action == SetupWizardRequestKind.Reload && result.Success) CurrentDialog.Draft.ReplaceAfterReload(result);
                    CurrentDialog.SetSession(result);
                }
                CurrentDialog.SetRequestFailure(result.Message ?? "This action is unavailable. Your edits are still here.");
                CurrentDialog.TryOpen();
                return;
            }
        }
        if (result.Kind == SetupWizardResultKind.Denied || !result.Success)
        {
            _captureRequested = false;
            if (CurrentDialog != null)
            {
                CurrentDialog.SetRequestFailure(result.Message);
                if (result.Kind == SetupWizardResultKind.Denied)
                {
                    _authorized = false;
                    CurrentDialog.Suspend();
                    _api.ShowChatMessage(result.Message ?? "You no longer have permission to change these settings. Your edits are still here. Reopen with /basic setup when you have access again.");
                }
            }
            else _api.ShowChatMessage(result.Message ?? "Setup is not available.");
            return;
        }
        if (result.Kind != SetupWizardResultKind.Open) return;
        _authorized = true;
        _destination = SetupWizardState.ValidDestination(result.Destination) ?? _destination;
        if (IsCaptureOnly && !_captureRequested) Close();
        if (CurrentDialog != null)
        {
            _runId = result.RunId;
            CurrentDialog.Draft.CancelRequest();
            try
            {
                if (CurrentDialog.Draft.MergeSaveReadback(result))
                    CurrentDialog.SetRequestFailure("Save status checked. Any remaining changes are still in your draft.");
                CurrentDialog.SetSession(result);
                NavigateDestination();
                CurrentDialog.TryOpen();
            }
            catch (Exception error)
            {
                _api.Logger.Error("The BASICs setup could not reopen: {0}", error);
                CurrentDialog.Suspend();
                _api.ShowChatMessage("Could not reopen setup. Your edits are still here. Try /basic setup again.");
            }
            return;
        }
        _pendingInvitation = null;
        _invitation?.Dispose();
        _invitation = null;
        _runId = result.RunId;
        IsCaptureOnly = _captureRequested;
        _captureRequested = false;
        try
        {
            CurrentDialog = new SetupWizardDialog(_api, new SetupWizardDraft(result.Values, result.Languages, result.SheetFields), _runId, result.IsDedicated,
                IsCaptureOnly ? null : Save, IsCaptureOnly ? null : Track, Close, IsCaptureOnly ? null : _openAdvanced, captureOnly: IsCaptureOnly, onMaintenance: IsCaptureOnly ? null : Maintain, onHelp: OpenHelp);
            CurrentDialog.SetSession(result);
            NavigateDestination();
            CurrentDialog.TryOpen();
        }
        catch (Exception error)
        {
            _api.Logger.Error("The BASICs setup could not open: {0}", error);
            Close();
            _api.ShowChatMessage("Could not open setup. Use /tb config advanced to edit your settings.");
            return;
        }
        Track("hub", "viewed", null);
    }

    private void TryShowInvitation()
    {
        if (_disposed || _pendingInvitation == null || CurrentDialog != null || _invitation != null) return;
        if (_api.Gui.LoadedGuis.Any(dialog => dialog.DialogType == EnumDialogType.Dialog && dialog.IsOpened())) return;
        var offered = _pendingInvitation;
        _invitation = new SetupWizardInvitationDialog(_api,
            () => AcknowledgeInvitation(offered.RunId, true),
            () => AcknowledgeInvitation(offered.RunId, false));
        if (_invitation.TryOpen())
        {
            _pendingInvitation = null;
            _channel.TrySendPacketWithoutQueue(new TheBasicsSetupWizardRequestMessage
            {
                Kind = SetupWizardRequestKind.Track, RunId = offered.RunId,
                StepId = "invitation", JourneyAction = "viewed"
            });
        }
        else
        {
            _invitation.Dispose();
            _invitation = null;
        }
    }

    private void AcknowledgeInvitation(string runId, bool start)
    {
        if (_disposed) return;
        var invitation = _invitation;
        _invitation = null;
        invitation?.Dispose();
        if (start) RequestOpen(SetupWizardRequestKind.InviteStart, runId);
        else _channel.SendPacketSafely(new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.InviteDismiss, RunId = runId });
    }

    private long BeginRequest(Action failure, Action timeout = null)
    {
        return _requests.Begin(failure, (callback, delay) => _api.Event.RegisterCallback(_ => { if (!_disposed) callback(); }, delay), timeout);
    }

    private void RequestOpen(SetupWizardRequestKind kind, string runId)
    {
        _opening = true;
        var id = BeginRequest(() =>
        {
            _opening = false;
            _captureRequested = false;
            if (CurrentDialog?.Draft.HasUnconfirmedSave == true)
                CurrentDialog.SetRequestFailure("Could not check the save. Your edits are still here. Use /basic setup to check again before saving.");
            if (!_disposed) _api.ShowChatMessage("Could not open setup. Try /basic setup again.");
        });
        _channel.SendPacketSafely(new TheBasicsSetupWizardRequestMessage
        {
            Kind = kind, RequestId = id, RunId = runId, CaptureOnly = _captureRequested, Destination = _destination
        }, () => _requests.Fail(id));
    }

    private void NavigateDestination()
    {
        if (_destination == null || CurrentDialog == null) return;
        CurrentDialog.ShowPage(_destination);
        _destination = null;
    }

    internal void Maintain(SetupWizardRequestKind kind)
    {
        var dialog = CurrentDialog;
        if (_disposed || dialog == null || IsCaptureOnly || !_authorized || _opening || _maintenance.HasValue ||
            !dialog.SupportsMaintenance || dialog.Draft.IsSavePending || dialog.Draft.HasUnconfirmedSave ||
            kind is not (SetupWizardRequestKind.MarkReviewed or SetupWizardRequestKind.Reload)) return;
        _maintenance = kind;
        dialog.Suspend();
        var id = BeginRequest(() =>
        {
            _maintenance = null;
            dialog.SetRequestFailure("The action was not confirmed. Your edits are still here. Reopen setup to check the server settings.");
            dialog.TryOpen();
        });
        _channel.SendPacketSafely(new TheBasicsSetupWizardRequestMessage { Kind = kind, RequestId = id, RunId = _runId }, () => _requests.Fail(id));
    }

    internal void OpenHelp()
    {
        if (_disposed || CurrentDialog == null) return;
        CurrentDialog.Suspend();
        _helpOpen = true;
        HandbookGuide.Open(_api, HandbookGuide.OverviewPage);
    }

    internal void ReturnFromHelp()
    {
        if (!_helpOpen || CurrentDialog == null || _disposed) return;
        if (_api.Gui.LoadedGuis.Any(dialog => dialog != CurrentDialog && dialog.DialogType == EnumDialogType.Dialog && dialog.IsOpened())) return;
        _helpOpen = false;
        CurrentDialog.TryOpen();
    }

    private void Save()
    {
        var dialog = CurrentDialog;
        if (_disposed || dialog == null || IsCaptureOnly || !_authorized || _opening || _maintenance.HasValue || dialog.Draft.HasUnconfirmedSave) return;
        void Failure(string message, string action)
        {
            if (_disposed || CurrentDialog != dialog) return;
            dialog.Draft.CancelRequest();
            dialog.SetRequestFailure(message);
            Track("review", action, null);
        }
        var id = BeginRequest(() => Failure("Could not send your changes. Your edits are still here. Try again.", "save_failed"),
            () =>
            {
                if (_disposed || CurrentDialog != dialog) return;
                dialog.Draft.MarkSaveTimedOut();
                dialog.SetRequestFailure("The server has not confirmed the save. Checking the current settings...");
                Track("review", "save_timeout", null);
                RequestOpen(SetupWizardRequestKind.Open, _runId);
            });
        dialog.RememberSaveNavigation();
        var request = dialog.Draft.CreateSaveRequest(id, _runId);
        Track("review", "save_requested", null);
        _channel.SendPacketSafely(request, () => _requests.Fail(id));
    }

    private void Track(string stepId, string action, string choice)
    {
        if (_disposed || IsCaptureOnly || string.IsNullOrEmpty(_runId)) return;
        _channel.TrySendPacketWithoutQueue(new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Track, RunId = _runId, StepId = stepId, JourneyAction = action, ChoiceId = choice
        });
    }

    private void Close()
    {
        if (_disposed) return;
        if (!IsCaptureOnly) _channel.TrySendPacketWithoutQueue(new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Closed, RunId = _runId });
        _requests.Reset();
        var dialog = CurrentDialog;
        CurrentDialog = null;
        _runId = null;
        _maintenance = null;
        _helpOpen = false;
        _destination = null;
        IsCaptureOnly = false;
        dialog?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _requests.Reset();
        _api.Event.UnregisterGameTickListener(_invitationListener);
        _pendingInvitation = null;
        _invitation?.Dispose();
        _invitation = null;
        CurrentDialog?.Dispose();
        CurrentDialog = null;
    }
}
