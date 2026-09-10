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
    private SceneTitleIconDialog _iconPicker;
    private SceneTitleIconDialog _symbolIconPicker;
    private GuiDialogConfirm _clearReadConfirm;
    private bool _closing;
    private bool _lockAfterSave;
    // Whether the composed layout carries the text distance row. It only applies to the nearby mode.
    private bool _nearbyLayout;
    private readonly bool _canManageLock;
    private readonly Action _onUnlock;
    private readonly Action _onClearRead;
    private readonly SceneDescriptionData _appearance;
    private readonly Shape[] _symbolShapes;

    public SceneDescriptionDialog(ICoreClientAPI capi, SceneDescriptionData data, bool canManageLock, Action<SceneDescriptionData, bool> onSave, Action onUnlock, Action onClearRead, Action onClose) : base(capi)
    {
        _onSave = onSave;
        _onUnlock = onUnlock;
        _onClearRead = onClearRead;
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
        _iconPicker?.TryClose();
        _symbolIconPicker?.TryClose();
        _clearReadConfirm?.TryClose();
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
        var titleLabelBounds = ElementBounds.Fixed(90, top, DialogWidth - 110, 22);
        var titleInputBounds = ElementBounds.Fixed(90, top + 24, DialogWidth - 110, 30);
        var kindLabelBounds = ElementBounds.Fixed(0, top + 66, 180, 22);
        var kindBounds = ElementBounds.Fixed(0, top + 90, 220, 30);
        var bodyLabelBounds = ElementBounds.Fixed(0, top + 132, DialogWidth - 20, 22);
        var textAreaBounds = ElementBounds.Fixed(0, top + 156, DialogWidth - 20, BodyHeight);
        // The text distance only applies to the nearby mode. Any other mode drops that row and pulls
        // the right column's remaining rows up into the space it used.
        _nearbyLayout = data.Display == SceneDescriptionDisplay.AlwaysNearby;
        var textRow = _nearbyLayout ? 0 : 66;
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
            .AddDropDown(kindValues, kindNames, (int)data.Display, (_, _) => OnDisplayModeChanged(), kindBounds, "kind")
            .AddStaticText(Lang.Get("thebasics:scene-description-body-label"), CairoFont.WhiteSmallText(), bodyLabelBounds)
            .AddTextArea(textAreaBounds, _ => RefreshPreview(), CairoFont.TextInput(), "body")
            .AddSceneDrawing(ElementBounds.Fixed(530, top + 46, 260, 166), DrawPreview, "preview")
            .AddStaticText(Lang.Get("thebasics:scene-symbol"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 212, 260, 22))
            .AddTextToggleButtons(Enumerable.Repeat(string.Empty, _symbolShapes.Length).ToArray(), CairoFont.WhiteSmallText().WithFontSize(26),
                index => { _appearance.Symbol = (SceneMarkerSymbol)index; _appearance.SymbolIconName = string.Empty; RefreshPreview(); },
                Enumerable.Range(0, 6).Select(index => ElementBounds.Fixed(530 + index * 44, top + 236, 40, 44)).ToArray(), "symbol")
            .AddSmallButton(Lang.Get("thebasics:scene-symbol-other"), OpenSymbolIconPicker, ElementBounds.Fixed(660, top + 210, 130, 26), key: "symbolicon")
            .AddSceneDrawing(ElementBounds.Fixed(630, top + 212, 22, 22),
                (ctx, surface, _) =>
                {
                    // Mirror the billboard: a catalog icon that will not draw shows the fallback symbol instead of nothing.
                    if (_appearance.SymbolIconName.Length > 0 && !SceneTitleIcons.Draw(capi, ctx, surface, _appearance.SymbolIconName, _appearance.Color))
                        SceneMarkerVisuals.Draw(ctx, _symbolShapes[(int)_appearance.Symbol], 0, 0, surface.Width, _appearance.Color, _appearance.Symbol);
                }, "symboliconart")
            .AddStaticText(Lang.Get("thebasics:scene-color"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 290, 90, 22))
            .AddDropDown(new[] { "gold", "parchment", "blue", "green", "red" }, new[] { Lang.Get("thebasics:scene-color-gold"), Lang.Get("thebasics:scene-color-parchment"), Lang.Get("thebasics:scene-color-blue"), Lang.Get("thebasics:scene-color-green"), Lang.Get("thebasics:scene-color-red") }, (int)data.Color,
                (value, _) => { _appearance.Color = value switch { "parchment" => SceneMarkerColor.Parchment, "blue" => SceneMarkerColor.Blue, "green" => SceneMarkerColor.Green, "red" => SceneMarkerColor.Red, _ => SceneMarkerColor.Gold }; RefreshPreview(); }, ElementBounds.Fixed(620, top + 288, 170, 30), "color")
            // The icon distance fades the indicator in every mode, so its row is never dropped.
            .AddStaticText(Lang.Get("thebasics:scene-icon-distance"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 342, 260, 22))
            .AddNumberInput(ElementBounds.Fixed(530, top + 366, 100, 30), null, CairoFont.TextInput(), "distance")
            .AddSwitch(value => { _appearance.UnlimitedIconDistance = value; RefreshPreview(); }, ElementBounds.Fixed(645, top + 366, 30, 30), "unlimited")
            .AddStaticText(Lang.Get("thebasics:scene-unlimited"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(684, top + 368, 110, 28))
            ;
        if (_nearbyLayout)
        {
            SingleComposer
                .AddStaticText(Lang.Get("thebasics:scene-text-distance"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 408, 260, 22))
                .AddNumberInput(ElementBounds.Fixed(530, top + 432, 100, 30), null, CairoFont.TextInput(), "textdistance")
                .AddSwitch(value => { _appearance.UnlimitedTextDistance = value; RefreshPreview(); }, ElementBounds.Fixed(645, top + 432, 30, 30), "textunlimited")
                .AddStaticText(Lang.Get("thebasics:scene-unlimited"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(684, top + 434, 110, 28))
                ;
        }
        SingleComposer
            .AddStaticText(Lang.Get("thebasics:scene-height"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 485 - textRow, 150, 22))
            .AddNumberInput(ElementBounds.Fixed(690, top + 480 - textRow, 100, 30), _ => RefreshPreview(), CairoFont.TextInput(), "height")
            .AddStaticText(Lang.Get("thebasics:scene-size"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(530, top + 525 - textRow, 150, 22))
            .AddNumberInput(ElementBounds.Fixed(690, top + 520 - textRow, 100, 30), _ => RefreshPreview(), CairoFont.TextInput(), "size")
            .AddStaticText(Lang.Get("thebasics:scene-bubble-size"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, top + 450, 180, 22))
            .AddNumberInput(ElementBounds.Fixed(185, top + 444, 80, 30), _ => RefreshPreview(), CairoFont.TextInput(), "bubblesize")
            .AddSmallButton(Lang.Get("thebasics:scene-icon-button"), OpenIconPicker, ElementBounds.Fixed(0, top + 24, 78, 30), key: "titleicon")
            .AddSceneDrawing(ElementBounds.Fixed(28, top - 1, 22, 22), (ctx, surface, _) => { if (_appearance.TitleIconName.Length > 0) SceneTitleIcons.Draw(capi, ctx, surface, _appearance.TitleIconName); }, "titleiconart")
            .AddSwitch(value => _appearance.IdleBobbing = value, ElementBounds.Fixed(160, top + 490, 30, 30), "bobbing")
            .AddStaticText(Lang.Get("thebasics:scene-bobbing"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, top + 494, 150, 22))
            .AddSwitch(value => { _appearance.ShowBodyInBubble = value; RefreshPreview(); }, ElementBounds.Fixed(250, top + 90, 30, 30), "showbody")
            .AddStaticText(Lang.Get("thebasics:scene-show-body"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(250, top + 66, 250, 22))
            .AddSmallButton(Lang.Get("thebasics:scene-description-cancel"), OnCancelButton, ElementBounds.Fixed(0, buttonY, 120, ButtonHeight))
            .AddSmallButton(Lang.Get("thebasics:scene-clear-read"), OnClearRead, ElementBounds.Fixed(140, buttonY, 140, ButtonHeight), key: "clearread")
            .AddSmallButton(Lang.Get("thebasics:scene-description-save"), OnSave, ElementBounds.Fixed(DialogWidth - 140, buttonY, 120, ButtonHeight), key: "save")
            .AddSmallButton("", OnLockButton, ElementBounds.Fixed(750, top, 36, 32), key: "lock")
            .AddSceneDrawing(ElementBounds.Fixed(756, top + 4, 24, 24), DrawLock, "lockart")
            .AddHoverText(Lang.Get(data.IsLocked ? "thebasics:scene-unlock" : "thebasics:scene-lock-on-save"), CairoFont.WhiteSmallText(), 250, ElementBounds.Fixed(750, top, 36, 32))
            ;
        for (var index = 0; index < _symbolShapes.Length; index++)
        {
            var symbolIndex = index;
            SingleComposer.AddSceneDrawing(ElementBounds.Fixed(534 + index * 44, top + 242, 32, 32),
                (ctx, surface, _) => SceneMarkerVisuals.Draw(ctx, _symbolShapes[symbolIndex], 0, 0, surface.Width,
                    _appearance.Color, (SceneMarkerSymbol)symbolIndex), "symbolart-" + index);
        }
        SingleComposer.EndChildElements().Compose(focusFirstElement: false);

        SingleComposer.ToggleButtonsSetValue("symbol", data.SymbolIconName.Length > 0 ? -1 : (int)data.Symbol);
        SingleComposer.GetNumberInput("distance").SetValue(data.IconDistance.ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetSwitch("unlimited").SetValue(data.UnlimitedIconDistance);
        if (_nearbyLayout)
        {
            SingleComposer.GetSwitch("textunlimited").SetValue(data.UnlimitedTextDistance);
            SingleComposer.GetNumberInput("textdistance").SetValue(data.TextDistance.ToString(CultureInfo.InvariantCulture));
        }
        SingleComposer.GetNumberInput("height").SetValue(data.HeightOffset.ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetNumberInput("height").Enabled = !data.IsLocked;
        SingleComposer.GetNumberInput("size").SetValue((data.IndicatorScale * 100).ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetNumberInput("size").Enabled = !data.IsLocked;
        SingleComposer.GetNumberInput("bubblesize").SetValue((data.BubbleScale * 100).ToString(CultureInfo.InvariantCulture));
        SingleComposer.GetNumberInput("bubblesize").Enabled = !data.IsLocked;
        SingleComposer.GetButton("titleicon").Enabled = !data.IsLocked;
        SingleComposer.GetButton("symbolicon").Enabled = !data.IsLocked;
        SingleComposer.GetDropDown("color").Enabled = !data.IsLocked;
        SingleComposer.GetSwitch("bobbing").SetValue(data.IdleBobbing);
        SingleComposer.GetSwitch("bobbing").Enabled = !data.IsLocked;
        SingleComposer.GetSwitch("showbody").SetValue(data.ShowBodyInBubble);
        SingleComposer.GetSwitch("showbody").Enabled = !data.IsLocked;
        RefreshPreview();
        SingleComposer.GetButton("lock").Enabled = _canManageLock;
        SingleComposer.GetButton("save").Enabled = !data.IsLocked;
        SingleComposer.GetButton("clearread").Enabled = !data.IsLocked;
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
        if (_nearbyLayout)
        {
            SingleComposer.GetNumberInput("textdistance").Enabled = !_appearance.IsLocked && !_appearance.UnlimitedTextDistance;
            SingleComposer.GetSwitch("textunlimited").Enabled = !_appearance.IsLocked;
        }
        SingleComposer.GetSceneDrawing("preview").Redraw();
        SingleComposer.GetSceneDrawing("titleiconart").Redraw();
        SingleComposer.GetSceneDrawing("symboliconart").Redraw();
        for (var index = 0; index < _symbolShapes.Length; index++) SingleComposer.GetSceneDrawing("symbolart-" + index).Redraw();
    }

    // The distance rows appear and disappear with the mode, so that switch has to rebuild the
    // layout. Everything the user has typed but not saved is carried into the new composer.
    private void OnDisplayModeChanged()
    {
        var nearby = SelectedDisplay() == SceneDescriptionDisplay.AlwaysNearby;
        if (nearby == _nearbyLayout)
        {
            RefreshPreview();
            return;
        }

        var pending = _appearance.Clone();
        pending.Display = SelectedDisplay();
        pending.Title = SingleComposer.GetTextInput("title").GetText();
        pending.Body = SingleComposer.GetTextArea("body").GetText();
        if (TryReadNumber("height", out var height)) pending.HeightOffset = height;
        if (TryReadNumber("size", out var size)) pending.IndicatorScale = size / 100;
        if (TryReadNumber("bubblesize", out var bubble)) pending.BubbleScale = bubble / 100;
        if (TryReadNumber("distance", out var iconDistance)) pending.IconDistance = iconDistance;
        // Also written back to _appearance: once the row is gone, saving reads the text distance from
        // there instead of from the input.
        if (_nearbyLayout && TryReadNumber("textdistance", out var textDistance)) _appearance.TextDistance = pending.TextDistance = textDistance;
        // Deferred: the dropdown keeps working on itself after this callback returns, and composing
        // now would dispose the element out from under it.
        capi.Event.EnqueueMainThreadTask(() => { if (IsOpened()) Compose(pending); }, "thebasics-scene-recompose");
    }

    private SceneDescriptionDisplay SelectedDisplay() => SingleComposer.GetDropDown("kind").SelectedValue switch
    {
        "nearby" => SceneDescriptionDisplay.AlwaysNearby,
        "interaction" => SceneDescriptionDisplay.OnInteraction,
        _ => SceneDescriptionDisplay.WhenTargeted,
    };

    private bool TryReadNumber(string key, out float value) =>
        float.TryParse(SingleComposer.GetNumberInput(key).GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);

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
            if (float.TryParse(SingleComposer.GetNumberInput("bubblesize").GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var bubblePercent))
                preview.BubbleScale = bubblePercent / 100;
        }
        preview.Normalize();
        using var text = preview.ShouldShowDescription(true) ? SceneMarkerVisuals.DescriptionSurface(capi, preview) : null;
        var (textWidth, textHeight) = text == null ? (0f, 0f) : SceneBubbleLayout.Size(text.Width, text.Height, RuntimeEnv.GUIScale, preview.BubbleRenderScale);
        var scale = Math.Min((width - 24) / Math.Max(3, textWidth), (height - 24) / (textHeight + 3.2 + Math.Max(0, preview.HeightOffset)));
        var symbolSize = 0.8 * preview.IndicatorScale;
        var centerY = height - 12 - (1.8 + preview.HeightOffset) * scale;
        var symbolY = centerY - symbolSize / 2 * scale;
        var symbolX = width / 2.0 - symbolSize / 2 * scale;
        if (!DrawSymbolIcon(ctx, preview, symbolX, symbolY, symbolSize * scale))
            SceneMarkerVisuals.Draw(ctx, _symbolShapes[(int)preview.Symbol], symbolX, symbolY, symbolSize * scale,
                preview.Color, preview.Symbol, SceneMarkerVisuals.IndicatorOpacity);
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

    // Catalog icons only draw at a whole-pixel size, so the preview renders one and scales that tile into place.
    private bool DrawSymbolIcon(Context ctx, SceneDescriptionData preview, double x, double y, double size)
    {
        if (preview.SymbolIconName.Length == 0 || !(size >= 1)) return false;
        var pixels = (int)Math.Ceiling(size);
        using var icon = new ImageSurface(Format.Argb32, pixels, pixels);
        using (var iconCtx = new Context(icon))
            if (!SceneTitleIcons.Draw(capi, iconCtx, icon, preview.SymbolIconName, preview.Color)) return false;
        ctx.Save();
        ctx.Translate(x, y);
        ctx.Scale(size / pixels, size / pixels);
        ctx.SetSourceSurface(icon, 0, 0);
        ctx.PaintWithAlpha(SceneMarkerVisuals.IndicatorOpacity);
        ctx.Restore();
        return true;
    }

    private bool OpenSymbolIconPicker()
    {
        if (_appearance.IsLocked || _symbolIconPicker != null) return false;
        _symbolIconPicker = new SceneTitleIconDialog(capi, icon =>
        {
            _appearance.SymbolIconName = SceneDescriptionData.NormalizeIconName(icon);
            // A catalog icon replaces the six-button choice, so none of those tiles stays lit beside it.
            SingleComposer.ToggleButtonsSetValue("symbol", _appearance.SymbolIconName.Length > 0 ? -1 : (int)_appearance.Symbol);
            RefreshPreview();
        }, () => _symbolIconPicker = null);
        return _symbolIconPicker.TryOpen();
    }

    private bool OpenIconPicker()
    {
        if (_appearance.IsLocked || _iconPicker != null) return false;
        _iconPicker = new SceneTitleIconDialog(capi, icon => { _appearance.TitleIcon = 0; _appearance.TitleIconName = icon; RefreshPreview(); }, () => _iconPicker = null);
        return _iconPicker.TryOpen();
    }

    private bool OnClearRead()
    {
        if (_appearance.IsLocked) return false;
        _clearReadConfirm = new GuiDialogConfirm(capi, Lang.Get("thebasics:scene-clear-read-confirm"),
            confirmed => { _clearReadConfirm = null; if (confirmed) _onClearRead?.Invoke(); });
        _clearReadConfirm.TryOpen();
        return true;
    }

    private void DrawLock(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        ctx.Scale(surface.Width / 24.0, surface.Height / 24.0);
        ctx.SetSourceRGBA(1, 1, 1, _canManageLock ? 1 : 0.4);
        ctx.LineWidth = 2;
        ctx.Rectangle(5, 11, 14, 11);
        ctx.Stroke();
        var closed = _appearance.IsLocked || _lockAfterSave;
        ctx.Arc(closed ? 12 : 16, 10, 5, Math.PI, Math.PI * 2);
        ctx.Stroke();
    }
    private bool OnLockButton()
    {
        if (!_canManageLock) return false;
        if (!_appearance.IsLocked)
        {
            _lockAfterSave = !_lockAfterSave;
            SingleComposer.GetSceneDrawing("lockart").Redraw();
            return true;
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
        if (!float.TryParse(SingleComposer.GetNumberInput("bubblesize").GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var bubblePercent) ||
            !float.IsFinite(bubblePercent) || bubblePercent < 40 || bubblePercent > 200)
        {
            capi.TriggerIngameError(this, "scene-bubble-size", Lang.Get("thebasics:scene-bubble-size-invalid"));
            return false;
        }
        _appearance.BubbleScale = bubblePercent / 100;
        if (!float.TryParse(SingleComposer.GetNumberInput("height").GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var height) ||
            !float.IsFinite(height) || height < -0.5f || height > 4)
        {
            capi.TriggerIngameError(this, "scene-height", Lang.Get("thebasics:scene-height-invalid"));
            return false;
        }
        _appearance.HeightOffset = height;
        if (_nearbyLayout && !_appearance.UnlimitedTextDistance)
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
            SymbolIconName = _appearance.SymbolIconName,
            IconDistance = _appearance.IconDistance,
            UnlimitedIconDistance = _appearance.UnlimitedIconDistance,
            TextDistance = _appearance.TextDistance,
            UnlimitedTextDistance = _appearance.UnlimitedTextDistance,
            HeightOffset = _appearance.HeightOffset,
            IndicatorScale = _appearance.IndicatorScale,
            Color = _appearance.Color,
            IdleBobbing = _appearance.IdleBobbing,
            ShowBodyInBubble = _appearance.ShowBodyInBubble,
            BubbleScale = _appearance.BubbleScale, TitleIcon = _appearance.TitleIcon, TitleIconName = _appearance.TitleIconName,
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
