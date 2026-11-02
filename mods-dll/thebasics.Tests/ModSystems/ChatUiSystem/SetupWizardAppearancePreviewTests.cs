using Cairo;
using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardAppearancePreviewTests
{
    [Fact]
    public void TypingKeepsTheNativeInclusiveTimeoutThenBecomesThinking()
    {
        var draft = Draft();
        draft.Set("TypingIndicatorTimeoutSeconds", "2.5");
        Assert.Equal(ChatTypingIndicatorState.Typing, SetupWizardAppearancePreview.TypingState(draft, 2.5));
        Assert.Equal(ChatTypingIndicatorState.ChatOpenComposing, SetupWizardAppearancePreview.TypingState(draft, 2.501));
        Assert.Equal(ChatTypingIndicatorState.ChatOpenComposing, SetupWizardAppearancePreview.TypingState(draft, 4.499));
        Assert.Equal(ChatTypingIndicatorState.None, SetupWizardAppearancePreview.TypingState(draft, 4.5));
        Assert.Equal("Chat closed", SetupWizardAppearancePreview.Message("appearance.typing", draft, 4.5));
        Assert.Equal(5.5, SetupWizardAppearancePreview.DurationSeconds("appearance.typing", draft));
        draft.Set("TypingIndicatorTimeoutSeconds", "0");
        Assert.Equal(ChatTypingIndicatorState.Typing, SetupWizardAppearancePreview.TypingState(draft, 5));
        Assert.Equal(ChatTypingIndicatorState.ChatOpenComposing, SetupWizardAppearancePreview.TypingState(draft, 5.001));
        draft.Set("TypingIndicatorMaxRange", "0");
        Assert.Equal(ChatTypingIndicatorState.None, SetupWizardAppearancePreview.TypingState(draft, 0));
        draft.Set("TypingIndicatorMaxRange", "30");
        draft.Set("EnableTypingIndicator", "0");
        Assert.Equal(ChatTypingIndicatorState.None, SetupWizardAppearancePreview.TypingState(draft, 0));
    }

    [Fact]
    public void BubbleLifetimeUsesVisibleCharactersAndOnlyCustomBubblesUseTheMinimum()
    {
        var draft = Draft();
        draft.Set("OverheadChatBubbleMode", "RpText");
        draft.Set("SpeechBubbleMinimumDisplayMilliseconds", "12000");
        Assert.Equal(12, SetupWizardAppearancePreview.BubbleDuration(draft));
        Assert.True(SetupWizardAppearancePreview.BubbleVisible(draft, 12));
        Assert.False(SetupWizardAppearancePreview.BubbleVisible(draft, 12.001));
        Assert.Equal(13, SetupWizardAppearancePreview.DurationSeconds("appearance.bubbles", draft));
        draft.Set("OverheadChatBubbleMode", "Vanilla");
        Assert.Equal(4.1, SetupWizardAppearancePreview.BubbleDuration(draft), 5);
        draft.Set("OverheadChatBubbleMode", "RpText");
        draft.Set("DisableRPChat", "1");
        Assert.Equal(4.1, SetupWizardAppearancePreview.BubbleDuration(draft), 5);
        draft.Set("OverheadChatBubbleMode", "Off");
        Assert.False(SetupWizardAppearancePreview.BubbleVisible(draft, 0));
    }

    [Fact]
    public void NametagUsesTheNativeSingleLineAndStrictRangeTargetAndSightGates()
    {
        var draft = Draft();
        draft.Set("NametagRenderRange", "8");
        draft.Set("HideNametagUnlessTargeting", "1");
        draft.Set("NametagRequiresLineOfSight", "1");
        Assert.Equal("Pip (PipPlayer)", SetupWizardAppearancePreview.Name(draft));
        Assert.True(SetupWizardAppearancePreview.NameplateVisible(draft, 7.99, true, true));
        Assert.False(SetupWizardAppearancePreview.NameplateVisible(draft, 8, true, true));
        Assert.False(SetupWizardAppearancePreview.NameplateVisible(draft, 3, false, true));
        Assert.False(SetupWizardAppearancePreview.NameplateVisible(draft, 3, true, false));
        draft.Set("NametagRequiresLineOfSight", "0");
        Assert.True(SetupWizardAppearancePreview.NameplateVisible(draft, 3, true, false));
        draft.Set("ShowNicknameInNametag", "0");
        Assert.Equal("PipPlayer", SetupWizardAppearancePreview.Name(draft));
        draft.Set("ShowPlayerNameInNametag", "0");
        Assert.Equal("", SetupWizardAppearancePreview.Name(draft));
        Assert.False(SetupWizardAppearancePreview.NameplateVisible(draft, 3, true, true));
    }

    [VisualTheory]
    [InlineData(270, 1)]
    [InlineData(310, 1.25)]
    public void NativeArtworkAndLifecycleFramesAreDeterministicWithoutEffects(int height, double scale)
    {
        using var host = CreateHost(scale);
        var draft = Draft();
        var original = draft.Values.Select(value => (value.Key, value.Value)).ToArray();
        foreach (var page in new[] { "appearance.typing", "appearance.bubbles", "appearance.names", "appearance.nametagcolors" })
        {
            using var first = Render(host, draft, page, .5, height, scale);
            using var repeated = Render(host, draft, page, .5, height, scale);
            Assert.Equal(first.Data, repeated.Data);
            Assert.Contains(first.Data.Where((_, index) => index % 4 == 3), alpha => alpha > 0);
            Assert.Contains(Overhead(first, height, scale).Where((_, index) => index % 4 == 3), alpha => alpha > 0);
            Assert.All(first.Data[..first.Stride].Where((_, index) => index % 4 == 3), alpha => Assert.Equal(0, alpha));
        }
        using var typing = Render(host, draft, "appearance.typing", 1, height, scale);
        using var thinking = Render(host, draft, "appearance.typing", 5.1, height, scale);
        Assert.False(Overhead(typing, height, scale).SequenceEqual(Overhead(thinking, height, scale)));
        using var bubble = Render(host, draft, "appearance.bubbles", .5, height, scale);
        using var expired = Render(host, draft, "appearance.bubbles", 5, height, scale);
        Assert.Contains(Overhead(bubble, height, scale).Where((_, index) => index % 4 == 3), alpha => alpha > 0);
        Assert.All(Overhead(expired, height, scale).Where((_, index) => index % 4 == 3), alpha => Assert.Equal(0, alpha));
        draft.Set("HideNametagUnlessTargeting", "1");
        using var targeted = Render(host, draft, "appearance.names", .5, height, scale);
        using var untargeted = Render(host, draft, "appearance.names", 3.5, height, scale);
        Assert.Contains(Overhead(targeted, height, scale).Where((_, index) => index % 4 == 3), alpha => alpha > 0);
        Assert.All(Overhead(untargeted, height, scale).Where((_, index) => index % 4 == 3), alpha => Assert.Equal(0, alpha));
        draft.Set("HideNametagUnlessTargeting", original.Single(value => value.Key == "HideNametagUnlessTargeting").Value);
        Assert.Equal(original, draft.Values.Select(value => (value.Key, value.Value)).ToArray());
        Assert.False(draft.IsSavePending);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
        Assert.DoesNotContain(host.UsedCalls, call => call.Contains("SendChatMessage", StringComparison.Ordinal));
    }

    [VisualTheory]
    [InlineData(1d)]
    public void TypingAndNameplateUseTheProductionArtworkAndStyle(double scale)
    {
        using var host = CreateHost(scale);
        var draft = Draft();
        draft.Set("TypingIndicatorDisplayMode", "Both");
        draft.Set("TypingIndicatorTextOverride", "Writing...");
        var label = SetupWizardAppearancePreview.TypingLabel(draft, 0);
        Assert.Contains("<icon name=\"paintbrush\">", label);
        Assert.Contains("Writing...", label);
        Assert.Contains("Thinking...", SetupWizardAppearancePreview.TypingLabel(draft, 5.1));
        Assert.DoesNotContain("Writing...", SetupWizardAppearancePreview.TypingLabel(draft, 5.1));
        var background = TypingIndicatorRenderer.CreateBackground();
        Assert.Equal(2, background.BorderWidth);
        Assert.Equal(5, background.HorPadding);
        Assert.Equal(3, background.VerPadding);
        Assert.Equal(.85, background.FillColor[3]);
        draft.Set("TypingIndicatorDisplayMode", "Icon");
        Assert.DoesNotContain("Writing...", SetupWizardAppearancePreview.TypingLabel(draft, 0));
        draft.Set("TypingIndicatorDisplayMode", "Text");
        Assert.DoesNotContain("<icon", SetupWizardAppearancePreview.TypingLabel(draft, 0));
        draft.Set("TypingIndicatorTextOverride", "A long label that the native text-only indicator keeps on one line");
        using var wide = SetupWizardAppearancePreview.TypingSurface(host.Api, draft, 0);
        Assert.NotNull(wide);
        Assert.True(wide.Width > 180);
        Assert.True(wide.Width > wide.Height * 4);
        draft.Set("NametagBackgroundColor", "#29323C");
        draft.Set("NametagBorderColor", "#AABBCC");
        using var colored = Render(host, draft, "appearance.nametagcolors", 0, 270, 1);
        draft.Set("NametagBorderColor", " #AABBCC ");
        using var paddedColor = Render(host, draft, "appearance.nametagcolors", 0, 270, 1);
        Assert.Equal(colored.Data, paddedColor.Data);
        draft.Set("NametagBorderColor", "#CC5544");
        using var changed = Render(host, draft, "appearance.nametagcolors", 0, 270, 1);
        Assert.False(colored.Data.SequenceEqual(changed.Data));
        draft.Set("NametagBorderColor", "invalid");
        using var invalidColor = Render(host, draft, "appearance.nametagcolors", 0, 270, 1);
        draft.Set("NametagBorderColor", "");
        using var themeColor = Render(host, draft, "appearance.nametagcolors", 0, 270, 1);
        Assert.Equal(invalidColor.Data, themeColor.Data);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(double.MaxValue)]
    public void ArbitraryTimeStaysWithinTheLocalExample(double time)
    {
        var draft = Draft();
        Assert.True(SetupWizardAppearancePreview.DurationSeconds("appearance.typing", draft) > 0);
        Assert.True(SetupWizardAppearancePreview.TypingState(draft, time) is ChatTypingIndicatorState.None or ChatTypingIndicatorState.Typing or ChatTypingIndicatorState.ChatOpenComposing);
        Assert.NotEmpty(SetupWizardAppearancePreview.Message("appearance.names", draft, time));
    }

    [VisualTheory]
    [InlineData("appearance.typing")]
    [InlineData("appearance.bubbles")]
    [InlineData("appearance.names")]
    public void OverheadArtworkMatchesNativeSurfaceSizeAtEachGuiScale(string page)
    {
        var draft = Draft();
        foreach (double scale in new[] { 1, 1.25 })
        {
            using var host = CreateHost(scale);
            using var image = Render(host, draft, page, .5, 270, scale);
            using var native = page switch
            {
                "appearance.typing" => SetupWizardAppearancePreview.TypingSurface(host.Api, draft, .5),
                "appearance.bubbles" => SetupWizardAppearancePreview.BubbleSurface(host.Api, draft),
                _ => SetupWizardAppearancePreview.NameplateSurface(host.Api, draft)
            };
            Assert.NotNull(native);
            var pixels = image.Data;
            int left = image.Width, right = 0, top = image.Height, bottom = 0;
            for (int y = (int)(45 * scale); y < (int)(125 * scale); y++)
                for (int x = 0; x < image.Width; x++)
                    if (pixels[y * image.Stride + x * 4 + 3] > 32)
                    { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
            Assert.True(right > left && bottom > top);
            // Native font hinting changes glyph widths between scales. The scene must retain that renderer's size.
            Assert.InRange(Math.Abs((right - left + 1) / scale - native.Width / scale), 0, 3);
            Assert.InRange(Math.Abs((bottom - top + 1) / scale - native.Height / scale), 0, 3);
            Assert.Empty(host.UnsupportedCalls);
            Assert.Empty(host.RecordedEffects);
        }
    }

    private static SetupWizardDraft Draft() => new(SetupWizardCaptureScenes.DefaultValues());
    private static PreviewHost CreateHost(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
    private static ImageSurface Render(PreviewHost host, SetupWizardDraft draft, string page, double time, int height, double scale)
    {
        var surface = new ImageSurface(Format.Argb32, (int)Math.Ceiling(310 * scale), (int)Math.Ceiling(height * scale));
        using var context = new Context(surface);
        SetupWizardAppearancePreview.Draw(context, surface, ElementBounds.FixedSize(310, height), page, draft, time, host.Api);
        surface.Flush();
        return surface;
    }
    private static byte[] Overhead(ImageSurface surface, int height, double scale)
    {
        var offset = (height - 270) / 2d;
        return surface.Data[((int)Math.Ceiling((offset + 45) * scale) * surface.Stride)..((int)Math.Floor((offset + 125) * scale) * surface.Stride)];
    }
}
