using System;
using System.Linq;
using thebasics.Models;
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

    public SetupWizardClientController(ICoreClientAPI api, SafeClientNetworkChannel channel, Action openAdvanced = null)
    {
        _api = api;
        _channel = channel;
        _openAdvanced = openAdvanced;
        _invitationListener = api.Event.RegisterGameTickListener(_ => TryShowInvitation(), 250);
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

    public void Open()
    {
        if (_disposed || _opening || CurrentDialog?.Draft.IsSavePending == true) return;
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
        if (result.Kind == SetupWizardResultKind.SaveResult)
        {
            if (CurrentDialog == null || result.RunId != _runId || !_requests.Accept(result.RequestId)) return;
            CurrentDialog.SetResult(result);
            return;
        }
        if (!_requests.Accept(result.RequestId)) return;
        _opening = false;
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
                    _api.ShowChatMessage(result.Message ?? "Setup authorization was lost. Your draft is kept; reopen with /basic setup after access is restored.");
                }
            }
            else _api.ShowChatMessage(result.Message ?? "Setup is not available.");
            return;
        }
        if (result.Kind != SetupWizardResultKind.Open) return;
        _authorized = true;
        if (IsCaptureOnly && !_captureRequested) Close();
        if (CurrentDialog != null)
        {
            _runId = result.RunId;
            CurrentDialog.Draft.CancelRequest();
            CurrentDialog.SetSession(result);
            CurrentDialog.TryOpen();
            return;
        }
        _pendingInvitation = null;
        _invitation?.Dispose();
        _invitation = null;
        _runId = result.RunId;
        IsCaptureOnly = _captureRequested;
        _captureRequested = false;
        CurrentDialog = new SetupWizardDialog(_api, new SetupWizardDraft(result.Values), _runId, result.IsDedicated,
            IsCaptureOnly ? null : Save, IsCaptureOnly ? null : Track, Close, IsCaptureOnly ? null : _openAdvanced, captureOnly: IsCaptureOnly);
        CurrentDialog.SetSession(result);
        CurrentDialog.TryOpen();
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
            if (!_disposed) _api.ShowChatMessage("Setup could not open. Try /basic setup again.");
        });
        _channel.SendPacketSafely(new TheBasicsSetupWizardRequestMessage
        {
            Kind = kind, RequestId = id, RunId = runId, CaptureOnly = _captureRequested
        }, () => _requests.Fail(id));
    }

    private void Save()
    {
        var dialog = CurrentDialog;
        if (_disposed || dialog == null || IsCaptureOnly || !_authorized) return;
        void Failure(string message, string action)
        {
            if (_disposed || CurrentDialog != dialog) return;
            dialog.Draft.CancelRequest();
            dialog.SetRequestFailure(message);
            Track("review", action, null);
        }
        var id = BeginRequest(() => Failure("Could not send the save. Your changes are kept; try again.", "save_failed"),
            () => Failure("The server did not acknowledge the save. Your changes are kept; try again.", "save_timeout"));
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
