using Cairo;
using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.SceneDescriptions;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardNativeArtworkPreviewTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void SceneMarkerToggleHidesNativeArtworkAndKeepsTheGround(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        Assert.True(SetupWizardFeaturePreview.Supports("roleplay.scenemarkers"));
        Assert.False(SetupWizardFeaturePreview.IsSettingsOnly("roleplay.scenemarkers"));
        draft.Set("EnableSceneMarkers", "1");
        using var visible = Render(host, draft, "roleplay.scenemarkers", scale);
        using var repeated = Render(host, draft, "roleplay.scenemarkers", scale);
        Assert.Equal(visible.Data, repeated.Data);
        draft.Set("EnableSceneMarkers", "0");
        using var hidden = Render(host, draft, "roleplay.scenemarkers", scale);
        Assert.False(visible.Data.SequenceEqual(hidden.Data));
        Assert.Equal(Region(visible, 180, 220, scale), Region(hidden, 180, 220, scale));
        var sample = new SceneDescriptionData
        {
            Title = "Old campsite", Body = "Ashes are cold. Someone left in a hurry.",
            ShowBodyInBubble = true, Display = SceneDescriptionDisplay.AlwaysNearby, Symbol = SceneMarkerSymbol.Information
        };
        using (var description = SceneMarkerVisuals.DescriptionSurface(host.Api, sample))
        using (var context = new Context(hidden))
        {
            Assert.NotNull(description);
            var pictureScale = Math.Min(hidden.Width / 310d, hidden.Height / 220d);
            context.Translate((hidden.Width - 310 * pictureScale) / 2, (hidden.Height - 220 * pictureScale) / 2);
            context.Scale(pictureScale, pictureScale);
            var fit = Math.Min(286d / description.Width, 100d / description.Height);
            context.Save();
            context.Translate(12 + (286 - description.Width * fit) / 2, 18 + (100 - description.Height * fit) / 2);
            context.Scale(fit, fit);
            context.SetSourceSurface(description, 0, 0);
            context.Paint();
            context.Restore();
            SceneMarkerVisuals.Draw(context, SceneMarkerVisuals.LoadShape(host.Api, sample.Symbol),
                133, 127, 44, sample.Color, sample.Symbol, SceneMarkerVisuals.IndicatorOpacity);
        }
        hidden.Flush();
        Assert.Equal(visible.Data, hidden.Data);
        Assert.Contains(host.AssetFiles, path => path.EndsWith("scene-symbol-information.json", StringComparison.Ordinal));
        Assert.Empty(host.UnsupportedCalls);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
        Assert.Empty(host.RecordedEffects);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void NativeArtworkPagesFitTheWizardWithoutWorldOperations(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("EnableSceneMarkers", "1");
        draft.Set("EnableDiceRolling", "1");
        using var wizard = new SetupWizardDialog(host.Api, draft, "artwork-preview", true,
            null!, null!, null!, layoutOnly: true, actorFactory: host.CreateGuide);
        wizard.TryOpen();
        foreach (var page in new[] { "roleplay.scenemarkers", "roleplay.dice" })
        {
            wizard.ShowPage(page);
            host.RenderGuiDialog(wizard, 0);
            var bounds = Assert.IsType<GuiElementSetupPreviewDraw>(wizard.SingleComposer["preview-diagram"]).Bounds;
            Assert.InRange(bounds.renderX, 0, host.Canvas.Width - bounds.OuterWidth);
            Assert.InRange(bounds.renderY, 0, host.Canvas.Height - bounds.OuterHeight);
        }
        Assert.Empty(host.UnsupportedCalls);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void DicePreviewUsesTheNativePurpleResultBubble(double scale)
    {
        using var host = CreateHost(scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("EnableDiceRolling", "1");
        using var visible = Render(host, draft, "roleplay.dice", scale);
        bool purple = false;
        var pixels = visible.Data;
        for (var offset = 0; offset < pixels.Length; offset += 4)
            purple |= pixels[offset] > pixels[offset + 1] + 20 && pixels[offset + 2] > pixels[offset + 1] + 20;
        Assert.True(purple, "The result must use the shared purple dice artwork.");
        draft.Set("EnableDiceRolling", "0");
        using var hidden = Render(host, draft, "roleplay.dice", scale);
        Assert.False(visible.Data.SequenceEqual(hidden.Data));
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    private static PreviewHost CreateHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);

    private static ImageSurface Render(PreviewHost host, SetupWizardDraft draft, string page, double scale)
    {
        var surface = new ImageSurface(Format.Argb32, (int)Math.Ceiling(310 * scale), (int)Math.Ceiling(220 * scale));
        using var context = new Context(surface);
        SetupWizardFeaturePreview.Draw(context, surface, ElementBounds.FixedSize(310, 220), page, draft, host.Api);
        surface.Flush();
        return surface;
    }

    private static byte[] Region(ImageSurface surface, double top, double bottom, double scale)
    {
        var start = (int)Math.Ceiling(top * scale) * surface.Stride;
        var end = Math.Min(surface.Data.Length, (int)Math.Floor(bottom * scale) * surface.Stride);
        return surface.Data[start..end];
    }
}
