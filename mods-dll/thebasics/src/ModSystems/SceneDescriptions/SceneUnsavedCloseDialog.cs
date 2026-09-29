using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneUnsavedCloseDialog : GuiDialog
{
    private readonly Action _discard;
    private readonly Action _save;
    private readonly Action _closed;

    internal SceneUnsavedCloseDialog(ICoreClientAPI capi, Action discard, Action save, Action closed) : base(capi)
    {
        _discard = discard;
        _save = save;
        _closed = closed;

        var bounds = ElementBounds.Fixed(0, 0, 560, 135).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = capi.Gui.CreateCompo("thebasics-scene-unsaved-close", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(bounds)
            .AddDialogTitleBar(Lang.Get("thebasics:scene-close-unsaved-title"), () => TryClose())
            .BeginChildElements(bounds)
            .AddStaticText(Lang.Get("thebasics:scene-close-unsaved-confirm"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(10, 42, 540, 28))
            .AddSmallButton(Lang.Get("thebasics:scene-close-discard"), () => Choose(_discard), ElementBounds.Fixed(10, 88, 170, 30))
            .AddSmallButton(Lang.Get("thebasics:scene-close-save"), () => Choose(_save), ElementBounds.Fixed(195, 88, 170, 30))
            .AddSmallButton(Lang.Get("thebasics:scene-close-go-back"), () => Choose(null), ElementBounds.Fixed(380, 88, 170, 30))
            .EndChildElements().Compose();
    }

    public override string ToggleKeyCombinationCode => null;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override bool UnregisterOnClose => true;

    private bool Choose(Action action)
    {
        TryClose();
        action?.Invoke();
        return true;
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        _closed?.Invoke();
        Dispose();
    }
}
