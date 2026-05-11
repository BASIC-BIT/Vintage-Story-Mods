using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Small modal that prompts for an image URL when the user clicks "Set from URL..." on the
/// character sheet's headshot row. Stays out of the main dialog so the bio editing surface
/// isn't crowded by a permanent text field.
/// </summary>
public sealed class HeadshotUrlPromptDialog : GuiDialog
{
    private const double DialogWidth = 460;
    private readonly Action<string> _onSubmit;

    public HeadshotUrlPromptDialog(ICoreClientAPI capi, Action<string> onSubmit) : base(capi)
    {
        _onSubmit = onSubmit;
        Compose();
    }

    public override string ToggleKeyCombinationCode => null;

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    // Render above the bio dialog (which is 0.25).
    public override double DrawOrder => 0.5;

    private void Compose()
    {
        SingleComposer?.Dispose();

        var contentTop = GuiStyle.TitleBarHeight + 12;
        var rowHeight = 28;
        var hintBounds = ElementBounds.Fixed(0, contentTop, DialogWidth - 20, 22);
        var inputBounds = ElementBounds.Fixed(0, contentTop + 24, DialogWidth - 20, rowHeight);
        var buttonsTop = inputBounds.fixedY + rowHeight + 14;
        var bodyHeight = buttonsTop + rowHeight + 6;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        SingleComposer = capi.Gui.CreateCompo("thebasics-headshot-url-prompt", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("thebasics:headshot-url-prompt-title"), OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddStaticText(Lang.Get("thebasics:headshot-url-prompt-hint"), CairoFont.WhiteSmallishText().WithFontSize(12f), hintBounds)
            .AddTextInput(inputBounds, _ => { }, CairoFont.TextInput(), "urlInput")
            .AddSmallButton(Lang.Get("Cancel"), OnCancel, ElementBounds.Fixed(0, buttonsTop, 120, rowHeight))
            .AddSmallButton(Lang.Get("thebasics:headshot-url-prompt-submit"), OnSubmit, ElementBounds.Fixed(DialogWidth - 142, buttonsTop, 120, rowHeight), EnumButtonStyle.Normal, "submitButton")
            .EndChildElements()
            .Compose(focusFirstElement: true);
    }

    private bool OnSubmit()
    {
        var url = (SingleComposer?.GetTextInput("urlInput")?.GetText() ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(url))
        {
            return true;
        }

        _onSubmit?.Invoke(url);
        TryClose();
        return true;
    }

    private bool OnCancel()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarCloseClicked()
    {
        TryClose();
    }
}
