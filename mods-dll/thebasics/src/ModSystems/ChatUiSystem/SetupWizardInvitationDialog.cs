using System;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class SetupWizardInvitationDialog : GuiDialog
{
    private readonly Action _onStart;
    private readonly Action _onDismiss;
    private bool _handled;
    private bool _disposing;

    public SetupWizardInvitationDialog(ICoreClientAPI api, Action onStart, Action onDismiss) : base(api)
    {
        _onStart = onStart;
        _onDismiss = onDismiss;
        var body = ElementBounds.Fixed(0, 0, 450, 150).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = api.Gui.CreateCompo("thebasics-setup-invitation", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(body)
            .AddDialogTitleBar("The BASICs setup", () => TryClose())
            .BeginChildElements(body)
            .AddStaticText("Configure chat, teleportation, and notifications?", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 38, 450, 48))
            .AddSmallButton("Start setup", Start, ElementBounds.Fixed(110, 100, 160, 30))
            .AddSmallButton("Not now", () => TryClose(), ElementBounds.Fixed(280, 100, 150, 30))
            .EndChildElements().Compose(focusFirstElement: false);
    }

    public override string ToggleKeyCombinationCode => null;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    private bool Start()
    {
        if (_handled) return true;
        _handled = true;
        base.TryClose();
        _onStart?.Invoke();
        return true;
    }

    public override bool TryClose()
    {
        var closed = base.TryClose();
        if (closed && !_handled && !_disposing)
        {
            _handled = true;
            _onDismiss?.Invoke();
        }
        return closed;
    }

    public override void Dispose()
    {
        _disposing = true;
        base.TryClose();
        base.Dispose();
    }
}
