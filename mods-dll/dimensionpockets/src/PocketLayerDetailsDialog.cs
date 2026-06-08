using System;
using Vintagestory.API.Client;

namespace PocketDimensions;

internal sealed class PocketLayerDetailsDialog : GuiDialog
{
    private const double DialogWidth = 520;

    private readonly string _title;
    private readonly string _hint;
    private readonly bool _showLayerIndex;
    private readonly int _layerIndex;
    private readonly Action<int, string> _onSubmit;

    public PocketLayerDetailsDialog(
        ICoreClientAPI capi,
        string title,
        string hint,
        bool showLayerIndex,
        int layerIndex,
        Action<int, string> onSubmit)
        : base(capi)
    {
        _title = title;
        _hint = hint;
        _showLayerIndex = showLayerIndex;
        _layerIndex = layerIndex;
        _onSubmit = onSubmit;
        Compose();
    }

    public override string ToggleKeyCombinationCode => null;

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.56;

    private void Compose()
    {
        SingleComposer?.Dispose();

        var contentTop = GuiStyle.TitleBarHeight + 12;
        var rowHeight = 28;
        var fieldGap = 54;
        var y = contentTop;
        var width = DialogWidth - 20;

        var bodyHeight = contentTop + 48 + rowHeight + 48 + rowHeight + 12;
        if (_showLayerIndex)
        {
            bodyHeight += fieldGap;
        }

        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        var composer = capi.Gui.CreateCompo("pocket-layer-details", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(_title, OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds);

        if (_showLayerIndex)
        {
            composer
                .AddStaticText("Layer index", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, width, 20))
                .AddTextInput(ElementBounds.Fixed(0, y + 20, 160, rowHeight), _ => { }, CairoFont.TextInput(), "layerIndex");
            y += fieldGap;
        }

        composer
            .AddStaticText("Display name", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, width, 20))
            .AddTextInput(ElementBounds.Fixed(0, y + 20, width, rowHeight), _ => { }, CairoFont.TextInput(), "displayName");
        y += fieldGap;

        composer
            .AddStaticText(_hint, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, width, 42));
        y += 52;

        SingleComposer = composer
            .AddSmallButton("Cancel", OnCancel, ElementBounds.Fixed(0, y, 120, rowHeight))
            .AddSmallButton("Save", OnSubmit, ElementBounds.Fixed(DialogWidth - 142, y, 120, rowHeight), EnumButtonStyle.Normal, "save")
            .EndChildElements()
            .Compose(focusFirstElement: true);
    }

    private bool OnSubmit()
    {
        var index = _layerIndex;
        var indexText = Text("layerIndex");
        if (_showLayerIndex && !string.IsNullOrWhiteSpace(indexText) && !TryParseLayerIndex(indexText, out index))
        {
            return true;
        }

        _onSubmit?.Invoke(index, Text("displayName").Trim());
        TryClose();
        return true;
    }

    private string Text(string key)
    {
        return SingleComposer?.GetTextInput(key)?.GetText() ?? string.Empty;
    }

    private static bool TryParseLayerIndex(string value, out int index)
    {
        return int.TryParse((value ?? string.Empty).Trim(), out index);
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
