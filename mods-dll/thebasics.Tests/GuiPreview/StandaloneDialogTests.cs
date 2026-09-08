using TheBasics.GuiPreview;
using Vintagestory.API.Client;
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
    }
}
