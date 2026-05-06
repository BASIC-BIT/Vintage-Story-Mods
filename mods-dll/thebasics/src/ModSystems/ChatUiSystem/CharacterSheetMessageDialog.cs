using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public class CharacterSheetMessageDialog : GuiDialog
{
    private readonly string _message;

    public CharacterSheetMessageDialog(ICoreClientAPI capi, string message) : base(capi)
    {
        _message = message;
        ComposeDialog();
    }

    public override string ToggleKeyCombinationCode => null;

    public override double DrawOrder => 2;

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();

        var textBounds = ElementBounds.Fixed(0, 32, 360, 30);
        var buttonBounds = ElementBounds.Fixed(120, 76, 120, 30);
        var bodyBounds = ElementBounds.Fixed(0, 0, 360, 130).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        SingleComposer = capi.Gui.CreateCompo("thebasics-character-sheet-message", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("thebasics:charsheet-gui-title"), OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddStaticText(_message, CairoFont.WhiteSmallText(), EnumTextOrientation.Center, textBounds)
            .AddSmallButton(Lang.Get("OK"), OnOk, buttonBounds)
            .EndChildElements()
            .Compose();
    }

    private bool OnOk()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarCloseClicked()
    {
        TryClose();
    }
}
