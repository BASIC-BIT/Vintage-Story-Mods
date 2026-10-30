using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class GuiElementPrivilegePickerTests
{
    private static readonly ConfigAdminPrivilegeSuggestion[] Suggestions =
    [
        new() { Code = "chat", Description = "Send chat messages" },
        new() { Code = "commandplayer", Description = "Manage other players" },
        new() { Code = "custom.server", Description = "A registered server privilege" },
        new() { Code = "server.chat", Description = "Read server chat history" }
    ];

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void UnknownAndBlankValuesStayEditableWithoutAutomaticSelection(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<string>();
        using var composer = CreateComposer(host);
        var picker = CreatePicker(host, changes, "custom.unregistered");
        composer.AddInteractiveElement(picker, "privilege").Compose();
        Render(host, composer);

        Assert.Equal("custom.unregistered", picker.GetText());
        Assert.Empty(changes);
        picker.SetValue("");
        Type(composer, "another.mod");
        Assert.Equal("another.mod", picker.Value);
        Assert.False(picker.IsSuggestionsOpen);
        Assert.Equal("another.mod", changes.Last());
        picker.SetValue("  custom value  ");
        Assert.Equal("  custom value  ", picker.GetText());
        Assert.Equal(11, changes.Count);
        picker.SetValue("");
        Assert.Equal("", picker.GetText());
        Assert.Equal(11, changes.Count);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void TypingFiltersCodeAndDescriptionAndPrioritizesCodePrefixes(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<string>();
        using var composer = CreateComposer(host);
        var picker = CreatePicker(host, changes, "");
        composer.AddInteractiveElement(picker, "privilege").Compose();

        Type(composer, "chat");
        Assert.True(picker.IsSuggestionsOpen);
        Assert.Equal(new[] { "chat", "server.chat" }, Menu(picker).Values);
        Assert.Equal(0, Menu(picker).HoveredIndex);
        Assert.Equal("chat", picker.Value);
        Assert.Equal(4, changes.Count);
        Press(composer, GlKeys.Escape);
        Assert.False(picker.IsSuggestionsOpen);

        picker.SetValue("");
        Type(composer, "players");
        Assert.Equal(new[] { "commandplayer" }, Menu(picker).Values);
        Assert.Equal("players", picker.Value);
        changes.Clear();
        Press(composer, GlKeys.Enter);
        Assert.Equal("commandplayer", picker.Value);
        Assert.Equal(new[] { "commandplayer" }, changes);
        Assert.False(picker.IsSuggestionsOpen);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ArrowShowsEverySuggestionAndClickingItsDescriptionChoosesOnce(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<string>();
        using var composer = CreateComposer(host);
        var picker = CreatePicker(host, changes, "commandplayer");
        composer.AddInteractiveElement(picker, "privilege").Compose();
        Click(host, composer, (int)(picker.Bounds.renderX + picker.Bounds.InnerWidth - 8 * scale),
            (int)(picker.Bounds.renderY + picker.Bounds.InnerHeight / 2));
        Assert.True(picker.IsSuggestionsOpen);
        Assert.Equal(4, Menu(picker).Values.Length);
        Render(host, composer);
        Assert.Equal("normal", composer.MouseOverCursor);

        var menu = Menu(picker);
        var popup = Field<ElementBounds>(menu, "visibleBounds");
        host.SetMouse((int)(popup.renderX + 30 * scale), (int)(popup.renderY + 43 * scale));
        Render(host, composer);
        Assert.Equal("normal", composer.MouseOverCursor);
        Click(host, composer, (int)(popup.renderX + 30 * scale), (int)(popup.renderY + 43 * scale));
        Assert.Equal("chat", picker.Value);
        Assert.Equal(new[] { "chat" }, changes);
        Assert.False(picker.IsSuggestionsOpen);
        Assert.True(picker.HasFocus);
        host.SetMouse((int)(picker.Bounds.renderX + 30 * scale), (int)(picker.Bounds.renderY + picker.Bounds.InnerHeight / 2));
        Render(host, composer);
        Assert.Equal("textselect", composer.MouseOverCursor);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void EscapeAndOutsideClickDismissWithoutChangingValueAndTabMovesFocus(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<string>();
        using var composer = CreateComposer(host);
        var picker = CreatePicker(host, changes, "chat");
        composer.AddInteractiveElement(picker, "privilege")
            .AddSmallButton("Next", () => true, ElementBounds.Fixed(20, 600, 90, 28), key: "next").Compose();

        Press(composer, GlKeys.Down);
        Press(composer, GlKeys.Down);
        Assert.Equal("chat", picker.Value);
        Press(composer, GlKeys.Escape);
        Assert.False(picker.IsSuggestionsOpen);
        Assert.True(picker.HasFocus);
        Press(composer, GlKeys.Down);
        Press(composer, GlKeys.Tab);
        Assert.False(picker.IsSuggestionsOpen);
        Assert.False(picker.HasFocus);
        Assert.True(composer.GetElement("next").HasFocus);
        Assert.True(composer.FocusElement(0));
        Press(composer, GlKeys.Down);
        host.SetMouse(850, 600);
        composer.OnMouseDown(new MouseEvent(850, 600, EnumMouseButton.Left, 0));
        Assert.False(picker.IsSuggestionsOpen);
        Assert.Equal("chat", picker.Value);
        Assert.Empty(changes);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void DisabledPickerPreservesTextAndIgnoresInput(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<string>();
        using var composer = CreateComposer(host);
        var picker = CreatePicker(host, changes, "custom.server");
        composer.AddInteractiveElement(picker, "privilege").Compose();
        Press(composer, GlKeys.Down);
        picker.Enabled = false;
        Assert.False(picker.IsSuggestionsOpen);
        Assert.False(picker.Focusable);
        Assert.False(picker.HasFocus);
        picker.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Down });
        picker.OnKeyPress(host.Api, new KeyEvent { KeyCode = 'x', KeyChar = 'x' });
        Render(host, composer);
        Assert.Equal("custom.server", picker.Value);
        Assert.Empty(changes);
        picker.Enabled = true;
        Assert.True(composer.FocusElement(0));
        Press(composer, GlKeys.Down);
        Assert.True(picker.IsSuggestionsOpen);
        Assert.Equal("custom.server", picker.Value);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LargeSuggestionListsUseViewportTexturesAndReachLastEntry(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<string>();
        using var composer = CreateComposer(host);
        var entries = Enumerable.Range(0, 700).Select(index => new ConfigAdminPrivilegeSuggestion
        { Code = $"custom.{index:000}", Description = $"Description for custom privilege {index}" }).ToArray();
        var picker = new GuiElementPrivilegePicker(host.Api, ElementBounds.Fixed(20, 40, 600, 32),
            "custom.000", entries, changes.Add, CairoFont.WhiteSmallText());
        composer.AddInteractiveElement(picker, "privilege").Compose();
        Press(composer, GlKeys.Down);
        var menu = Menu(picker);
        Assert.Equal(700, menu.Values.Length);
        Assert.InRange(Field<LoadedTexture>(menu, "dropDownTexture").Height, 1, host.Api.Render.FrameHeight);
        Assert.InRange(Field<GuiElementRichtext[]>(menu, "richtTextElem").Length, 1, 20);
        Press(composer, GlKeys.End);
        Render(host, composer);
        Assert.Equal(699, menu.HoveredIndex);
        Assert.Contains(Field<GuiElementRichtext[]>(menu, "richtTextElem")
            .SelectMany(row => row.Components.OfType<RichTextComponent>()), row => row.DisplayText == "custom.699");
        Assert.InRange(Field<LoadedTexture>(menu, "dropDownTexture").Height, 1, host.Api.Render.FrameHeight);
        Press(composer, GlKeys.Enter);
        Assert.Equal("custom.699", picker.Value);
        Assert.Equal(new[] { "custom.699" }, changes);
        Assert.False(picker.IsSuggestionsOpen);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LowerPickerOpensUpwardAndWheelScrollsTheSuggestions(double scale)
    {
        using var host = CreateHost(scale);
        var changes = new List<string>();
        using var composer = CreateComposer(host);
        var entries = Enumerable.Range(0, 100).Select(index => new ConfigAdminPrivilegeSuggestion
        { Code = $"custom.{index:000}", Description = "A long registered privilege description that should stay inside the menu" }).ToArray();
        var picker = new GuiElementPrivilegePicker(host.Api, ElementBounds.Fixed(20, 510, 400, 30).WithScale(0.9),
            "custom.000", entries, changes.Add, CairoFont.WhiteSmallText().WithFontSize(18));
        composer.AddInteractiveElement(picker, "privilege").Compose();
        Press(composer, GlKeys.Down);
        Render(host, composer);
        var menu = Menu(picker);
        var popup = Field<ElementBounds>(menu, "visibleBounds");
        Assert.True(popup.renderY >= 0);
        Assert.InRange(Math.Abs(popup.renderY + popup.InnerHeight - picker.Bounds.renderY), 0, 0.01);
        host.SetMouse((int)(popup.renderX + 50), (int)(popup.renderY + 50));
        var wheel = new MouseWheelEventArgs { delta = -1, deltaPrecise = -1 };
        composer.OnMouseWheel(wheel);
        Assert.True(wheel.IsHandled);
        Assert.True(Field<double>(menu, "scrollOffY") > 0);
        Render(host, composer);
        Assert.Equal("custom.000", picker.Value);
        Assert.Empty(changes);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost CreateHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 900, scale);

    private static GuiComposer CreateComposer(PreviewHost host) =>
        host.Api.Gui.CreateCompo("privilege-picker", ElementBounds.Fixed(0, 0, 900, 700));

    private static GuiElementPrivilegePicker CreatePicker(PreviewHost host, List<string> changes, string value) =>
        new(host.Api, ElementBounds.Fixed(20, 40, 600, 32), value, Suggestions, changes.Add, CairoFont.WhiteSmallText());

    private static GuiElementListMenu Menu(GuiElementPrivilegePicker picker) => Field<GuiElementListMenu>(picker, "menu");

    private static T Field<T>(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) return (T)field.GetValue(target)!;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }

    private static void Type(GuiComposer composer, string text)
    {
        foreach (var character in text)
        {
            var args = new KeyEvent { KeyCode = character, KeyChar = character };
            composer.OnKeyPress(args);
            Assert.True(args.Handled);
        }
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

    private static void Render(PreviewHost host, GuiComposer composer)
    {
        host.Canvas.Clear(30, 30, 30, 255);
        composer.Render(0);
    }
}
