using System;
using System.Collections.Generic;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

// Keeps form scroll per page while previews and footer stay outside the viewport.
internal sealed class SetupWizardSettingsForm
{
    private readonly Dictionary<string, float> _positions = new(StringComparer.Ordinal);
    private string _page;
    private ElementBounds _clip;
    private ElementBounds _content;
    public double Width => _content.fixedWidth;

    internal void Reset(string page) => _positions.Remove(page);

    public void Begin(GuiComposer composer, string page, ElementBounds clip)
    {
        _page = page;
        _clip = clip;
        _content = ElementBounds.Fixed(0, -_positions.GetValueOrDefault(page), clip.fixedWidth - 20, 1);
        composer.BeginClip(clip).BeginChildElements(_content);
    }

    public void End(GuiComposer composer, ICoreClientAPI api, double height, Action onScroll)
    {
        var page = _page;
        var clip = _clip;
        var content = _content;
        content.fixedHeight = Math.Max(clip.fixedHeight, height);
        composer.EndChildElements().EndClip();
        var scrollbar = new SetupWizardSheetEditor.BuilderScrollbar(api, value =>
        {
            _positions[page] = value;
            onScroll();
            content.fixedY = -value;
            content.MarkDirtyRecursive();
            content.CalcWorldBounds();
        }, ElementBounds.Fixed(clip.fixedX + clip.fixedWidth - 12, clip.fixedY, 12, clip.fixedHeight), clip);
        composer.AddInteractiveElement(scrollbar, "settings-scroll");
        var initialScroll = _positions.GetValueOrDefault(page);
        composer.OnComposed += () =>
        {
            scrollbar.SetHeights((float)clip.fixedHeight, (float)content.fixedHeight);
            scrollbar.MoveTo(initialScroll);
        };
        composer.OnFocusChanged += focused =>
        {
            var control = composer.CurrentTabIndexElement;
            if (!focused || control?.Bounds.ParentBounds != content) return;
            var top = Math.Max(0, control.Bounds.fixedY - 23);
            var bottom = control.Bounds.fixedY + control.Bounds.fixedHeight;
            var scroll = _positions.GetValueOrDefault(page);
            if (top < scroll) scrollbar.MoveTo((float)top);
            else if (bottom > scroll + clip.fixedHeight) scrollbar.MoveTo((float)(bottom - clip.fixedHeight));
        };
    }
}
