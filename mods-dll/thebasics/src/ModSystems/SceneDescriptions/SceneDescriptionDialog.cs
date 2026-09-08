using System;
using System.Globalization;
using Cairo;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneDescriptionDialog : GuiDialog
{
    private const double DialogWidth = 520;
    private const double BodyHeight = 260;
    private const double ButtonHeight = 30;
    private readonly Action<SceneDescriptionData, bool> _onSave;
    private readonly Action _onClose;
    private bool _closing;
    private bool _lockAfterSave;
    private readonly bool _canManageLock;
    private readonly Action _onUnlock;
    private readonly SceneDescriptionData _appearance;
    private readonly Shape[] _symbolShapes;

    public SceneDescriptionDialog(ICoreClientAPI capi, SceneDescriptionData data, bool canManageLock, Action<SceneDescriptionData, bool> onSave, Action onUnlock, Action onClose) : base(capi)
    {
        _onSave = onSave;
        _onUnlock = onUnlock;
        _canManageLock = canManageLock;
        _onClose = onClose;
        _appearance = (data ?? new SceneDescriptionData()).Clone().Normalize();
        _symbolShapes = new[] { SceneMarkerVisuals.LoadShape(capi, SceneMarkerSymbol.Exclamation),
            SceneMarkerVisuals.LoadShape(capi, SceneMarkerSymbol.Question), SceneMarkerVisuals.LoadShape(capi, SceneMarkerSymbol.Information) };
        Compose(_appearance);
    }

    public override string ToggleKeyCombinationCode => null;

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    // A fresh dialog is built per edit, so drop it from the GUI manager and free its composers on close.
    public override bool UnregisterOnClose => true;

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        Dispose();
    }

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
        var buttonY = top + BodyHeight + 170;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth + 290, buttonY + ButtonHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var titleLabelBounds = ElementBounds.Fixed(0, top, DialogWidth - 20, 22);
        var titleInputBounds = ElementBounds.Fixed(0, top + 24, DialogWidth - 20, 30);
        var kindLabelBounds = ElementBounds.Fixed(0, top + 66, 180, 22);
        var kindBounds = ElementBounds.Fixed(0, top + 90, 220, 30);
        var bodyLabelBounds = ElementBounds.Fixed(0, top + 132, DialogWidth - 20, 22);
        var textAreaBounds = ElementBounds.Fixed(0, top + 156, DialogWidth - 20, BodyHeight);
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
            .AddDynamicCustomDraw(ElementBounds.Fixed(530, top + 12, 260, 200), DrawPreview, "preview")
            .AddStaticText(Lang.Get("thebasics:scene-symbol"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 230, 260, 22))
            .AddTextToggleButtons(new[] { "!", "?", "i" }, CairoFont.WhiteSmallText().WithFontSize(26),
                index => { _appearance.Symbol = (SceneMarkerSymbol)index; RefreshPreview(); },
                new[] { ElementBounds.Fixed(530, top + 260, 80, 44), ElementBounds.Fixed(620, top + 260, 80, 44),
                    ElementBounds.Fixed(710, top + 260, 80, 44) }, "symbol")
            .AddStaticText(Lang.Get("thebasics:scene-icon-distance"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 330, 260, 22))
            .AddNumberInput(ElementBounds.Fixed(530, top + 360, 100, 30), null, CairoFont.TextInput(), "distance")
            .AddSwitch(value => { _appearance.UnlimitedIconDistance = value; RefreshPreview(); }, ElementBounds.Fixed(645, top + 360, 30, 30), "unlimited")
            .AddStaticText(Lang.Get("thebasics:scene-unlimited"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(684, top + 362, 110, 28))
            .AddSmallButton(Lang.Get("thebasics:scene-description-cancel"), OnCancelButton, ElementBounds.Fixed(0, buttonY, 120, ButtonHeight))
            .AddSmallButton(Lang.Get("thebasics:scene-description-save"), OnSave, ElementBounds.Fixed(DialogWidth - 140, buttonY, 120, ButtonHeight), key: "save")
            .AddSmallButton(Lang.Get(data.IsLocked ? "thebasics:scene-unlock" : "thebasics:scene-save-lock"), OnLockButton, ElementBounds.Fixed(530, buttonY, 260, ButtonHeight), key: "lock")
            .EndChildElements()
            .Compose(focusFirstElement: false);

        SingleComposer.ToggleButtonsSetValue("symbol", (int)data.Symbol);
        SingleComposer.GetNumberInput("distance").SetValue(data.IconDistance.ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetSwitch("unlimited").SetValue(data.UnlimitedIconDistance);
        RefreshPreview();
        SingleComposer.GetButton("lock").Enabled = _canManageLock;
        SingleComposer.GetButton("save").Enabled = !data.IsLocked;
        SingleComposer.GetTextInput("title").Enabled = !data.IsLocked;
        SingleComposer.GetTextArea("body").Enabled = !data.IsLocked;
        SingleComposer.GetDropDown("kind").Enabled = !data.IsLocked;
        SingleComposer.GetSwitch("unlimited").Enabled = !data.IsLocked;
        for (var i = 0; i < 3; i++) SingleComposer.GetToggleButton("symbol-" + i).Enabled = !data.IsLocked;
        SingleComposer.GetTextInput("title").SetMaxLength(SceneDescriptionData.MaxTitleLength);
        SingleComposer.GetTextInput("title").SetValue(data.Title);
        SingleComposer.GetTextArea("body").SetMaxLength(SceneDescriptionData.MaxBodyLength);
        SingleComposer.GetTextArea("body").SetValue(data.Body, setCaretPosToEnd: false);
        SingleComposer.FocusElement(SingleComposer.GetTextArea("body").TabIndex);
    }

    private void RefreshPreview()
    {
        if (SingleComposer?.Composed != true) return;
        SingleComposer.GetNumberInput("distance").Enabled = !_appearance.IsLocked && !_appearance.UnlimitedIconDistance;
        SingleComposer.GetCustomDraw("preview").Redraw();
    }

    private void DrawPreview(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var width = surface.Width;
        var height = surface.Height;
        ctx.SetSourceRGBA(0.07, 0.09, 0.12, 0.9);
        ctx.Paint();
        SceneMarkerVisuals.Draw(ctx, _symbolShapes[(int)_appearance.Symbol], width / 2.0 - 64, height / 2.0 - 64, 128);
    }

    private bool OnLockButton()
    {
        if (!_canManageLock) return false;
        if (!_appearance.IsLocked)
        {
            _lockAfterSave = true;
            var saved = OnSave();
            _lockAfterSave = false;
            return saved;
        }
        _closing = true;
        base.TryClose();
        _onUnlock?.Invoke();
        return true;
    }

    private bool OnSave()
    {
        if (_appearance.IsLocked) return false;
        var usesDistance = !_appearance.UnlimitedIconDistance;
        if (usesDistance &&
            (!float.TryParse(SingleComposer.GetNumberInput("distance").GetText(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var distance) || !float.IsFinite(distance) || distance < 1 || distance > 1024))
        {
            capi.TriggerIngameError(this, "scene-distance", Lang.Get("thebasics:scene-distance-invalid"));
            return false;
        }
        _appearance.IconDistance = !usesDistance ? _appearance.IconDistance :
            float.Parse(SingleComposer.GetNumberInput("distance").GetText(), CultureInfo.InvariantCulture);
        var data = new SceneDescriptionData
        {
            Appearance = _appearance.Appearance,
            Symbol = _appearance.Symbol,
            IconDistance = _appearance.IconDistance,
            UnlimitedIconDistance = _appearance.UnlimitedIconDistance,
            Title = SingleComposer.GetTextInput("title").GetText(),
            Body = SingleComposer.GetTextArea("body").GetText(),
            Kind = SingleComposer.GetDropDown("kind").SelectedValue == "ooc"
                ? SceneDescriptionKind.OocNotice
                : SceneDescriptionKind.Environmental,
        }.Normalize();

        _closing = true;
        base.TryClose();
        _onSave?.Invoke(data, _lockAfterSave);
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
