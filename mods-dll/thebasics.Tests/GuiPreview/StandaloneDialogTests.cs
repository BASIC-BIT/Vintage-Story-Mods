using TheBasics.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Xunit;

namespace thebasics.Tests.GuiPreview;

[CollectionDefinition("Standalone GUI", DisableParallelization = true)]
public class StandaloneGuiCollection;

public sealed class VisualTheoryAttribute : TheoryAttribute
{
    public VisualTheoryAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")))
            Skip = "Full GUI snapshots require THEBASICS_GUI_ASSETS and a complete VINTAGE_STORY installation. Run scripts/preview-gui.ps1 -Test.";
    }
}

[Collection("Standalone GUI")]
public class StandaloneDialogTests
{
    [VisualTheory]
    [InlineData("language-default")]
    public void Production_focus_change_fails_pixel_comparison_and_writes_diff(string scenario)
    {
        var output = Path.Combine(Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT") ?? Path.GetTempPath(), "focus-regression");
        Directory.CreateDirectory(output);
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        using var scene = PreviewScene.Create(scenario, host);
        host.RenderGuiDialog(scene.Dialog, 0);
        var before = Path.Combine(output, "before.png");
        var after = Path.Combine(output, "after.png");
        var diff = Path.Combine(output, "diff.png");
        host.Canvas.SavePng(before);
        Assert.True(ImageComparison.Compare(before, before, diff).Matches);

        scene.PointAt(host, "input-Name", click: true);
        host.RenderGuiDialog(scene.Dialog, 0);
        host.Canvas.SavePng(after);

        var comparison = ImageComparison.Compare(before, after, diff);
        Assert.False(comparison.Matches);
        Assert.True(comparison.ChangedPixels > 0);
        Assert.True(comparison.SameDimensions);
        Assert.True(File.Exists(diff));
    }
    [VisualTheory]
    [InlineData("language-default", 1.0)]
    [InlineData("language-focus", 1.0)]
    [InlineData("language-dropdown", 1.0)]
    [InlineData("language-tooltip", 1.0)]
    [InlineData("language-dropdown", 1.25)]
    [InlineData("admin-chat", 1.0)]
    [InlineData("admin-bubbles", 1.0)]
    [InlineData("admin-discord", 1.0)]
    [InlineData("admin-search", 1.0)]
    [InlineData("admin-search", 1.25)]
    [InlineData("admin-search-empty", 1.0)]
    [InlineData("admin-search-none", 1.0)]
    [InlineData("admin-search-target", 1.0)]
    [InlineData("admin-search-target", 1.25)]
    public void Production_dialog_renders_complete_repeatable_frame_without_game(string scenario, double scale)
    {
        var game = Environment.GetEnvironmentVariable("VINTAGE_STORY") ?? throw new InvalidOperationException("VINTAGE_STORY required.");
        var assets = Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!;
        using var host = new PreviewHost(game, assets, 1600, 1000, scale);
        using var scene = PreviewScene.Create(scenario, host);
        host.RenderGuiDialog(scene.Dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
        if (scenario.StartsWith("language-", StringComparison.Ordinal))
            Assert.Equal("Common", ((GuiElementTextInput)scene.Dialog.SingleComposer["input-Name"]).GetText());
        if (scenario == "language-focus") Assert.True(scene.Dialog.SingleComposer["input-Name"].HasFocus);
        if (scenario == "language-dropdown") Assert.True(((GuiElementDropDown)scene.Dialog.SingleComposer["dropdown-Default"]).listMenu.IsOpened);
        var first = host.Canvas.GetRgbaPixels();
        host.RenderGuiDialog(scene.Dialog, 0);
        Assert.Equal(first, host.Canvas.GetRgbaPixels());
        var output = Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT") ?? Path.Combine(AppContext.BaseDirectory, "visual-artifacts");
        Directory.CreateDirectory(output);
        var filename = scenario + "-" + scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        host.Canvas.SavePng(Path.Combine(output, filename + ".png"));
        File.WriteAllText(Path.Combine(output, filename + ".json"), System.Text.Json.JsonSerializer.Serialize(host.Manifest));
        if (scenario == "admin-chat")
        {
            var composer = scene.Dialog.SingleComposer;
            var selector = composer.GetDropDown("dropdown-3");
            var headerY = selector.Bounds.absY;
            var scrollbar = composer.GetScrollbar("settings-scrollbar");
            scrollbar.ScrollToBottom();
            Assert.Equal(headerY, selector.Bounds.absY);
            var elements = (Dictionary<string, GuiElement>)typeof(GuiComposer)
                .GetField("staticElements", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(composer)!;
            Assert.DoesNotContain(elements.Values, element => element is GuiElementStaticText && element.InsideClipBounds != null);
            Assert.Contains(elements.Values, element => element.InsideClipBounds != null && element.Bounds.absY < element.InsideClipBounds.absY);
            host.RenderGuiDialog(scene.Dialog, 0);
            var commands = System.Text.Json.JsonSerializer.SerializeToElement(host.Canvas.Commands);
            Assert.DoesNotContain(commands.EnumerateArray(), command => command.GetProperty("op").GetString() == "texture" &&
                command.GetProperty("height").GetDouble() < 50 && command.GetProperty("y").GetDouble() < composer.Bounds.absY && !command.GetProperty("scissorEnabled").GetBoolean());
            host.Canvas.SavePng(Path.Combine(output, filename + "-scrolled.png"));
        }
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void Search_focus_scroll_navigation_and_session_query_use_production_controls(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        using var scene = PreviewScene.Create("admin-search-target", host);
        var type = typeof(thebasics.ModSystems.ChatUiSystem.ChatUiSystem);
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        void Invoke(string method) => type.GetMethod(method, flags)!.Invoke(null, null);
        var queryField = type.GetField("_configSearchQuery", flags)!;
        queryField.SetValue(null, "proximity");
        Invoke("OpenConfigSearch");
        var search = (GuiDialog)type.GetField("_configSearchDialog", flags)!.GetValue(null)!;
        var input = search.SingleComposer.GetTextInput("query");
        Assert.True(input.HasFocus);
        Assert.Equal("proximity", input.GetText());
        var results = (GuiElementContainer)search.SingleComposer["results"];
        Assert.InRange(results.Bounds.absY - results.InsideClipBounds.absY, -0.1, 0.1);
        var scrollbar = search.SingleComposer.GetScrollbar("scrollbar");
        scrollbar.ScrollToBottom();
        Assert.InRange(results.Elements.Last().Bounds.absY + results.Elements.Last().Bounds.OuterHeight - (results.InsideClipBounds.absY + results.InsideClipBounds.OuterHeight), -1, 1);
        input.SetValue("missing-setting-xyz");
        Assert.False(results.Tabbable);
        Assert.True(input.HasFocus);
        Assert.Equal("missing-setting-xyz", queryField.GetValue(null));
        Assert.Equal(0, scrollbar.CurrentYPosition);
        input.SetValue("a");
        Assert.True(results.Bounds.fixedHeight > 16384);
        var commands = System.Text.Json.JsonSerializer.SerializeToElement(host.Canvas.Commands);
        Assert.All(commands.EnumerateArray().Where(command => command.GetProperty("op").GetString() == "upload"),
            command => Assert.True(command.GetProperty("height").GetInt32() < 16384));
        host.RenderGuiDialog(search, 0);
        input.SetValue("missing-setting-xyz");
        var outsideClick = new MouseEvent(0, 0, EnumMouseButton.Left, 0);
        search.OnMouseDown(outsideClick);
        Assert.True(outsideClick.Handled);
        search.TryClose();
        search.Dispose();
        Invoke("OpenConfigAdminDialog");
        Assert.Equal("missing-setting-xyz", queryField.GetValue(null));
        var settings = (GuiDialog)type.GetField("_configAdminDialog", flags)!.GetValue(null)!;
        var controls = (Dictionary<string, (string Control, double Y)>)typeof(thebasics.ModSystems.ChatUiSystem.ScrollableConfigAdminDialog)
            .GetField("_controls", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(settings)!;
        Assert.True(settings.SingleComposer.GetElement(controls["ProximityChatTabPosition"].Control).HasFocus);
        Invoke("OnConfigAdminDialogClosed");
        Assert.Equal("", queryField.GetValue(null));
        settings.Dispose();

        using var language = new thebasics.ModSystems.ChatUiSystem.LanguageConfigDialog(host.Api,
            new() { new() { OriginalName = "Common", Name = "Common", Color = "#ffffff" }, new() { OriginalName = "Old", Name = "Old", Color = "#ffffff" } }, "", true, _ => { }, () => { }, () => { });
        language.SingleComposer.GetTextInput("input-Name").SetValue("Draft name");
        Assert.True(language.JumpTo("Old", "Description"));
        Assert.True(language.SingleComposer["input-Description"].HasFocus);
        Assert.True(language.JumpTo("Common", "Name"));
        Assert.Equal("Draft name", language.SingleComposer.GetTextInput("input-Name").GetText());

        using var fields = new thebasics.ModSystems.ChatUiSystem.CharacterSheetFieldConfigDialog(host.Api,
            new() { new() { OriginalId = "bio", Id = "bio", Label = "Bio", Type = "string" }, new() { OriginalId = "trade", Id = "trade", Label = "Trade", Type = "select", Options = "Scribe, Glassblower" } }, _ => { }, () => { }, () => { });
        Assert.True(fields.JumpTo("trade", "Options"));
        Assert.True(fields.SingleComposer["input-Options"].HasFocus);
        Assert.Empty(host.UnsupportedCalls);
    }
}
