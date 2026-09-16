using TheBasics.GuiPreview;
using Xunit;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class DicePreviewTests
{
    [VisualTheory]
    [InlineData("dice-normal")]
    [InlineData("dice-whisper")]
    [InlineData("dice-yell")]
    [InlineData("dice-complex")]
    public void Production_dice_fixture_renders_repeatably_and_exports_image(string scenario)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 600, 240);
        var fixture = DicePreview.Render(scenario, host);
        var first = host.Canvas.GetRgbaPixels();
        DicePreview.Render(scenario, host);
        Assert.Equal(first, host.Canvas.GetRgbaPixels());
        Assert.Empty(host.UnsupportedCalls);
        var output = Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT") ?? Path.Combine(AppContext.BaseDirectory, "visual-artifacts");
        Directory.CreateDirectory(output);
        host.Canvas.SavePng(Path.Combine(output, scenario + ".png"));
        File.WriteAllText(Path.Combine(output, scenario + ".json"),
            System.Text.Json.JsonSerializer.Serialize(new { fixture, environment = host.Manifest }));
    }
}
