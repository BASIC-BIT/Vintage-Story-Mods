using System.Reflection;
using NSubstitute;
using TheBasics.GuiPreview;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardPlaybackTests
{
    [VisualTheory]
    [InlineData(1d)]
    public void ReplayResetsTheGuidePoseBeforeResumingItsAnimation(double scale)
    {
        using var host = Host(1600, 1000, scale);
        var guide = Substitute.For<ISetupGuidePreview>();
        using var wizard = new SetupWizardDialog(host.Api,
            new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues()), "replay-guide-test", true,
            null!, null!, null!, layoutOnly: true, actorFactory: (_, _) => guide);
        wizard.ShowPage("appearance.typing");
        wizard.TryOpen();
        wizard.SetPreviewTime(2);
        guide.ClearReceivedCalls();

        Click(host, wizard, "preview-replay");

        Received.InOrder(() =>
        {
            guide.SetAnimation("idle", 0);
            guide.PlayAnimation("idle");
        });
        Assert.Equal(0, Time(wizard));
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void ReplayPauseAndResumeOnlyControlTheSampleClock(double scale)
    {
        using var host = Host(1600, 1000, scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var original = draft.Values.Select(value => (value.Key, value.Value)).ToArray();
        using var wizard = Wizard(host, draft);
        wizard.ShowPage("chat.ranges");
        wizard.TryOpen();
        host.RenderGuiDialog(wizard, 0.75f);
        Assert.Equal(0.75, Time(wizard));
        Click(host, wizard, "preview-pause");
        host.RenderGuiDialog(wizard, 2);
        Assert.Equal(0.75, Time(wizard));
        Click(host, wizard, "preview-pause");
        host.RenderGuiDialog(wizard, 0.5f);
        Assert.Equal(1.25, Time(wizard));
        Click(host, wizard, "preview-replay");
        Assert.Equal(0, Time(wizard));
        host.RenderGuiDialog(wizard, 0.25f);
        Assert.Equal(0.25, Time(wizard));
        Assert.Equal(original, draft.Values.Select(value => (value.Key, value.Value)));
        Assert.False(draft.IsSavePending);
        Assert.Empty(host.UnsupportedCalls);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void ScrubbingCanReachTheEndOfARealFiveMinuteWarmup(double scale)
    {
        using var host = Host(1600, 1000, scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("Teleportation.RegisterStuckCommand", "1");
        draft.Set("Teleportation.StuckWarmupSeconds", "300");
        using var wizard = Wizard(host, draft);
        wizard.ShowPage("teleport.stuck");
        wizard.TryOpen();
        Assert.Equal(304, SetupWizardTravelPreview.DurationSeconds("teleport.stuck", draft));
        var slider = wizard.SingleComposer.GetSlider("preview-time");
        var x = (int)(slider.Bounds.renderX + slider.Bounds.OuterWidth - 2);
        var y = (int)(slider.Bounds.renderY + slider.Bounds.OuterHeight / 2);
        host.SetMouse(x, y);
        wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.InRange(Time(wizard), 300.5, 304);
        var pausedAt = Time(wizard);
        host.RenderGuiDialog(wizard, 1);
        Assert.Equal(pausedAt, Time(wizard));
        Assert.Contains("teleported to spawn", SetupWizardTravelPreview.Message("teleport.stuck", draft, pausedAt));
        typeof(SetupWizardDialog).GetMethod("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(wizard, ["Teleportation.StuckWarmupSeconds", "0", "edited", false]);
        Assert.InRange(Time(wizard), 0, 4);
        Assert.Equal("0", draft.Get("Teleportation.StuckWarmupSeconds"));
        var rebased = Time(wizard);
        host.RenderGuiDialog(wizard, 1);
        Assert.Equal(rebased, Time(wizard));
        Click(host, wizard, "preview-replay");
        Assert.Equal(0, Time(wizard));
        Assert.Empty(host.UnsupportedCalls);
        Assert.Throws<ArgumentOutOfRangeException>(() => wizard.SetPreviewTime(61));
    }

    [Fact]
    public void RequestTimeoutCanExpireAfterTwentyFourHoursWithoutWrappingTheClock()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("AllowPlayerTpa", "1");
        draft.Set("TpaUseTimeout", "1");
        draft.Set("TpaTimeoutMinutes", "1440");
        Assert.Equal(86403, SetupWizardTravelPreview.DurationSeconds("teleport.requesttiming", draft));
        Assert.Contains("expired", SetupWizardTravelPreview.Message("teleport.requesttiming", draft, 86400));
        var clock = (string)typeof(SetupWizardDialog).GetMethod("Clock", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [86403d])!;
        Assert.Equal("24:00:03", clock);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void EditingDurationRefreshesTheSliderWhileTheSameSecondIsPaused(double scale)
    {
        using var host = Host(1600, 1000, scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("TypingIndicatorTimeoutSeconds", "5");
        using var wizard = Wizard(host, draft);
        wizard.ShowPage("appearance.typing");
        wizard.TryOpen();
        host.RenderGuiDialog(wizard, 1);
        Click(host, wizard, "preview-pause");
        var slider = wizard.SingleComposer.GetSlider("preview-time");
        var before = slider.GetValue();
        typeof(SetupWizardDialog).GetMethod("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(wizard, ["TypingIndicatorTimeoutSeconds", "10", "edited", false]);
        Assert.Equal(1, Time(wizard));
        Assert.True(slider.GetValue() < before);
        host.RenderGuiDialog(wizard, 1);
        Assert.Equal(1, Time(wizard));
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void RedrawReusesItsTextureAndDisposalReleasesIt(double scale)
    {
        using var host = Host(1600, 1000, scale);
        var draw = new GuiElementSetupPreviewDraw(host.Api, ElementBounds.Fixed(0, 0, 310, 130), (context, _, _) =>
        {
            context.SetSourceRGBA(0.2, 0.4, 0.5, 1);
            context.Paint();
        });
        var composer = host.Api.Gui.CreateCompo("owned-preview-draw-test", ElementBounds.Fixed(20, 20, 310, 130))
            .AddInteractiveElement(draw, "drawing").Compose(focusFirstElement: false);
        try
        {
            var textureId = draw.TextureId;
            Assert.True(textureId > 0);
            for (var frame = 0; frame < 5; frame++) draw.Redraw();
            Assert.Equal(textureId, draw.TextureId);
            composer.Dispose();
            Assert.Equal(0, draw.TextureId);
            draw.Redraw();
            Assert.Equal(0, draw.TextureId);
            Assert.Empty(host.UnsupportedCalls);
        }
        finally { composer.Dispose(); }
    }

    [VisualTheory]
    [InlineData(1600, 1000, 1d)]
    [InlineData(1280, 720, 1.25)]
    public void PlaybackFitsEveryAnimatedPageAndIsAbsentFromPlainSettings(int width, int height, double scale)
    {
        using var host = Host(width, height, scale);
        using var wizard = Wizard(host, new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues()));
        wizard.TryOpen();
        foreach (var page in new[] { "chat.tabs", "chat.ranges", "appearance.bubbles", "appearance.typing",
            "appearance.names", "teleport.stuck", "characters.slots", "notifications.sleep" })
        {
            wizard.ShowPage(page);
            host.RenderGuiDialog(wizard, 0);
            foreach (var key in new[] { "preview-replay", "preview-pause", "preview-time", "preview-clock" })
            {
                var bounds = wizard.SingleComposer[key].Bounds;
                Assert.InRange(bounds.renderX, 0, width);
                Assert.InRange(bounds.renderY, 0, height);
                Assert.True(bounds.renderX + bounds.OuterWidth <= width);
                Assert.True(bounds.renderY + bounds.OuterHeight < wizard.SingleComposer["close"].Bounds.renderY);
            }
            var clock = wizard.SingleComposer["preview-clock"].Bounds;
            var track = wizard.SingleComposer["preview-time"].Bounds;
            Assert.True(clock.renderX > track.renderX + track.OuterWidth);
            Assert.False(wizard.SingleComposer.GetSlider("preview-time").ShowTextWhenResting);
            foreach (var key in SetupWizardCatalog.Pages.Single(item => item.Id == page).SettingKeys)
            {
                var setting = wizard.SingleComposer.GetElement(key);
                if (setting == null) continue;
                var bounds = setting.Bounds;
                Assert.True(clock.renderX + clock.OuterWidth <= bounds.renderX || bounds.renderX + bounds.OuterWidth <= clock.renderX ||
                    clock.renderY + clock.OuterHeight <= bounds.renderY || bounds.renderY + bounds.OuterHeight <= clock.renderY,
                    page + ": preview clock overlaps " + key);
            }
        }
        wizard.ShowPage("characters.access");
        Assert.Null(wizard.SingleComposer.GetElement("preview-time"));
        wizard.ShowPage("server.retention");
        Assert.Null(wizard.SingleComposer.GetElement("preview-replay"));
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void AutomaticChatEventsDoNotReplaceOrUnfocusSettings(double scale)
    {
        using var host = Host(1600, 1000, scale);
        using var wizard = Wizard(host, new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues()));
        wizard.ShowPage("chat.tabs");
        wizard.TryOpen();
        var composer = wizard.SingleComposer;
        var setting = composer["PreserveDefaultChatChoice"];
        composer.FocusElement(setting.TabIndex);
        Assert.True(setting.HasFocus);
        foreach (var time in new[] { 1.25, 2.25, 4.25, 6.25, 7.25 })
        {
            wizard.SetPreviewTime(time);
            host.RenderGuiDialog(wizard, 0);
            Assert.Same(composer, wizard.SingleComposer);
            Assert.Same(setting, composer["PreserveDefaultChatChoice"]);
            Assert.True(setting.HasFocus);
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost Host(int width, int height, double scale) => new(
        Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, width, height, scale);
    private static SetupWizardDialog Wizard(PreviewHost host, SetupWizardDraft draft) =>
        new(host.Api, draft, "playback-test", true, null!, null!, null!, layoutOnly: true, actorFactory: host.CreateGuide);
    private static double Time(SetupWizardDialog wizard) =>
        (double)typeof(SetupWizardDialog).GetProperty("Time", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wizard)!;
    private static void Click(PreviewHost host, SetupWizardDialog wizard, string key)
    {
        var bounds = wizard.SingleComposer[key].Bounds;
        var x = (int)(bounds.renderX + bounds.OuterWidth / 2);
        var y = (int)(bounds.renderY + bounds.OuterHeight / 2);
        host.SetMouse(x, y);
        wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
}
