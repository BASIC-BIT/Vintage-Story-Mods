namespace thebasics.ModSystems.ChatUiSystem;

internal sealed class DialogDraftState
{
    private string _baseline;
    private string _submitted;

    public DialogDraftState(string snapshot) => _baseline = snapshot;

    public bool TryBeginRequest(string snapshot)
    {
        if (_submitted != null) return false;
        _submitted = snapshot;
        return true;
    }

    // Compare with the submitted draft for an acknowledgement, and the last
    // server snapshot for background refreshes. Only acknowledgements finish a request.
    public bool ApplyResponse(string current, string incoming, bool success, bool completesRequest = true)
    {
        var reference = completesRequest && success ? _submitted ?? _baseline : _baseline;
        var preserve = !success || current != reference;
        if (success) _baseline = incoming;
        if (completesRequest) _submitted = null;
        return preserve;
    }

    public bool IsDirty(string current) => current != _baseline;
}
