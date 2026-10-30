using System.Text.Json;
using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class SeraphSceneTests
{
    [VisualTheory]
    [InlineData("wizard-hub", 1)]
    [InlineData("wizard-appearance-typing-thinking", 1)]
    [InlineData("wizard-chat-signing-visible", 2)]
    [InlineData("wizard-travel-damage-continues", 1)]
    [InlineData("wizard-characters-autoopen", 1)]
    [InlineData("wizard-characters-slots-second", 1)]
    [InlineData("wizard-notifications-sleep", 4)]
    public void ProductionScenesRenderActualClothedMeshesWithoutOmittedActors(string scenario, int count)
    {
        using var host = Host();
        using var scene = PreviewScene.Create(scenario, host);
        host.RenderGuiDialog(scene.Dialog, 0);
        var draws = Draws(host);
        Assert.Equal(count, draws.Length);
        Assert.All(draws, draw => Assert.True(draw.GetProperty("vertices").GetInt32() > 280));
        Assert.Equal("standalone-gui-and-seraph", scene.Coverage);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    [VisualTheory]
    [InlineData(1d)]
    public void TravelSequenceKeepsRealActorsAcrossWarmupFadeArrivalAndReplay(double scale)
    {
        using var host = Host();
        using var scene = PreviewScene.Create("wizard-travel-damage-continues", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        var output = Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT");
        var frames = string.IsNullOrEmpty(output) ? null : Path.Combine(output, "seraph-travel-motion");
        if (frames != null) Directory.CreateDirectory(frames);
        var poses = new HashSet<string>();
        byte[]? first = null;
        for (int frame = 0; frame < 80; frame++)
        {
            wizard.SetPreviewTime(frame / 10d);
            host.RenderGuiDialog(wizard, 0);
            var draws = Draws(host);
            Assert.NotEmpty(draws);
            foreach (var draw in draws) poses.Add(draw.GetProperty("animation").GetString()!);
            if (frame == 0) first = host.Canvas.GetRgbaPixels();
            if (frames != null) host.Canvas.SavePng(Path.Combine(frames, $"frame{frame:000}.png"));
        }
        Assert.Contains("idle", poses);
        Assert.Contains("wave", poses);
        wizard.SetPreviewTime(0);
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal(first, host.Canvas.GetRgbaPixels());
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    private static JsonElement[] Draws(PreviewHost host) => host.Canvas.Commands.Select(command => JsonSerializer.SerializeToElement(command))
        .Where(command => command.GetProperty("op").GetString() == "seraph").ToArray();
    private static PreviewHost Host() => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, 1);
}
