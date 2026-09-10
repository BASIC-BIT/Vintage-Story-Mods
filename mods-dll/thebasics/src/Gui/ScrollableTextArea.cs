using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.Gui;

/// <summary>Scroll arithmetic for <c>ScrollableTextArea</c>, in unscaled GUI units.</summary>
internal static class TextAreaScroll
{
    internal const double Padding = 4;

    internal static double ContentHeight(int lineCount, double lineHeightPixels, double guiScale, double visible) =>
        Math.Max(visible, lineCount * lineHeightPixels / (guiScale > 0 ? guiScale : 1) + Padding);

    internal static double TargetFor(double caretTop, double caretBottom, double current, double visible, double total)
    {
        var target = caretTop < current ? caretTop
            : caretBottom > current + visible ? caretBottom - visible
            : current;
        return Math.Clamp(target, 0, Math.Max(0, total - visible));
    }
}

/// <summary>
/// A multiline text area that keeps its caret on the right character when a keypress re-wraps the text,
/// and, with a <see cref="Scrollbar"/>, scrolls inside a fixed-height clip and follows the caret.
/// </summary>
internal class ScrollableTextArea : GuiElementTextArea
{
    /// <summary>Null for a plain text area that only carries the caret fix.</summary>
    internal GuiElementScrollbar Scrollbar;

    private readonly double _visible;

    internal ScrollableTextArea(ICoreClientAPI capi, ElementBounds bounds, Action<string> onChanged, CairoFont font)
        : base(capi, bounds, null, font)
    {
        _visible = bounds.fixedHeight;
        // Vanilla autoheight counts hard line breaks twice and mixes scaled with unscaled units.
        Autoheight = false;
        OnTextChanged = text =>
        {
            UpdateScroll();
            onChanged?.Invoke(text);
        };
        OnCursorMoved = (_, caretY) => KeepCaretVisible(caretY);
    }

    // 1.22.6 OnKeyPress re-wraps, then advances the caret from its pre-wrap line and column, which lands
    // k-1 characters short when k characters wrap. Carrying the flat index across the swap fixes it.
    public override void LoadValue(List<string> newLines)
    {
        var caret = CaretPosWithoutLineBreaks;
        base.LoadValue(newLines);
        CaretPosWithoutLineBreaks = caret;
    }

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (Scrollbar != null) Scrollbar.OnMouseWheel(api, args);
        else base.OnMouseWheel(api, args);
    }

    internal void UpdateScroll()
    {
        if (Scrollbar == null) return;
        Bounds.fixedHeight = TextAreaScroll.ContentHeight(lines.Count, Font.GetFontExtents().Height, RuntimeEnv.GUIScale, _visible);
        Bounds.CalcWorldBounds();
        Scrollbar.SetHeights((float)_visible, (float)Bounds.fixedHeight);
    }

    internal void OnScroll(float value)
    {
        Bounds.fixedY = -value;
        Bounds.CalcWorldBounds();
    }

    // caretY arrives in scaled pixels (GuiElementEditableTextBase.SetCaretPos). The vanilla EnsureVisible
    // compares it against a scaled height, so it is only right at GUI scale 1; this does its own math.
    private void KeepCaretVisible(double caretY)
    {
        if (Scrollbar == null) return;
        var scale = RuntimeEnv.GUIScale > 0 ? RuntimeEnv.GUIScale : 1;
        var top = caretY / scale;
        var bottom = top + Font.GetFontExtents().Height / scale + TextAreaScroll.Padding;
        var current = Scrollbar.CurrentYPosition;
        var target = TextAreaScroll.TargetFor(top, bottom, current, _visible, Bounds.fixedHeight);
        if (Math.Abs(target - current) <= 0.5) return;
        Scrollbar.CurrentYPosition = (float)target;
        Scrollbar.TriggerChanged();
    }
}

internal static class ScrollableTextAreaComposerExtensions
{
    /// <summary>
    /// Adds a clipped text area with an always-present scrollbar. <paramref name="bounds"/> is the whole box;
    /// the text gets the box minus the bar, so its wrap width never changes when the bar becomes useful.
    /// </summary>
    internal static GuiComposer AddScrollableTextArea(this GuiComposer composer, ElementBounds bounds, Action<string> onChanged, CairoFont font, string key)
    {
        if (composer.Composed) return composer;
        var clip = ElementBounds.Fixed(bounds.fixedX, bounds.fixedY, bounds.fixedWidth - GuiElementScrollbar.DefaultScrollbarWidth - 3, bounds.fixedHeight);
        var area = new ScrollableTextArea(composer.Api, ElementBounds.Fixed(0, 0, clip.fixedWidth, clip.fixedHeight), onChanged, font);
        area.Scrollbar = new GuiElementScrollbar(composer.Api, area.OnScroll, ElementStdBounds.VerticalScrollbar(clip));
        composer.BeginClip(clip).AddInteractiveElement(area, key).EndClip().AddInteractiveElement(area.Scrollbar, key + "-scrollbar");
        // The scrollbar only has world bounds once the composer has composed.
        composer.OnComposed += area.UpdateScroll;
        return composer;
    }

    internal static ScrollableTextArea GetScrollableTextArea(this GuiComposer composer, string key) =>
        (ScrollableTextArea)composer.GetElement(key);
}
