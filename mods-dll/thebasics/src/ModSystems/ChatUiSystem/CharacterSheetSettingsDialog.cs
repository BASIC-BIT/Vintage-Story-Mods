using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class CharacterSheetSettingsDialog : GuiDialog
{
    private readonly Action<int> _onSave;

    public CharacterSheetSettingsDialog(ICoreClientAPI api, bool serverDefault, int choice, Action<int> onSave) : base(api)
    {
        _onSave = onSave;
        var body = ElementBounds.Fixed(0, 0, 420, 160).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = api.Gui.CreateCompo("thebasics-character-sheet-settings", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(body)
            .AddDialogTitleBar(Lang.Get("thebasics:charsheet-settings-title"), () => TryClose())
            .BeginChildElements(body)
            .AddStaticText(Lang.Get("thebasics:charsheet-settings-autoopen-label"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 45, 400, 24))
            .AddDropDown(
                new[] { "default", "on", "off" },
                new[] {
                    Lang.Get("thebasics:charsheet-autoopen-default", Lang.Get(serverDefault ? "thebasics:charsheet-autoopen-on" : "thebasics:charsheet-autoopen-off")),
                    Lang.Get("thebasics:charsheet-autoopen-on"),
                    Lang.Get("thebasics:charsheet-autoopen-off")
                },
                choice is >= 0 and <= 2 ? choice : 0,
                (_, _) => { }, ElementBounds.Fixed(0, 72, 400, 30), "autoopen")
            .AddSmallButton(Lang.Get("Cancel"), () => TryClose(), ElementBounds.Fixed(0, 115, 110, 30))
            .AddSmallButton(Lang.Get("Save"), Save, ElementBounds.Fixed(290, 115, 110, 30))
            .EndChildElements()
            .Compose(focusFirstElement: false);
    }

    public override string ToggleKeyCombinationCode => null;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override double DrawOrder => 0.55;

    private bool Save()
    {
        var choice = SingleComposer.GetDropDown("autoopen").SelectedValue switch
        {
            "on" => 1,
            "off" => 2,
            _ => 0
        };
        _onSave(choice);
        TryClose();
        return true;
    }
}
