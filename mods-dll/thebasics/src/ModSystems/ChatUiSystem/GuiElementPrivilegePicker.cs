using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A free-text privilege field with optional registered privilege suggestions.</summary>
public sealed class GuiElementPrivilegePicker : GuiElementControl
{
    private const double ArrowWidth = 24;
    private readonly GuiElementTextInput input;
    private readonly PrivilegeListMenu menu;
    private readonly ConfigAdminPrivilegeSuggestion[] suggestions;
    private readonly Action<string> onChanged;
    private LoadedTexture arrowReleased;
    private LoadedTexture arrowPressed;
    private string value;
    private bool composed;
    private bool settingValue;

    public GuiElementPrivilegePicker(ICoreClientAPI api, ElementBounds bounds, string value,
        IEnumerable<ConfigAdminPrivilegeSuggestion> suggestions, Action<string> onChanged, CairoFont font)
        : base(api, bounds)
    {
        this.value = value ?? "";
        this.onChanged = onChanged;
        this.suggestions = ConfigAdminPrivilegeSuggestions.ForClient(suggestions, new[] { this.value }).ToArray();
        input = new GuiElementTextInput(api, ElementBounds.Fixed(0, 0, 1, 1).WithParent(bounds),
            TextChanged, font.Clone());
        menu = new PrivilegeListMenu(api, ElementBounds.Fixed(0, 0, 1, 1).WithParent(bounds),
            SelectSuggestion, font.Clone().WithFontSize(17));
        arrowReleased = new LoadedTexture(api);
        arrowPressed = new LoadedTexture(api);
        MouseOverCursor = "textselect";
    }

    public string Value => value;
    public string GetText() => value;
    public bool IsSuggestionsOpen => menu.IsOpened;
    public override bool Focusable => enabled;
    public override double DrawOrder => 0.5;

    public override double Scale
    {
        get => base.Scale;
        set
        {
            base.Scale = value;
            if (input != null) input.Scale = value;
            if (menu != null) menu.Scale = value;
        }
    }

