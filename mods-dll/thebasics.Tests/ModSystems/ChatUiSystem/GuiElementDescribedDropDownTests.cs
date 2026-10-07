using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class GuiElementDescribedDropDownTests
{
    private static readonly string[] Codes = ["rp", "vanilla", "off", "typing", "both"];
    private static readonly string[] Names = ["Roleplay text", "Standard text", "Hidden", "Typing only", "Text and typing"];
    private static readonly string[] Descriptions =
        ["Nearby speech appears above players.", "Use the game's standard speech bubbles.", "Hide speech bubbles above players.",
            "Show when someone is typing.", "Show both nearby speech and typing."];

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void FirstRowTitleAndDescriptionAreClickableAndCollapsedValueStaysPlain(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<(string Code, bool On)>();
        using var composer = host.Api.Gui.CreateCompo("described-options", ElementBounds.Fixed(0, 0, 900, 560));
        var dropdown = CreateDropdown(host, changes, 40);
        composer.AddInteractiveElement(dropdown, "presentation").Compose();
        var closed = Render(host, composer);

        foreach (var offset in new[] { 25, 45 })
        {
            dropdown.SetSelectedIndex(1);
            changes.Clear();
            Press(composer, GlKeys.Enter);
            Assert.True(dropdown.listMenu.IsOpened);
            Assert.False(closed.SequenceEqual(Render(host, composer)));
            AssertExpandedTitleAndSubtitle(dropdown);
            var x = (int)(dropdown.listMenu.Bounds.renderX + 20 * scale);
            var y = (int)(PopupTop(dropdown) + offset * scale);
            Assert.True(dropdown.IsPositionInside(x, y));

            Click(host, composer, x, y);

            Assert.False(dropdown.listMenu.IsOpened);
            Assert.Equal("rp", dropdown.SelectedValue);
            Assert.Equal(new[] { ("rp", true) }, changes);
            Assert.Equal(Names[0], CurrentLabel(dropdown));
            Assert.Equal(Names, dropdown.listMenu.Names);
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void BottomPopupOpensAboveAndItsLastDescriptionRemainsVisibleAndClickable(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<(string Code, bool On)>();
        using var composer = host.Api.Gui.CreateCompo("described-bottom-options", ElementBounds.Fixed(0, 0, 900, 560));
        var dropdown = CreateDropdown(host, changes, 480);
        composer.AddInteractiveElement(dropdown, "presentation").Compose();
        Press(composer, GlKeys.Enter);
        Render(host, composer);

        var top = PopupTop(dropdown);
        var bottom = top + Codes.Length * 58 * scale;
        Assert.True(top >= 0);
        Assert.True(bottom <= host.Api.Render.FrameHeight);
        Assert.InRange(Math.Abs(bottom - dropdown.Bounds.renderY), 0, 0.01);
        var x = (int)(dropdown.listMenu.Bounds.renderX + 20 * scale);
        var y = (int)(top + (4 * 58 + 45) * scale);
        Assert.True(dropdown.IsPositionInside(x, y));

        Click(host, composer, x, y);

        Assert.Equal("both", dropdown.SelectedValue);
        Assert.Equal(new[] { ("both", true) }, changes);
        Assert.Equal(Names[4], CurrentLabel(dropdown));
        Assert.False(dropdown.listMenu.IsOpened);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void NativeArrowSelectionCallsOnceAndTabClosesPopupAndMovesFocus(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<(string Code, bool On)>();
        using var composer = host.Api.Gui.CreateCompo("described-keyboard-options", ElementBounds.Fixed(0, 0, 900, 560));
        var dropdown = CreateDropdown(host, changes, 40);
        composer.AddInteractiveElement(dropdown, "presentation")
            .AddSmallButton("Next", () => true, ElementBounds.Fixed(20, 480, 90, 28), key: "next").Compose();

        Press(composer, GlKeys.Enter);
        Press(composer, GlKeys.Down);
        Assert.Empty(changes);
        Press(composer, GlKeys.Enter);
        Assert.Equal("off", dropdown.SelectedValue);
        Assert.Equal(new[] { ("off", true) }, changes);
        Assert.Equal(Names[2], CurrentLabel(dropdown));

        Press(composer, GlKeys.Space);
        Assert.True(dropdown.listMenu.IsOpened);
        Press(composer, GlKeys.Tab);
        Assert.False(dropdown.listMenu.IsOpened);
        Assert.False(dropdown.HasFocus);
        Assert.True(composer.GetElement("next").HasFocus);
        Assert.Single(changes);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void OutsideClicksDismissWithoutSelectingAndMouseUpDoesNotReopen(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<(string Code, bool On)>();
        using var composer = host.Api.Gui.CreateCompo("described-outside-options", ElementBounds.Fixed(0, 0, 900, 560));
        var dropdown = CreateDropdown(host, changes, 40);
        composer.AddInteractiveElement(dropdown, "presentation").Compose();

        for (var position = 0; position < 4; position++)
        {
            Assert.True(composer.FocusElement(0));
            Press(composer, GlKeys.Enter);
            var x = (int)(dropdown.Bounds.renderX + 20 * scale);
            var y = (int)(PopupTop(dropdown) + 45 * scale);
            if (position == 0) x = (int)(dropdown.Bounds.renderX - 10 * scale);
            if (position == 1) x = (int)(dropdown.Bounds.renderX + dropdown.Bounds.InnerWidth + 10 * scale);
            if (position == 2) y = (int)(PopupTop(dropdown) + (Codes.Length * 58 + 10) * scale);
            if (position == 3) y = (int)(dropdown.Bounds.renderY + dropdown.Bounds.InnerHeight / 2);
            host.SetMouse(x, y);

            composer.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));

            Assert.False(dropdown.listMenu.IsOpened);
            Assert.Equal("vanilla", dropdown.SelectedValue);
            Assert.Empty(changes);
            composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            Assert.False(dropdown.listMenu.IsOpened);
            Assert.Equal(Names[1], CurrentLabel(dropdown));
            Assert.Empty(changes);
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost CreateHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);

    private static GuiElementDescribedDropDown CreateDropdown(PreviewHost host, List<(string, bool)> changes, double y) =>
        new(host.Api, Codes, Names, Descriptions, 1, (code, on) => changes.Add((code, on)),
            ElementBounds.Fixed(20, y, 600, 28), CairoFont.WhiteSmallText().WithFontSize(20));

    private static double PopupTop(GuiElementDropDown dropdown) => dropdown.listMenu.Bounds.renderY + dropdown.listMenu.Bounds.InnerHeight;

    private static string CurrentLabel(GuiElementDropDown dropdown) =>
        string.Concat(dropdown.richTextElem.Components.OfType<RichTextComponent>().Select(component => component.DisplayText));

    private static void AssertExpandedTitleAndSubtitle(GuiElementDropDown dropdown)
    {
        var rows = (GuiElementRichtext[])typeof(GuiElementListMenu)
            .GetField("richtTextElem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dropdown.listMenu)!;
        var components = rows[0].Components.OfType<RichTextComponent>().ToArray();
        Assert.Contains(components, component => component.DisplayText == Names[0] && component.Font.UnscaledFontsize == 17);
        Assert.Contains(components, component => component.DisplayText == Descriptions[0] && component.Font.UnscaledFontsize == 13);
    }

    private static byte[] Render(PreviewHost host, GuiComposer composer)
    {
        host.Canvas.Clear(30, 30, 30, 255);
        composer.Render(0);
        return host.Canvas.GetRgbaPixels();
    }

    private static void Press(GuiComposer composer, GlKeys key)
    {
        var args = new KeyEvent { KeyCode = (int)key };
        composer.OnKeyDown(args, true);
        Assert.True(args.Handled);
    }

    private static void Click(PreviewHost host, GuiComposer composer, int x, int y)
    {
        host.SetMouse(x, y);
        var args = new MouseEvent(x, y, EnumMouseButton.Left, 0);
        composer.OnMouseDown(args);
        Assert.True(args.Handled);
        composer.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
}
