using TheBasics.GuiPreview;
using thebasics.ModSystems.SceneDescriptions;
using thebasics.Tests.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

[Collection("Standalone GUI")]
public class SceneDialogLayoutTests
{
    [VisualTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FancyTogglesPreserveSaveValuesAndRespectLockedMarkers(bool locked)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, 1.25);
        SceneDescriptionData? saved = null;
        using var dialog = new SceneDescriptionDialog(host.Api,
            new SceneDescriptionData { Title = "Watchtower", Display = SceneDescriptionDisplay.AlwaysNearby,
                LockItemCode = locked ? "ui" : "", IdleBobbing = false, ShowBodyInBubble = false },
            true, (value, _) => saved = value, () => { }, () => { }, () => { }, _ => { });
        dialog.TryOpen();
        host.RenderGuiDialog(dialog, 0);
        foreach (var key in new[] { "unlimited", "textunlimited", "bobbing", "showbody" })
        {
            var toggle = Assert.IsType<GuiElementSetupToggle>(dialog.SingleComposer[key]);
            Assert.Equal(!locked, toggle.Enabled);
            toggle.OnFocusGained();
            toggle.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Space });
            Assert.Equal(!locked, toggle.Value);
            Assert.InRange(toggle.Bounds.absY, 0, 720 - toggle.Bounds.OuterHeight);
        }
        Assert.False(dialog.SingleComposer.GetNumberInput("distance").Enabled);
        if (!locked)
        {
            dialog.SingleComposer.FocusElement(dialog.SingleComposer["save"].TabIndex);
            dialog.SingleComposer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter }, true);
            Assert.NotNull(saved);
            Assert.True(saved.UnlimitedIconDistance && saved.UnlimitedTextDistance && saved.IdleBobbing && saved.ShowBodyInBubble);
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0, false)]
    [InlineData(1.25, false)]
    [InlineData(1.0, true)]
    [InlineData(1.25, true)]
    public void SmallViewportKeepsTitleFooterAndNearbyControlsOnScreen(double scale, bool nearby)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var dialog = new SceneDescriptionDialog(host.Api,
            new SceneDescriptionData { Title = "Watchtower", Body = "Wind whistles.", Display = nearby ? SceneDescriptionDisplay.AlwaysNearby : SceneDescriptionDisplay.WhenTargeted },
            true, (_, _) => { }, () => { }, () => { }, () => { }, _ => { });
        dialog.TryOpen();
        host.RenderGuiDialog(dialog, 0);
        Assert.True(dialog.SingleComposer.Bounds.absY >= 0);
        foreach (var key in new[] { "title", "body", "size", "bobbing", "save", "color-custom" })
        {
            var bounds = dialog.SingleComposer[key].Bounds;
            Assert.InRange(bounds.absY, 0, 719);
            Assert.True(bounds.absY + bounds.OuterHeight <= 720, key);
        }
        if (nearby)
        {
            var bounds = dialog.SingleComposer["textdistance"].Bounds;
            Assert.True(bounds.absY + bounds.OuterHeight <= dialog.SingleComposer["height"].Bounds.absY);
        }
        Assert.Empty(host.UnsupportedCalls);
    }
}

