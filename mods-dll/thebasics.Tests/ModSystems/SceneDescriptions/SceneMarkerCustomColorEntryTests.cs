using TheBasics.GuiPreview;
using thebasics.Tests.GuiPreview;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

[Collection("Standalone GUI")]
public class SceneMarkerCustomColorEntryTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void MarkerEditorOffersCustomAlongsideAllFivePresets(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        using var scene = PreviewScene.Create("scene-marker-colors", host);
        Assert.NotNull(scene.Dialog.SingleComposer.GetElement("color-custom"));
        for (var index = 0; index < 5; index++)
            Assert.NotNull(scene.Dialog.SingleComposer.GetElement("color-" + index));
        Assert.Empty(host.UnsupportedCalls);
    }
}