    public override bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value) return;
            enabled = value;
            MouseOverCursor = value ? "textselect" : null;
            if (!value) OnFocusLost();
            if (!composed) return;
            input.Enabled = value;
            ComposeArrows();
        }
    }

    /// <summary>Updates the field without firing the user-edit callback.</summary>
    public void SetValue(string newValue)
    {
        value = newValue ?? "";
        CloseSuggestions();
        if (!composed) return;
        settingValue = true;
        try { input.SetValue(value); }
        finally { settingValue = false; }
    }

    public void CloseSuggestions() => menu.Dismiss();

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        SyncChildBounds();
        input.ComposeElements(context, surface);
        composed = true;
        input.Enabled = enabled;
        settingValue = true;
        try { input.SetValue(value); }
        finally { settingValue = false; }
        ComposeArrows();
    }

    private void SyncChildBounds()
    {
        var width = Bounds.InnerWidth / RuntimeEnv.GUIScale;
        var height = Bounds.InnerHeight / RuntimeEnv.GUIScale;
        input.Bounds.WithFixedSize(Math.Max(1, width - ArrowWidth * Scale), height).WithFixedPosition(0, 0);
        input.Bounds.MarkDirtyRecursive();
        input.Bounds.CalcWorldBounds();
        menu.Bounds.WithFixedSize(width, height);
        menu.Bounds.MarkDirtyRecursive();
        menu.Bounds.CalcWorldBounds();
    }

    private void ComposeArrows()
    {
        ComposeArrow(false, ref arrowReleased);
        ComposeArrow(true, ref arrowPressed);
    }

    private void ComposeArrow(bool pressed, ref LoadedTexture texture)
    {
        var width = Math.Max(1, (int)Math.Ceiling(scaled(ArrowWidth) * Scale));
        var height = Math.Max(1, (int)Math.Ceiling(Bounds.InnerHeight));
        using var surface = new ImageSurface(Format.Argb32, width, height);
        using var context = genContext(surface);
        context.SetSourceRGB(GuiStyle.DialogDefaultBgColor[0], GuiStyle.DialogDefaultBgColor[1], GuiStyle.DialogDefaultBgColor[2]);
        RoundRectangle(context, 0, 0, width, height, GuiStyle.ElementBGRadius);
        context.FillPreserve();
        context.SetSourceRGBA(pressed ? 0 : 1, pressed ? 0 : 1, pressed ? 0 : 1, 0.1);
        context.Fill();
        EmbossRoundRectangleElement(context, 0, 0, width, height, pressed, 2, 1);
        context.SetSourceRGBA(GuiStyle.DialogHighlightColor);
        RoundRectangle(context, 0, 0, width, height, 1);
        context.Fill();
        var half = Math.Min(scaled(7) * Scale, width / 3.0);
        var centerY = height / 2.0;
        context.MoveTo(width / 2.0 - half, centerY - half / 2);
        context.LineTo(width / 2.0 + half, centerY - half / 2);
        context.LineTo(width / 2.0, centerY + half / 2);
        context.ClosePath();
        context.SetSourceRGBA(1, 1, 1, enabled ? (pressed ? 0.4 : 0.6) : 0.2);
        context.Fill();
        generateTexture(surface, ref texture);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        RenderControl(deltaTime);
        RenderSuggestions(deltaTime);
    }

    /// <summary>Renders the input and arrow. Scroll containers can render the menu after removing their clip.</summary>
    public void RenderControl(float deltaTime)
    {
        input.RenderInteractiveElements(deltaTime);
        if (api.Render.ScissorStack.Count > 0) api.Render.GlScissorFlag(true);
        var arrow = menu.IsOpened ? arrowPressed : arrowReleased;
        api.Render.Render2DTexturePremultipliedAlpha(arrow.TextureId,
            Bounds.renderX + Bounds.InnerWidth - arrow.Width, Bounds.renderY, arrow.Width, arrow.Height);
    }

    public void RenderSuggestions(float deltaTime) => menu.RenderInteractiveElements(deltaTime);

    private void TextChanged(string text)
    {
        if (settingValue) return;
        value = text;
        onChanged?.Invoke(text);
        if (HasFocus && enabled) ShowSuggestions(false);
    }

    private void SelectSuggestion(string code)
    {
        SetValue(code);
        onChanged?.Invoke(code);
    }

    private void ShowSuggestions(bool showAll)
    {
        SyncChildBounds();
        var query = value.Trim();
        var matches = suggestions.Where(item => showAll || query.Length == 0 ||
                item.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => showAll || query.Length == 0 ? 0 : item.Code.Equals(query, StringComparison.OrdinalIgnoreCase) ? 0 :
                item.Code.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 :
                item.Description.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 2 : 3)
            .ToArray();
        menu.Show(matches, value);
    }

    public override void OnFocusGained()
    {
        if (!enabled) return;
        base.OnFocusGained();
        input.OnFocusGained();
        menu.OnFocusGained();
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        input.OnFocusLost();
        menu.OnFocusLost();
    }

    public override bool IsPositionInside(int x, int y) => base.IsPositionInside(x, y) || menu.IsPositionInside(x, y);

    public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled || args.Handled) return;
        if (menu.IsPositionInside(args.X, args.Y))
        {
            menu.OnMouseDown(api, args);
            return;
        }
        var wasOpen = menu.IsOpened;
        CloseSuggestions();
        if (!base.IsPositionInside(args.X, args.Y)) return;
        if (args.X >= Bounds.renderX + Bounds.InnerWidth - arrowReleased.Width)
        {
            if (!wasOpen) ShowSuggestions(true);
            api.Gui.PlaySound("menubutton");
            args.Handled = true;
            return;
        }
        input.OnMouseDown(api, args);
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        input.OnMouseUp(api, args);
        menu.OnMouseUp(api, args);
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (!enabled) return;
        menu.OnMouseMove(api, args);
        if (!args.Handled) input.OnMouseMove(api, args);
    }

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (enabled) menu.OnMouseWheel(api, args);
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!enabled || !HasFocus) return;
        if (args.KeyCode == (int)GlKeys.Tab)
        {
            CloseSuggestions();
            return;
        }
        if (menu.IsOpened && args.KeyCode == (int)GlKeys.Escape)
        {
            CloseSuggestions();
            args.Handled = true;
            return;
        }
        if (args.KeyCode == (int)GlKeys.Down || args.KeyCode == (int)GlKeys.Up)
        {
            if (!menu.IsOpened) ShowSuggestions(true);
            else menu.OnKeyDown(api, args);
            args.Handled = true;
            return;
        }
        if (menu.IsOpened && (args.KeyCode == (int)GlKeys.Enter || args.KeyCode == (int)GlKeys.KeypadEnter))
        {
            menu.OnKeyDown(api, args);
            return;
        }
        if (menu.IsOpened && (args.KeyCode == (int)GlKeys.Home || args.KeyCode == (int)GlKeys.End))
        {
            menu.OnKeyDown(api, args);
            return;
        }
        input.OnKeyDown(api, args);
    }

    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
        if (enabled && HasFocus) input.OnKeyPress(api, args);
    }

    public override void Dispose()
    {
        input.Dispose();
        menu.Dispose();
        arrowReleased.Dispose();
        arrowPressed.Dispose();
        base.Dispose();
    }

    private sealed class PrivilegeListMenu : GuiElementListMenu
    {
        private ConfigAdminPrivilegeSuggestion[] items = Array.Empty<ConfigAdminPrivilegeSuggestion>();
        private double composedScrollOffset = double.NaN;

        public PrivilegeListMenu(ICoreClientAPI api, ElementBounds bounds, Action<string> selected, CairoFont font)
            : base(api, Array.Empty<string>(), Array.Empty<string>(), -1,
                (code, _) => selected(code), bounds, font, false)
        {
            unscaledLineHeight = 54;
        }

        public void Dismiss() => expanded = false;

        public void Show(ConfigAdminPrivilegeSuggestion[] matches, string current)
        {
            Dismiss();
            if (matches.Length == 0) return;
            items = matches;
            Values = matches.Select(item => item.Code).ToArray();
            Names = matches.Select(item => item.Code).ToArray();
            SelectedIndex = Array.FindIndex(Values, code => code.Equals(current, StringComparison.Ordinal));
            HoveredIndex = Math.Max(0, SelectedIndex);
            Bounds.fixedY = 0;
            Bounds.MarkDirtyRecursive();
            Bounds.CalcWorldBounds();
            var rowHeight = scaled(unscaledLineHeight) * Scale;
            var below = api.Render.FrameHeight - Bounds.renderY - Bounds.InnerHeight;
            var above = Bounds.renderY;
            var openAbove = below < matches.Length * rowHeight && above > below;
            var available = Math.Max(rowHeight, openAbove ? above : below);
            var viewportHeight = Math.Min(matches.Length, Math.Max(1, Math.Floor(available / rowHeight))) * rowHeight;
            MaxHeight = (int)Math.Floor(viewportHeight / RuntimeEnv.GUIScale);
            if (openAbove) Bounds.fixedY -= (viewportHeight + Bounds.InnerHeight) / RuntimeEnv.GUIScale;
            Bounds.MarkDirtyRecursive();
            Bounds.CalcWorldBounds();
            expandedBoxWidth = Bounds.InnerWidth;
            expandedBoxHeight = Values.Length * rowHeight;
            visibleBounds = Bounds.FlatCopy();
            visibleBounds.fixedWidth = expandedBoxWidth / RuntimeEnv.GUIScale;
            visibleBounds.fixedHeight = viewportHeight / RuntimeEnv.GUIScale;
            visibleBounds.fixedY += Bounds.InnerHeight / RuntimeEnv.GUIScale;
            visibleBounds.CalcWorldBounds();
            scrollbar.Bounds.WithEmptyParent().WithFixedSize(10, visibleBounds.fixedHeight - 3)
                .WithFixedPosition(expandedBoxWidth / RuntimeEnv.GUIScale - 10, 0).WithFixedPadding(0, 2);
            scrollbar.Bounds.CalcWorldBounds();
            scrollbar.SetHeights((int)visibleBounds.InnerHeight, (int)expandedBoxHeight);
            scrollbar.CurrentYPosition = 0;
            scrollbar.TriggerChanged();
            ComposeScrollbar();
            ComposeHover(rowHeight);
            composedScrollOffset = double.NaN;
            expanded = true;
            ComposeRows(rowHeight);
            EnsureItemVisible(HoveredIndex);
        }

        private void ComposeRows(double rowHeight)
        {
            foreach (var row in richtTextElem) row.Dispose();
            var first = Math.Max(0, (int)Math.Floor(scrollOffY / rowHeight));
            var count = Math.Min(items.Length - first, (int)Math.Ceiling(visibleBounds.InnerHeight / rowHeight) + 1);
            richtTextElem = new GuiElementRichtext[count];
            using var surface = new ImageSurface(Format.Argb32, Math.Max(1, (int)Math.Ceiling(expandedBoxWidth)),
                Math.Max(1, (int)Math.Ceiling(visibleBounds.InnerHeight)));
            using var context = genContext(surface);
            context.SetSourceRGBA(GuiStyle.DialogStrongBgColor);
            RoundRectangle(context, 0, 0, expandedBoxWidth, visibleBounds.InnerHeight, 1);
            context.FillPreserve();
            context.SetSourceRGBA(0, 0, 0, 0.5);
            context.LineWidth = 2;
            context.Stroke();
            var textWidth = Math.Max(1, expandedBoxWidth - scaled(24) * Scale);
            var descriptionFont = Font.Clone().WithFontSize(13);
            for (var i = 0; i < count; i++)
            {
                var item = items[first + i];
                var code = FitLine(item.Code, Font, textWidth);
                var description = item.Description == item.Code ? "" : FitLine(item.Description, descriptionFont, textWidth);
                var rich = VtmlUtils.EscapeVtml(code);
                if (!string.IsNullOrWhiteSpace(description))
                    rich += "<br><font size=\"13\" color=\"#dddddd\">" + VtmlUtils.EscapeVtml(description) + "</font>";
                var row = richtTextElem[i] = new GuiElementRichtext(api, Array.Empty<RichTextComponentBase>(),
                    ElementBounds.Fixed(0, 0, textWidth / RuntimeEnv.GUIScale, 100).WithEmptyParent());
                row.Bounds.fixedWidth = textWidth / RuntimeEnv.GUIScale;
                row.SetNewTextWithoutRecompose(rich, Font);
                row.BeforeCalcBounds();
                row.Bounds.fixedX = 6 * Scale;
                var rowY = (first + i) * rowHeight - scrollOffY;
                row.Bounds.fixedY = (rowY + Math.Max(0, (rowHeight - row.TotalHeight) / 2)) / RuntimeEnv.GUIScale;
                row.Bounds.CalcWorldBounds();
                context.Save();
                context.Rectangle(0, rowY, expandedBoxWidth - scaled(12), rowHeight);
                context.Clip();
                row.ComposeFor(row.Bounds, context, surface);
                context.Restore();
            }
            generateTexture(surface, ref dropDownTexture);
            composedScrollOffset = scrollOffY;
        }

        private void ComposeScrollbar()
        {
            using var scrollbarSurface = new ImageSurface(Format.Argb32, Math.Max(1, (int)Math.Ceiling(expandedBoxWidth)),
                Math.Max(1, (int)Math.Ceiling(scrollbar.Bounds.OuterHeight)));
            using var scrollbarContext = genContext(scrollbarSurface);
            scrollbar.ComposeElements(scrollbarContext, scrollbarSurface);
            generateTexture(scrollbarSurface, ref scrollbarTexture);
        }

        private void ComposeHover(double rowHeight)
        {
            using var surface = new ImageSurface(Format.Argb32, Math.Max(1, (int)Math.Ceiling(expandedBoxWidth)),
                Math.Max(1, (int)Math.Ceiling(rowHeight)));
            using var context = genContext(surface);
            var color = GuiStyle.DialogHighlightColor;
            context.SetSourceRGBA(color[0], color[1], color[2], 0.5);
            context.Paint();
            generateTexture(surface, ref hoverTexture);
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (!expanded) return;
            var rowHeight = scaled(unscaledLineHeight) * Scale;
            if (composedScrollOffset != scrollOffY) ComposeRows(rowHeight);
            api.Render.PushScissor(visibleBounds);
            api.Render.Render2DTexture(dropDownTexture.TextureId, (int)visibleBounds.renderX, (int)visibleBounds.renderY,
                dropDownTexture.Width, dropDownTexture.Height, 310);
            if (HoveredIndex >= 0)
                api.Render.Render2DTexturePremultipliedAlpha(hoverTexture.TextureId, visibleBounds.renderX + 1,
                    visibleBounds.renderY + HoveredIndex * rowHeight - scrollOffY + 1,
                    expandedBoxWidth - scaled(10), rowHeight - 2, 311);
            api.Render.PopScissor();
            if (api.Render.ScissorStack.Count > 0) api.Render.GlScissorFlag(false);
            api.Render.Render2DTexturePremultipliedAlpha(scrollbarTexture.TextureId,
                visibleBounds.renderX, visibleBounds.renderY, scrollbarTexture.Width, scrollbarTexture.Height, 316);
            scrollbar.Bounds.WithParent(Bounds);
            scrollbar.Bounds.absFixedY = Bounds.InnerHeight;
            scrollbar.zOffset = 320;
            scrollbar.RenderInteractiveElements(deltaTime);
            if (api.Render.ScissorStack.Count > 0) api.Render.GlScissorFlag(true);
        }

        private static string FitLine(string text, CairoFont font, double width)
        {
            text = (text ?? "").Replace('\r', ' ').Replace('\n', ' ');
            if (font.GetTextExtents(text).Width <= width) return text;
            const string suffix = "...";
            var count = text.Length;
            while (count > 0 && font.GetTextExtents(text[..count] + suffix).Width > width) count--;
            return text[..count] + suffix;
        }

        public override bool IsPositionInside(int x, int y) => expanded && visibleBounds.PointInside(x, y);

        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            if (!IsPositionInside(args.X, args.Y)) return;
            if (Bounds.renderX + expandedBoxWidth - args.X < scaled(10))
            {
                scrollbar.OnMouseDown(api, args);
                args.Handled = true;
                return;
            }
            var rowHeight = scaled(unscaledLineHeight) * Scale;
            var index = (int)Math.Floor((args.Y - Bounds.renderY - Bounds.InnerHeight + scrollOffY) / rowHeight);
            args.Handled = true;
            if (index < 0 || index >= Values.Length) return;
            SelectedIndex = index;
            Dismiss();
            api.Gui.PlaySound("toggleswitch");
            onSelectionChanged?.Invoke(Values[index], true);
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            if (scrollbar.mouseDownOnScrollbarHandle || IsPositionInside(args.X, args.Y)) base.OnMouseMove(api, args);
        }

        public override void Dispose()
        {
            foreach (var row in richtTextElem) row.Dispose();
            base.Dispose();
        }
    }
}
