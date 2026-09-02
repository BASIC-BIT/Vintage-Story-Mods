using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneDescriptionDialog : GuiDialog
{
    private const double DialogWidth = 520;
    private const double BodyHeight = 260;
    private readonly Action<SceneDescriptionData> _onSave;
    private readonly Action _onClose;
    private bool _closing;

    public SceneDescriptionDialog(ICoreClientAPI capi, SceneDescriptionData data, Action<SceneDescriptionData> onSave, Action onClose) : base(capi)
    {
        _onSave = onSave;
        _onClose = onClose;
        Compose((data ?? new SceneDescriptionData()).Clone().Normalize());
    }

    public override string ToggleKeyCombinationCode => "thebasicsscenedescription";

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    public override bool TryClose()
    {
        var closed = base.TryClose();
        if (closed && !_closing)
        {
            _closing = true;
            _onClose?.Invoke();
        }

        return closed;
    }

    private void Compose(SceneDescriptionData data)
    {
        var top = GuiStyle.TitleBarHeight + 12;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth, top + BodyHeight + 150).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var titleLabelBounds = ElementBounds.Fixed(0, top, DialogWidth - 20, 22);
        var titleInputBounds = ElementBounds.Fixed(0, top + 24, DialogWidth - 20, 30);
        var kindLabelBounds = ElementBounds.Fixed(0, top + 66, 180, 22);
        var kindBounds = ElementBounds.Fixed(0, top + 90, 220, 30);
        var bodyLabelBounds = ElementBounds.Fixed(0, top + 132, DialogWidth - 20, 22);
        var textAreaBounds = ElementBounds.Fixed(0, top + 156, DialogWidth - 20, BodyHeight);
        var buttonY = top + BodyHeight + 170;
        var kindValues = new[] { "environmental", "ooc" };
        var kindNames = new[]
        {
            Lang.Get("thebasics:scene-description-kind-environmental"),
            Lang.Get("thebasics:scene-description-kind-ooc"),
        };

        SingleComposer = capi.Gui.CreateCompo("thebasics-scene-description", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("thebasics:scene-description-editor-title"), OnTitleBarClose)
            .BeginChildElements(bodyBounds)
            .AddStaticText(Lang.Get("thebasics:scene-description-title-label"), CairoFont.WhiteSmallText(), titleLabelBounds)
            .AddTextInput(titleInputBounds, null, CairoFont.TextInput(), "title")
            .AddStaticText(Lang.Get("thebasics:scene-description-kind-label"), CairoFont.WhiteSmallText(), kindLabelBounds)
            .AddDropDown(kindValues, kindNames, data.Kind == SceneDescriptionKind.OocNotice ? 1 : 0, null, kindBounds, "kind")
            .AddStaticText(Lang.Get("thebasics:scene-description-body-label"), CairoFont.WhiteSmallText(), bodyLabelBounds)
            .AddTextArea(textAreaBounds, null, CairoFont.TextInput(), "body")
            .AddSmallButton(Lang.Get("thebasics:scene-description-cancel"), OnCancelButton, ElementBounds.Fixed(0, buttonY, 120, 30))
            .AddSmallButton(Lang.Get("thebasics:scene-description-save"), OnSave, ElementBounds.Fixed(DialogWidth - 140, buttonY, 120, 30))
            .EndChildElements()
            .Compose(focusFirstElement: false);

        SingleComposer.GetTextInput("title").SetMaxLength(SceneDescriptionData.MaxTitleLength);
        SingleComposer.GetTextInput("title").SetValue(data.Title);
        SingleComposer.GetTextArea("body").SetMaxLength(SceneDescriptionData.MaxBodyLength);
        SingleComposer.GetTextArea("body").SetValue(data.Body, setCaretPosToEnd: false);
        SingleComposer.FocusElement(SingleComposer.GetTextArea("body").TabIndex);
    }

    private bool OnSave()
    {
        var data = new SceneDescriptionData
        {
            Title = SingleComposer.GetTextInput("title").GetText(),
            Body = SingleComposer.GetTextArea("body").GetText(),
            Kind = SingleComposer.GetDropDown("kind").SelectedValue == "ooc"
                ? SceneDescriptionKind.OocNotice
                : SceneDescriptionKind.Environmental,
        }.Normalize();

        _closing = true;
        base.TryClose();
        _onSave?.Invoke(data);
        return true;
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }

    private bool OnCancelButton()
    {
        TryClose();
        return true;
    }
}
