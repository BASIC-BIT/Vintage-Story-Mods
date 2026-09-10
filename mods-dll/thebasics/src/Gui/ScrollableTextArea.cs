using System;

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
