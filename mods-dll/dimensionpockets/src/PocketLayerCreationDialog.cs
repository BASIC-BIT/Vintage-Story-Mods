using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace PocketDimensions;

internal sealed class PocketLayerCreationDialog : GuiDialog
{
    private static int _dialogIndex;
    private readonly string _title;
    private readonly string _text;
    private readonly Action<bool> _onAnswer;

    public PocketLayerCreationDialog(ICoreClientAPI capi, string text, Action<bool> onAnswer)
        : this(capi, "Create Pocket Layer", text, onAnswer)
    {
    }

    public PocketLayerCreationDialog(ICoreClientAPI capi, string title, string text, Action<bool> onAnswer)
        : base(capi)
    {
        _title = title;
        _text = text;
        _onAnswer = onAnswer;
        Compose();
    }

    public override double DrawOrder => 2.0;

    public override string ToggleKeyCombinationCode => null;

    private void Compose()
    {
        var textBounds = ElementStdBounds.Rowed(0.4f, 0, EnumDialogArea.LeftFixed).WithFixedWidth(480);
        var bgBounds = ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding);
        var textUtil = new TextDrawUtil();
        var font = CairoFont.WhiteSmallText();
        var textHeight = textUtil.GetMultilineTextHeight(font, _text, textBounds.fixedWidth);
        var buttonRow = (float)((textHeight + 80) / 80);
        SingleComposer = capi.Gui.CreateCompo("pocket-layer-confirm-" + _dialogIndex++, ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(_title, OnTitleBarClose)
            .BeginChildElements(bgBounds)
            .AddStaticText(_text, font, textBounds)
            .AddSmallButton(Lang.Get("No"), () => Answer(false), ElementStdBounds.MenuButton(buttonRow).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(6))
            .AddSmallButton(Lang.Get("Yes"), () => Answer(true), ElementStdBounds.MenuButton(buttonRow).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(6))
            .EndChildElements()
            .Compose();
    }

    private bool Answer(bool create)
    {
        _onAnswer?.Invoke(create);
        TryClose();
        return true;
    }

    private void OnTitleBarClose()
    {
        _onAnswer?.Invoke(false);
        TryClose();
    }
}
