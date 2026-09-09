using System;
using System.Globalization;
using System.Linq;
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
        _symbolShapes = Enum.GetValues<SceneMarkerSymbol>().Select(symbol => SceneMarkerVisuals.LoadShape(capi, symbol)).ToArray();
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
        var buttonY = top + 570;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth + 290, buttonY + ButtonHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var titleLabelBounds = ElementBounds.Fixed(0, top, DialogWidth - 20, 22);
        var titleInputBounds = ElementBounds.Fixed(0, top + 24, DialogWidth - 20, 30);
        var kindLabelBounds = ElementBounds.Fixed(0, top + 66, 180, 22);
        var kindBounds = ElementBounds.Fixed(0, top + 90, 220, 30);
        var bodyLabelBounds = ElementBounds.Fixed(0, top + 132, DialogWidth - 20, 22);
        var textAreaBounds = ElementBounds.Fixed(0, top + 156, DialogWidth - 20, BodyHeight);
        var kindValues = new[] { "targeted", "nearby", "interaction" };
        var kindNames = new[]
        {
            Lang.Get("thebasics:scene-display-targeted"),
            Lang.Get("thebasics:scene-display-nearby"),
            Lang.Get("thebasics:scene-display-interaction"),
        };

        SingleComposer = capi.Gui.CreateCompo("thebasics-scene-description", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("thebasics:scene-description-editor-title"), OnTitleBarClose)
            .BeginChildElements(bodyBounds)
            .AddStaticText(Lang.Get("thebasics:scene-description-title-label"), CairoFont.WhiteSmallText(), titleLabelBounds)
            .AddTextInput(titleInputBounds, _ => RefreshPreview(), CairoFont.TextInput(), "title")
            .AddStaticText(Lang.Get("thebasics:scene-display-label"), CairoFont.WhiteSmallText(), kindLabelBounds)
            .AddDropDown(kindValues, kindNames, (int)data.Display, (_, _) => RefreshPreview(), kindBounds, "kind")
            .AddStaticText(Lang.Get("thebasics:scene-description-body-label"), CairoFont.WhiteSmallText(), bodyLabelBounds)
            .AddTextArea(textAreaBounds, _ => RefreshPreview(), CairoFont.TextInput(), "body")
            .AddDynamicCustomDraw(ElementBounds.Fixed(530, top + 12, 260, 200), DrawPreview, "preview")
            .AddStaticText(Lang.Get("thebasics:scene-symbol"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 212, 260, 22))
            .AddTextToggleButtons(new[] { "!", "?", "i", "●", "○", "◆" }, CairoFont.WhiteSmallText().WithFontSize(26),
                index => { _appearance.Symbol = (SceneMarkerSymbol)index; RefreshPreview(); },
                Enumerable.Range(0, 6).Select(index => ElementBounds.Fixed(530 + index * 44, top + 236, 40, 44)).ToArray(), "symbol")
            .AddStaticText(Lang.Get("thebasics:scene-color"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 290, 90, 22))
            .AddDropDown(new[] { "gold", "parchment", "blue", "green" }, new[] { Lang.Get("thebasics:scene-color-gold"), Lang.Get("thebasics:scene-color-parchment"), Lang.Get("thebasics:scene-color-blue"), Lang.Get("thebasics:scene-color-green") }, (int)data.Color,
                (value, _) => { _appearance.Color = value switch { "parchment" => SceneMarkerColor.Parchment, "blue" => SceneMarkerColor.Blue, "green" => SceneMarkerColor.Green, _ => SceneMarkerColor.Gold }; RefreshPreview(); }, ElementBounds.Fixed(620, top + 288, 170, 30), "color")
            .AddStaticText(Lang.Get("thebasics:scene-icon-distance"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 342, 260, 22))
            .AddNumberInput(ElementBounds.Fixed(530, top + 366, 100, 30), null, CairoFont.TextInput(), "distance")
            .AddSwitch(value => { _appearance.UnlimitedIconDistance = value; RefreshPreview(); }, ElementBounds.Fixed(645, top + 366, 30, 30), "unlimited")
            .AddStaticText(Lang.Get("thebasics:scene-unlimited"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(684, top + 368, 110, 28))
            .AddStaticText(Lang.Get("thebasics:scene-text-distance"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 408, 260, 22))
            .AddNumberInput(ElementBounds.Fixed(530, top + 432, 100, 30), null, CairoFont.TextInput(), "textdistance")
            .AddSwitch(value => { _appearance.UnlimitedTextDistance = value; RefreshPreview(); }, ElementBounds.Fixed(645, top + 432, 30, 30), "textunlimited")
            .AddStaticText(Lang.Get("thebasics:scene-unlimited"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(684, top + 434, 110, 28))
            .AddStaticText(Lang.Get("thebasics:scene-height"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 485, 150, 22))
            .AddNumberInput(ElementBounds.Fixed(690, top + 480, 100, 30), _ => RefreshPreview(), CairoFont.TextInput(), "height")
            .AddStaticText(Lang.Get("thebasics:scene-size"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 525, 150, 22))
            .AddNumberInput(ElementBounds.Fixed(690, top + 520, 100, 30), _ => RefreshPreview(), CairoFont.TextInput(), "size")
            .AddStaticText(Lang.Get("thebasics:scene-effect"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, top + 450, 150, 22))
            .AddDropDown(new[] { "plain", "hologram" }, new[] { Lang.Get("thebasics:scene-effect-plain"), Lang.Get("thebasics:scene-effect-hologram") }, (int)data.Effect,
                (value, _) => { _appearance.Effect = value == "hologram" ? SceneMarkerEffect.Hologram : SceneMarkerEffect.Plain; RefreshPreview(); }, ElementBounds.Fixed(160, top + 444, 260, 30), "effect")
            .AddSwitch(value => _appearance.IdleBobbing = value, ElementBounds.Fixed(160, top + 490, 30, 30), "bobbing")
            .AddStaticText(Lang.Get("thebasics:scene-bobbing"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, top + 494, 150, 22))
            .AddSwitch(value => { _appearance.ShowBodyInBubble = value; RefreshPreview(); }, ElementBounds.Fixed(290, top + 532, 30, 30), "showbody")
            .AddStaticText(Lang.Get("thebasics:scene-show-body"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, top + 536, 280, 22))
            .AddSmallButton(Lang.Get("thebasics:scene-description-cancel"), OnCancelButton, ElementBounds.Fixed(0, buttonY, 120, ButtonHeight))
            .AddSmallButton(Lang.Get("thebasics:scene-description-save"), OnSave, ElementBounds.Fixed(DialogWidth - 140, buttonY, 120, ButtonHeight), key: "save")
            .AddSmallButton(Lang.Get(data.IsLocked ? "thebasics:scene-unlock" : "thebasics:scene-save-lock"), OnLockButton, ElementBounds.Fixed(530, buttonY, 260, ButtonHeight), key: "lock")
            .EndChildElements()
            .Compose(focusFirstElement: false);

        SingleComposer.ToggleButtonsSetValue("symbol", (int)data.Symbol);
        SingleComposer.GetNumberInput("distance").SetValue(data.IconDistance.ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetSwitch("unlimited").SetValue(data.UnlimitedIconDistance);
        SingleComposer.GetSwitch("textunlimited").SetValue(data.UnlimitedTextDistance);
        SingleComposer.GetNumberInput("textdistance").SetValue(data.TextDistance.ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetNumberInput("height").SetValue(data.HeightOffset.ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetNumberInput("height").Enabled = !data.IsLocked;
        SingleComposer.GetNumberInput("size").SetValue((data.IndicatorScale * 100).ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetNumberInput("size").Enabled = !data.IsLocked;
        SingleComposer.GetDropDown("color").Enabled = !data.IsLocked;
        SingleComposer.GetDropDown("effect").Enabled = !data.IsLocked;
        SingleComposer.GetSwitch("bobbing").SetValue(data.IdleBobbing);
        SingleComposer.GetSwitch("bobbing").Enabled = !data.IsLocked;
        SingleComposer.GetSwitch("showbody").SetValue(data.ShowBodyInBubble);
        SingleComposer.GetSwitch("showbody").Enabled = !data.IsLocked;
        RefreshPreview();
        SingleComposer.GetButton("lock").Enabled = _canManageLock;
        SingleComposer.GetButton("save").Enabled = !data.IsLocked;
        SingleComposer.GetTextInput("title").Enabled = !data.IsLocked;
        SingleComposer.GetTextArea("body").Enabled = !data.IsLocked;
        SingleComposer.GetDropDown("kind").Enabled = !data.IsLocked;
        SingleComposer.GetSwitch("unlimited").Enabled = !data.IsLocked;
        for (var i = 0; i < _symbolShapes.Length; i++) SingleComposer.GetToggleButton("symbol-" + i).Enabled = !data.IsLocked;
        SingleComposer.GetTextInput("title").SetMaxLength(SceneDescriptionData.MaxTitleLength);
        SingleComposer.GetTextInput("title").SetValue(data.Title);
        SingleComposer.GetTextArea("body").SetMaxLength(SceneDescriptionData.MaxBodyLength);
        SingleComposer.GetTextArea("body").SetValue(data.Body, setCaretPosToEnd: false);
        RefreshPreview();
        SingleComposer.FocusElement(SingleComposer.GetTextArea("body").TabIndex);
    }

    private void RefreshPreview()
    {
        if (SingleComposer?.Composed != true) return;
        SingleComposer.GetNumberInput("distance").Enabled = !_appearance.IsLocked && !_appearance.UnlimitedIconDistance;
        var nearby = SingleComposer.GetDropDown("kind").SelectedValue == "nearby";
        SingleComposer.GetNumberInput("textdistance").Enabled = !_appearance.IsLocked && nearby && !_appearance.UnlimitedTextDistance;
        SingleComposer.GetSwitch("textunlimited").Enabled = !_appearance.IsLocked && nearby;
        SingleComposer.GetCustomDraw("preview").Redraw();
    }

    private void DrawPreview(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var width = surface.Width;
        var height = surface.Height;
        ctx.SetSourceRGBA(0.07, 0.09, 0.12, 0.9);
        ctx.Paint();
        var preview = _appearance.Clone();
        if (SingleComposer?.Composed == true)
        {
            preview.Title = SingleComposer.GetTextInput("title").GetText();
            preview.Body = SingleComposer.GetTextArea("body").GetText();
            preview.Display = SingleComposer.GetDropDown("kind").SelectedValue == "interaction"
                ? SceneDescriptionDisplay.OnInteraction : SceneDescriptionDisplay.WhenTargeted;
            if (float.TryParse(SingleComposer.GetNumberInput("height").GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var offset))
                preview.HeightOffset = offset;
            if (float.TryParse(SingleComposer.GetNumberInput("size").GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                preview.IndicatorScale = percent / 100;
        }
        preview.Normalize();
        using var text = preview.ShouldShowDescription(true) ? SceneMarkerVisuals.DescriptionSurface(capi, preview) : null;
        var textWidth = 3.0;
        var textHeight = text == null ? 0 : textWidth * text.Height / text.Width;
        if (textHeight > 6) { textWidth *= 6 / textHeight; textHeight = 6; }
        var scale = Math.Min((width - 24) / 3.0, (height - 24) / (textHeight + 3.2 + Math.Max(0, preview.HeightOffset)));
        var symbolSize = 0.8 * preview.IndicatorScale;
        var centerY = height - 12 - (1.8 + preview.HeightOffset) * scale;
        var symbolY = centerY - symbolSize / 2 * scale;
        SceneMarkerVisuals.Draw(ctx, _symbolShapes[(int)preview.Symbol], width / 2.0 - symbolSize / 2 * scale, symbolY, symbolSize * scale,
            preview.Color, preview.Symbol, preview.Effect, SceneMarkerVisuals.EffectOpacity(preview.Effect, 0));
        if (text != null)
        {
            ctx.Save();
            ctx.Translate((width - textWidth * scale) / 2, symbolY - (textHeight + 0.1) * scale);
            ctx.Scale(textWidth * scale / text.Width, textHeight * scale / text.Height);
            ctx.SetSourceSurface(text, 0, 0);
            ctx.Paint();
            ctx.Restore();
        }
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
        if (!float.TryParse(SingleComposer.GetNumberInput("size").GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ||
            !float.IsFinite(percent) || percent < 25 || percent > 300)
        {
            capi.TriggerIngameError(this, "scene-size", Lang.Get("thebasics:scene-size-invalid"));
            return false;
        }
        _appearance.IndicatorScale = percent / 100;
        if (!float.TryParse(SingleComposer.GetNumberInput("height").GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var height) ||
            !float.IsFinite(height) || height < -0.5f || height > 4)
        {
            capi.TriggerIngameError(this, "scene-height", Lang.Get("thebasics:scene-height-invalid"));
            return false;
        }
        _appearance.HeightOffset = height;
        if (SingleComposer.GetDropDown("kind").SelectedValue == "nearby" && !_appearance.UnlimitedTextDistance)
        {
            if (!float.TryParse(SingleComposer.GetNumberInput("textdistance").GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var textDistance) ||
                !float.IsFinite(textDistance) || textDistance < 1 || textDistance > 1024)
            {
                capi.TriggerIngameError(this, "scene-distance", Lang.Get("thebasics:scene-distance-invalid"));
                return false;
            }
            _appearance.TextDistance = textDistance;
        }
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
            TextDistance = _appearance.TextDistance,
            UnlimitedTextDistance = _appearance.UnlimitedTextDistance,
            HeightOffset = _appearance.HeightOffset,
            IndicatorScale = _appearance.IndicatorScale,
            Color = _appearance.Color,
            Effect = _appearance.Effect,
            IdleBobbing = _appearance.IdleBobbing,
            ShowBodyInBubble = _appearance.ShowBodyInBubble,
            Title = SingleComposer.GetTextInput("title").GetText(),
            Body = SingleComposer.GetTextArea("body").GetText(),
            Display = SingleComposer.GetDropDown("kind").SelectedValue switch
            {
                "nearby" => SceneDescriptionDisplay.AlwaysNearby,
                "interaction" => SceneDescriptionDisplay.OnInteraction,
                _ => SceneDescriptionDisplay.WhenTargeted,
            },
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
