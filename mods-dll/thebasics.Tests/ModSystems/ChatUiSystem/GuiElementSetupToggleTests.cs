using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class GuiElementSetupToggleTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void BothStatesRenderAndProgrammaticChangesDoNotNotify(double scale)
    {
        using var host = Host(scale);
        var changes = new List<bool>();
        using var composer = Composer(host);
        var toggle = Toggle(host, changes.Add);
        composer.AddInteractiveElement(toggle, "toggle").Compose();
        var off = Render(host, composer);
        toggle.SetValue(true);
        var on = Render(host, composer);
        Assert.False(off.SequenceEqual(on));
        Assert.True(toggle.Value);
        toggle.SetValue(false);
        Assert.Equal(off, Render(host, composer));
        Assert.Empty(changes);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void MatchingLeftReleaseTogglesAndHoverUsesHandCursor(double scale)
    {
        using var host = Host(scale);
        var changes = new List<bool>();
        using var composer = Composer(host);
        var toggle = Toggle(host, changes.Add);
        composer.AddInteractiveElement(toggle, "toggle").Compose();
        var (x, y) = Center(toggle.Bounds);
        host.SetMouse(x, y);
        Render(host, composer);
        Assert.Equal("linkselect", composer.MouseOverCursor);
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.Empty(changes);
        var down = new MouseEvent(x, y, EnumMouseButton.Left, 0);
        composer.OnMouseDown(down);
        Assert.True(down.Handled);
        Assert.Empty(changes);
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.Equal(new[] { true }, changes);
        Assert.True(toggle.HasFocus);
        composer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Right, 0));
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Right, 0));
        composer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        composer.OnMouseUp(new MouseEvent(500, 300, EnumMouseButton.Left, 0));
        Assert.Equal(new[] { true }, changes);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void FocusShowsOutlineAndSpaceEnterAndKeypadEnterToggle(double scale)
    {
        using var host = Host(scale);
        var changes = new List<bool>();
        using var composer = Composer(host);
        var toggle = Toggle(host, changes.Add);
        composer.AddInteractiveElement(toggle, "toggle").Compose(focusFirstElement: false);
        Assert.False(toggle.HasFocus);
        var idle = Render(host, composer);
        Assert.True(composer.FocusElement(toggle.TabIndex));
        Assert.True(toggle.HasFocus);
        Assert.False(idle.SequenceEqual(Render(host, composer)));
        foreach (var key in new[] { GlKeys.Space, GlKeys.Enter, GlKeys.KeypadEnter })
        {
            var args = new KeyEvent { KeyCode = (int)key };
            composer.OnKeyDown(args, true);
            Assert.True(args.Handled);
        }
        Assert.Equal(new[] { true, false, true }, changes);
        var handled = new KeyEvent { KeyCode = (int)GlKeys.Space, Handled = true };
        toggle.OnKeyDown(host.Api, handled);
        Assert.Equal(3, changes.Count);
        toggle.OnFocusLost();
        toggle.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Space });
        Assert.Equal(3, changes.Count);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void DisabledStateDimsClearsFocusAndRejectsPendingMouseAndKeyboardInput(double scale)
    {
        using var host = Host(scale);
        var changes = new List<bool>();
        using var composer = Composer(host);
        var toggle = Toggle(host, changes.Add);
        composer.AddInteractiveElement(toggle, "toggle").Compose();
        var (x, y) = Center(toggle.Bounds);
        host.SetMouse(x, y);
        var active = Render(host, composer);
        composer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        toggle.Enabled = false;
        var disabled = Render(host, composer);
        Assert.False(active.SequenceEqual(disabled));
        Assert.False(toggle.HasFocus);
        Assert.False(toggle.Focusable);
        Assert.Null(toggle.MouseOverCursor);
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        composer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        toggle.OnFocusGained();
        toggle.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Space });
        Assert.Empty(changes);
        Assert.False(toggle.Value);
        toggle.Enabled = true;
        Assert.Equal("linkselect", toggle.MouseOverCursor);
        Assert.True(toggle.Focusable);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LanguageFlagsStayInDraftAcrossSelectionAndSaveTheirBooleanValues(double scale)
    {
        using var host = DialogHost(scale);
        var languages = new List<LanguageConfigEntryMessage>
        {
            new() { OriginalName = "Common", Name = "Common", Prefix = "c", Color = "#ffffff", Default = true, Hidden = false },
            new() { OriginalName = "Trade", Name = "Trade", Prefix = "t", Color = "#ffffff", Default = false, Hidden = false }
        };
        List<LanguageConfigEntryMessage>? saved = null;
        using var dialog = new LanguageConfigDialog(host.Api, languages, "", true, value => saved = value, () => { }, () => { });
        Assert.True(Assert.IsType<GuiElementSetupToggle>(dialog.SingleComposer["toggle-Default"]).Value);
        Activate(dialog.SingleComposer, "toggle-Default");
        Activate(dialog.SingleComposer, "toggle-Hidden");
        Assert.True(dialog.JumpTo("Trade", "Hidden"));
        Assert.True(dialog.SingleComposer["toggle-Hidden"].HasFocus);
        Assert.False(Assert.IsType<GuiElementSetupToggle>(dialog.SingleComposer["toggle-Hidden"]).Value);
        Assert.IsAssignableFrom<GuiElementDropDown>(dialog.SingleComposer["language-selector"]);
        Assert.True(dialog.JumpTo("Common", "Default"));
        Assert.False(Assert.IsType<GuiElementSetupToggle>(dialog.SingleComposer["toggle-Default"]).Value);
        Assert.True(Assert.IsType<GuiElementSetupToggle>(dialog.SingleComposer["toggle-Hidden"]).Value);
        Activate(dialog.SingleComposer, "save");
        Assert.NotNull(saved);
        Assert.False(saved[0].Default);
        Assert.True(saved[0].Hidden);
        Assert.False(saved[1].Default);
        Assert.False(saved[1].Hidden);
        Assert.True(languages[0].Default);
        Assert.False(languages[0].Hidden);
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void CharacterFieldFlagsStayInDraftAcrossSelectionAndSaveTheirBooleanValues(double scale)
    {
        using var host = DialogHost(scale);
        var fields = new List<CharacterSheetFieldConfigEntryMessage>
        {
            new() { OriginalId = "bio", Id = "bio", Label = "Bio", Type = "string", Optional = true, ShowInLook = false },
            new() { OriginalId = "trade", Id = "trade", Label = "Trade", Type = "select", Options = "Scribe, Glassblower", Optional = true, ShowInLook = false }
        };
        List<CharacterSheetFieldConfigEntryMessage>? saved = null;
        using var dialog = new CharacterSheetFieldConfigDialog(host.Api, fields, value => saved = value, () => { }, () => { });
        Activate(dialog.SingleComposer, "toggle-Optional");
        Activate(dialog.SingleComposer, "toggle-ShowInLook");
        Assert.True(dialog.JumpTo("trade", "Optional"));
        Assert.True(dialog.SingleComposer["toggle-Optional"].HasFocus);
        Assert.True(Assert.IsType<GuiElementSetupToggle>(dialog.SingleComposer["toggle-Optional"]).Value);
        Assert.IsAssignableFrom<GuiElementDropDown>(dialog.SingleComposer["dropdown-Type"]);
        Assert.True(dialog.JumpTo("bio", "ShowInLook"));
        Assert.False(Assert.IsType<GuiElementSetupToggle>(dialog.SingleComposer["toggle-Optional"]).Value);
        Assert.True(Assert.IsType<GuiElementSetupToggle>(dialog.SingleComposer["toggle-ShowInLook"]).Value);
        Activate(dialog.SingleComposer, "save");
        Assert.NotNull(saved);
        Assert.False(saved[0].Optional);
        Assert.True(saved[0].ShowInLook);
        Assert.True(saved[1].Optional);
        Assert.False(saved[1].ShowInLook);
        Assert.True(fields[0].Optional);
        Assert.False(fields[0].ShowInLook);
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void VisiblePressReleasedInClippedPartDoesNotActivateOrLeavePendingPress(double scale)
    {
        using var host = Host(scale);
        var changes = new List<bool>();
        using var composer = Composer(host);
        var toggle = Toggle(host, changes.Add);
        var clip = ElementBounds.Fixed(0, 0, 300, 45);
        composer.BeginClip(clip).AddInteractiveElement(toggle, "toggle").EndClip().Compose();
        var x = (int)(toggle.Bounds.renderX + toggle.Bounds.InnerWidth / 2);
        var visibleY = (int)(toggle.Bounds.renderY + 3 * scale);
        var clippedY = (int)(clip.renderY + clip.InnerHeight + 3 * scale);
        Assert.True(toggle.IsPositionInside(x, visibleY));
        Assert.True(toggle.Bounds.PointInside(x, clippedY));
        Assert.False(toggle.IsPositionInside(x, clippedY));
        composer.OnMouseDown(new MouseEvent(x, visibleY, EnumMouseButton.Left, 0));
        var release = new MouseEvent(x, clippedY, EnumMouseButton.Left, 0);
        composer.OnMouseUp(release);
        Assert.True(release.Handled);
        Assert.Empty(changes);
        composer.OnMouseUp(new MouseEvent(x, visibleY, EnumMouseButton.Left, 0));
        Assert.Empty(changes);
        composer.OnMouseDown(new MouseEvent(x, visibleY, EnumMouseButton.Left, 0));
        composer.OnMouseUp(new MouseEvent(x, visibleY, EnumMouseButton.Left, 0));
        Assert.Equal(new[] { true }, changes);
        toggle.InsideClipBounds = null;
        composer.OnMouseDown(new MouseEvent(x, clippedY, EnumMouseButton.Left, 0));
        composer.OnMouseUp(new MouseEvent(x, clippedY, EnumMouseButton.Left, 0));
        Assert.Equal(new[] { true, false }, changes);
        Assert.Empty(host.UnsupportedCalls);
    }
    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 640, 400, scale);
    private static PreviewHost DialogHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
    private static void Activate(GuiComposer composer, string key)
    {
        Assert.True(composer.FocusElement(composer[key].TabIndex));
        var args = new KeyEvent { KeyCode = (int)GlKeys.Enter };
        composer.OnKeyDown(args, true);
        Assert.True(args.Handled);
    }
    private static GuiComposer Composer(PreviewHost host) => host.Api.Gui.CreateCompo("toggle", ElementBounds.Fixed(0, 0, 510, 310));
    private static GuiElementSetupToggle Toggle(PreviewHost host, Action<bool> onChanged) =>
        new(host.Api, false, onChanged, ElementBounds.Fixed(20, 30, 112, 28));
    private static (int X, int Y) Center(ElementBounds bounds) =>
        ((int)(bounds.renderX + bounds.InnerWidth / 2), (int)(bounds.renderY + bounds.InnerHeight / 2));
    private static byte[] Render(PreviewHost host, GuiComposer composer)
    {
        host.Canvas.Clear(30, 30, 30, 255);
        composer.Render(0);
        return host.Canvas.GetRgbaPixels();
    }
}


