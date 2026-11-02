using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardChatFocusElementTests
{
    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void LifecycleKeepsTheComposerAndSettingsFocusWhileHidingAllClosedChatPixels(double scale)
    {
        using var host = Host(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var element = new SetupWizardChatFocusElement(host.Api, ElementBounds.Fixed(12, 20, 310, 241), _ => { });
        element.Refresh(draft, 0.75, true);
        using var outer = host.Api.Gui.CreateCompo("chat-focus-wrapper-test", ElementBounds.Fixed(180, 130, 720, 350))
            .AddInteractiveElement(element, "sample")
            .AddTextInput(ElementBounds.Fixed(360, 30, 160, 30), _ => { }, CairoFont.WhiteSmallText(), "setting")
            .Compose(focusFirstElement: false);
        var sample = element.Composer;
        var tabs = sample.GetHorizontalTabs("preview-chat-tabs");
        Assert.InRange(tabs.Bounds.renderX - element.Bounds.renderX, -0.1, 0.1);
        Assert.InRange(tabs.Bounds.renderY - element.Bounds.renderY, -0.1, 0.1);
        var setting = outer.GetTextInput("setting");
        outer.FocusElement(setting.TabIndex);
        Assert.True(setting.HasFocus);
        foreach (double time in new[] { 1.25, 2.25, 4.25, 6.25, 7.25 })
        {
            element.Refresh(draft, time, true);
            Assert.Same(sample, element.Composer);
            Assert.True(setting.HasFocus);
        }

        host.Canvas.Clear(30, 30, 30, 255);
        var empty = host.Canvas.GetRgbaPixels();
        element.Refresh(draft, 6.25, true);
        element.RenderInteractiveElements(0);
        Assert.Equal(empty, host.Canvas.GetRgbaPixels());
        element.Refresh(draft, 7.25, true);
        element.RenderInteractiveElements(0);
        Assert.False(empty.AsSpan().SequenceEqual(host.Canvas.GetRgbaPixels()));
        Assert.Empty(host.UnsupportedCalls);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void OnlyOpenTabsAcceptMouseEventsAndOwnedTexturesAreDisposed(double scale)
    {
        using var host = Host(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        int clicks = 0, selected = -1;
        var element = new SetupWizardChatFocusElement(host.Api, ElementBounds.Fixed(12, 20, 310, 241),
            index => { clicks++; selected = index; });
        element.Refresh(draft, 4.25, true);
        var outer = host.Api.Gui.CreateCompo("chat-focus-events-test", ElementBounds.Fixed(180, 130, 340, 300))
            .AddInteractiveElement(element, "sample").Compose(focusFirstElement: false);
        try
        {
            var sample = element.Composer;
            var input = sample.GetChatInput("preview-chat-input");
            Assert.False(input.Enabled);
            Assert.False(input.Focusable);
            var tabs = sample.GetHorizontalTabs("preview-chat-tabs");
            outer.Bounds.WithFixedPosition(240, 175);
            outer.Bounds.MarkDirtyRecursive();
            outer.Bounds.CalcWorldBounds();
            element.RenderInteractiveElements(0);
            Assert.Same(sample, element.Composer);
            Assert.InRange(tabs.Bounds.renderX - element.Bounds.renderX, -0.1, 0.1);
            Assert.InRange(tabs.Bounds.renderY - element.Bounds.renderY, -0.1, 0.1);
            var widths = (int[])typeof(GuiElementHorizontalTabs).GetField("tabWidths", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tabs)!;
            int x = (int)(tabs.Bounds.absX + GuiElement.scaled(tabs.unscaledTabSpacing) * 2 + widths[0] + widths[1] / 2d);
            int y = (int)(tabs.Bounds.absY + tabs.Bounds.InnerHeight / 2);
            host.SetMouse(x, y);
            element.OnMouseDown(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
            element.OnMouseUp(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
            Assert.Equal(1, clicks);
            Assert.Equal(1, selected);
            element.Refresh(draft, 6.25, true);
            element.OnMouseDown(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
            element.OnMouseUp(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
            Assert.Equal(1, clicks);
            Assert.Empty(input.GetText());
            Assert.False(input.HasFocus);
            Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
            var historyTexture = sample.GetRichtext("preview-text").richtTextTexture;
            outer.Dispose();
            Assert.Null(element.Composer);
            Assert.Equal(0, historyTexture.TextureId);
            Assert.Empty(host.UnsupportedCalls);
        }
        finally { outer.Dispose(); }
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void FocusedOpenTabsKeepNativeArrowKeysAndClosedChatRejectsFocusAndKeys(double scale)
    {
        using var host = Host(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        int selections = 0;
        var element = new SetupWizardChatFocusElement(host.Api, ElementBounds.Fixed(12, 20, 310, 241), _ => selections++);
        element.Refresh(draft, 4.25, true);
        using var outer = host.Api.Gui.CreateCompo("chat-focus-keyboard-test", ElementBounds.Fixed(180, 130, 340, 300))
            .AddInteractiveElement(element, "sample").Compose(focusFirstElement: false);
        var tabs = element.Composer.GetHorizontalTabs("preview-chat-tabs");
        var unfocused = new KeyEvent { KeyCode = (int)GlKeys.Right };
        outer.OnKeyDown(unfocused, haveFocus: true);
        Assert.False(unfocused.Handled);
        Assert.Equal(0, selections);
        Assert.True(outer.FocusElement(element.TabIndex));
        Assert.True(element.HasFocus);
        Assert.True(tabs.HasFocus);
        var right = new KeyEvent { KeyCode = (int)GlKeys.Right };
        outer.OnKeyDown(right, haveFocus: true);
        Assert.True(right.Handled);
        Assert.Equal(1, tabs.activeElement);
        var left = new KeyEvent { KeyCode = (int)GlKeys.Left };
        outer.OnKeyDown(left, haveFocus: true);
        Assert.True(left.Handled);
        Assert.Equal(0, tabs.activeElement);
        Assert.Equal(2, selections);

        element.Refresh(draft, 6.25, true);
        Assert.False(element.Focusable);
        Assert.False(element.HasFocus);
        Assert.False(tabs.HasFocus);
        Assert.False(outer.FocusElement(element.TabIndex));
        var closed = new KeyEvent { KeyCode = (int)GlKeys.Right };
        outer.OnKeyDown(closed, haveFocus: true);
        Assert.False(closed.Handled);
        Assert.Equal(2, selections);
        element.Refresh(draft, 7.25, true);
        Assert.True(element.Focusable);
        Assert.False(element.HasFocus);
        Assert.False(tabs.HasFocus);
        Assert.Empty(host.UnsupportedCalls);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void ClosedReservedTabIsSkippedInBothKeyboardTraversalDirections(double scale)
    {
        using var host = Host(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var element = new SetupWizardChatFocusElement(host.Api, ElementBounds.Fixed(12, 50, 310, 241), _ => { });
        using var outer = host.Api.Gui.CreateCompo("chat-focus-traversal-test", ElementBounds.Fixed(180, 130, 340, 340))
            .AddSmallButton("Before", () => true, ElementBounds.Fixed(12, 12, 100, 24), key: "before")
            .AddInteractiveElement(element, "sample")
            .AddSmallButton("After", () => true, ElementBounds.Fixed(12, 300, 100, 24), key: "after");
        element.Refresh(draft, 6.25, true);
        outer.Compose(focusFirstElement: false);
        var before = outer.GetButton("before");
        var after = outer.GetButton("after");
        Assert.True(element.TabIndex >= 0);
        Assert.True(outer.FocusElement(before.TabIndex));
        var forward = new KeyEvent { KeyCode = (int)GlKeys.Tab };
        Assert.True(element.SkipClosedTabNavigation(outer, forward));
        Assert.True(forward.Handled);
        Assert.True(after.HasFocus);
        Assert.False(element.HasFocus);
        var backward = new KeyEvent { KeyCode = (int)GlKeys.Tab, ShiftPressed = true };
        Assert.True(element.SkipClosedTabNavigation(outer, backward));
        Assert.True(backward.Handled);
        Assert.True(before.HasFocus);
        Assert.False(element.HasFocus);

        element.Refresh(draft, 7.25, true);
        var open = new KeyEvent { KeyCode = (int)GlKeys.Tab };
        Assert.False(element.SkipClosedTabNavigation(outer, open));
        outer.OnKeyDown(open, haveFocus: true);
        Assert.True(element.HasFocus);
        Assert.True(element.Composer.GetHorizontalTabs("preview-chat-tabs").HasFocus);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
}
