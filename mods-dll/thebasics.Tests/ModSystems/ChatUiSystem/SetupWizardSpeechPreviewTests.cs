using Cairo;
using TheBasics.GuiPreview;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardSpeechPreviewTests
{
    [Fact]
    public void ListenerPhasesKeepDeliveryAndFadingDistancesDistinct()
    {
        Assert.Equal(new(0, 3, 0), SetupWizardSpeechPreview.Listener(0));
        Assert.Equal(SetupWizardSpeechPreview.Listener(0), SetupWizardSpeechPreview.Listener(12));
        Assert.Equal(SetupWizardSpeechPreview.Listener(0), SetupWizardSpeechPreview.Listener(double.NaN));
        var far = SetupWizardSpeechPreview.Listener(5);
        Assert.Equal(28, far.GridDistance);
        Assert.Equal(20, far.StraightDistance);
        Assert.Equal("20 blocks in a straight line", SetupWizardSpeechPreview.DistanceLabel("chat.obfuscation", far));
        Assert.Equal("28 blocks away", SetupWizardSpeechPreview.DistanceLabel("chat.ranges", far));
        Assert.Equal("28 blocks away", SetupWizardSpeechPreview.DistanceLabel("chat.signlanguage", far));
        Assert.False(far.BehindWall);
        Assert.True(SetupWizardSpeechPreview.Listener(8).BehindWall);
        Assert.True(SetupWizardSpeechPreview.HasMessage("chat.language"));
        Assert.False(SetupWizardSpeechPreview.HasScene("chat.language"));
        Assert.False(SetupWizardSpeechPreview.HasMessage("chat.basics"));
    }

    [Theory]
    [InlineData(ProximityChatMode.Whisper)]
    [InlineData(ProximityChatMode.Normal)]
    [InlineData(ProximityChatMode.Yell)]
    public void HearingUsesTheStrictGridBoundaryForEveryMode(ProximityChatMode mode)
    {
        var draft = Draft();
        var far = SetupWizardSpeechPreview.Listener(5);
        draft.Set("ProximityChatModeDistances." + mode, "28");
        Assert.False(SetupWizardSpeechPreview.Receives("chat.ranges", draft, mode, far));
        draft.Set("ProximityChatModeDistances." + mode, "29");
        Assert.True(SetupWizardSpeechPreview.Receives("chat.ranges", draft, mode, far));
        draft.Set("ProximityChatModeDistances." + mode, "0");
        Assert.False(SetupWizardSpeechPreview.Receives("chat.ranges", draft, mode, far));
    }

    [Fact]
    public void ObfuscationUsesStraightDistanceAndTheActualMarkupSafeHelper()
    {
        var draft = Draft();
        var far = SetupWizardSpeechPreview.Listener(5);
        Assert.Equal(0.25, SetupWizardSpeechPreview.ObscuredFraction(draft, ProximityChatMode.Normal, far));
        var obscured = SetupWizardSpeechPreview.Message("chat.obfuscation", draft, 5);
        Assert.Contains('*', obscured);
        Assert.Contains("yells", obscured);
        Assert.DoesNotContain("whispers", obscured);
        Assert.Equal(obscured, SetupWizardSpeechPreview.Message("chat.obfuscation", draft, 5));
        draft.Set("EnableDistanceObfuscationSystem", "0");
        Assert.DoesNotContain('*', SetupWizardSpeechPreview.Message("chat.obfuscation", draft, 5));
        draft.Set("EnableDistanceObfuscationSystem", "1");
        foreach (var mode in new[] { "Whisper", "Normal", "Yell" }) draft.Set("ProximityChatModeDistances." + mode, "-1");
        var unlimited = SetupWizardSpeechPreview.Message("chat.obfuscation", draft, 5);
        Assert.DoesNotContain('*', unlimited);
        Assert.Contains("whispers", unlimited);
        Assert.Contains("says", unlimited);
        Assert.Contains("yells", unlimited);
    }

    [Fact]
    public void SigningNeedsRangeSightAndEnabledRoleplayLanguages()
    {
        var draft = Draft();
        draft.Set("SignLanguageRange", "8");
        draft.Set("RequireLineOfSightForSignLanguage", "0");
        Assert.Empty(SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 8));
        draft.Set("SignLanguageRange", "9");
        Assert.Contains("signs", SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 8));
        draft.Set("RequireLineOfSightForSignLanguage", "1");
        Assert.Empty(SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 8));
        Assert.Contains("signs", SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 0));
        draft.Set("SignLanguageRange", "-1");
        Assert.False(SetupWizardSpeechPreview.Receives("chat.signlanguage", draft, ProximityChatMode.Normal, new(1, 80, 0)));
        draft.Set("EnableLanguageSystem", "0");
        Assert.Empty(SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 0));
        draft.Set("EnableLanguageSystem", "1");
        draft.Set("DisableRPChat", "1");
        Assert.Empty(SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 0));
    }

    [Fact]
    public void DeliveredSigningUsesTheIndependentSpeechObfuscationSettings()
    {
        var draft = Draft();
        draft.Set("SignLanguageRange", "29");
        var far = SetupWizardSpeechPreview.Listener(5);
        Assert.True(SetupWizardSpeechPreview.Receives("chat.signlanguage", draft, ProximityChatMode.Normal, far));
        Assert.Equal(0.25, SetupWizardSpeechPreview.ObscuredFraction(draft, ProximityChatMode.Normal, far));
        string obscured = SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 5);
        Assert.Contains("signs", obscured);
        Assert.Contains('*', obscured);
        draft.Set("EnableDistanceObfuscationSystem", "0");
        string clear = SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 5);
        Assert.Contains("Meet me by the gate.", clear);
        Assert.DoesNotContain('*', clear);
        draft.Set("EnableDistanceObfuscationSystem", "1");
        draft.Set("ProximityChatModeDistances.Normal", "-1");
        Assert.DoesNotContain('*', SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 5));
        draft.Set("SignLanguageRange", "28");
        Assert.Empty(SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 5));
    }

    [Fact]
    public void LanguageToggleShowsKnownSpeechAndActualUnknownSyllables()
    {
        var draft = Draft();
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var trade = config.Languages.Single(language => language.Name == "Tradeband");
        string syllables = LanguageScrambler.ScrambleMessage("Meet me by the gate.", trade);
        string languages = SetupWizardSpeechPreview.Message("chat.language", draft, 0);
        Assert.Contains("Meet me by the gate.", languages);
        Assert.Contains("<i>" + syllables + "</i>", languages);
        Assert.Contains(trade.Color, languages);
        draft.Set("EnableLanguageSystem", "0");
        string disabled = SetupWizardSpeechPreview.Message("chat.language", draft, 0);
        Assert.DoesNotContain(syllables, disabled);
        Assert.DoesNotContain(trade.Color, disabled);
        Assert.Equal(2, disabled.Split("Meet me by the gate.").Length - 1);
    }

    [Fact]
    public void LanguageExampleUsesTheConfiguredServerLanguageAndReflectsDraftEdits()
    {
        var languages = new[]
        {
            new LanguageConfigEntryMessage
            {
                Name = "Hillspeech", Description = "Spoken in the highlands.", Prefix = "hill",
                Syllables = "or, an", Color = "#F3CD97", Default = true
            },
            new LanguageConfigEntryMessage
            {
                Name = "River Cant", Description = "Spoken along the rivers.", Prefix = "river",
                Syllables = "sha, vri, ko", Color = "#67C9AD"
            }
        };
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), languages);
        var selected = draft.Languages[1];
        var preview = SetupWizardLanguagePreview.Build(selected);
        var actual = SetupWizardSpeechPreview.Message("chat.language", draft, 0);

        Assert.Equal(preview.Understood + "<br>" + preview.Unfamiliar, actual);
        Assert.Contains("#67C9AD", actual);
        Assert.DoesNotContain("#F3CD97", actual);
        Assert.Equal(actual, SetupWizardSpeechPreview.Message("chat.language", draft, 300));

        selected.Name = "Harbor Cant";
        selected.Prefix = "harbor";
        selected.Color = "#DCB36A";
        selected.Syllables = "zu";
        LanguageConfigAdmin.ValidateEntries([selected with { Default = true }], out var parsed);
        var expected = LanguageScrambler.ScrambleMessage("Meet me by the gate.", Assert.Single(parsed));
        var edited = SetupWizardSpeechPreview.Message("chat.language", draft, 0);
        Assert.NotEqual(actual, edited);
        Assert.Contains("#DCB36A", edited);
        Assert.Contains("<i>" + expected + "</i>", edited);
        Assert.Equal("River Cant", languages[1].Name);
    }

    [Fact]
    public void MissingConfiguredLanguagesDoNotInventShippedLanguageExamples()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());

        Assert.Empty(draft.Languages);
        Assert.Empty(SetupWizardSpeechPreview.Message("chat.language", draft, 0));
        draft.Set("EnableLanguageSystem", "0");
        Assert.Empty(SetupWizardSpeechPreview.Message("chat.language", draft, 0));
    }

    [Theory]
    [InlineData("chat.ranges")]
    [InlineData("chat.obfuscation")]
    public void NearbySpeechUsesAValidConfiguredDefaultAndStaysNeutralWithoutOne(string page)
    {
        var first = new LanguageConfigEntryMessage
        {
            Name = "Hillspeech",
            Description = "Spoken in the highlands.",
            Prefix = "hill",
            Syllables = "or, an",
            Color = "#F3CD97"
        };
        var selectedDefault = first with
        {
            Name = "River Cant",
            Prefix = "river",
            Color = "#67C9AD",
            Default = true
        };
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(),
            [selectedDefault with { Color = "blue" }, first, selectedDefault]);

        var configured = SetupWizardSpeechPreview.Message(page, draft, 0);
        Assert.Contains("#67C9AD", configured);
        Assert.DoesNotContain("#F3CD97", configured);
        Assert.Contains("Meet me by the gate.", configured);

        draft.Languages.RemoveAt(2);
        var firstValid = SetupWizardSpeechPreview.Message(page, draft, 0);
        Assert.Contains("#F3CD97", firstValid);
        Assert.DoesNotContain("#67C9AD", firstValid);

        draft.Languages.RemoveAt(1);
        var invalidOnly = SetupWizardSpeechPreview.Message(page, draft, 0);
        Assert.Contains("Meet me by the gate.", invalidOnly);
        Assert.DoesNotContain("<font", invalidOnly);
        draft.Languages.Clear();
        Assert.Equal(invalidOnly, SetupWizardSpeechPreview.Message(page, draft, 0));
        draft.Set("ProximityChatPresentationMode", "Prose");
        Assert.Contains("Meet me by the gate.", SetupWizardSpeechPreview.Message(page, draft, 0));
    }

    [Fact]
    public void SigningKeepsItsBuiltInLanguageWhenConfiguredLanguageDataIsMissing()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var message = SetupWizardSpeechPreview.Message("chat.signlanguage", draft, 0);

        Assert.Contains("signs", message);
        Assert.Contains(LanguageSystem.SignLanguage.Color, message);
    }

    [Fact]
    public void ProseKeepsActionsClearAndMirrorsTheRuntimeLanguageGate()
    {
        var draft = Draft();
        draft.Set("ProximityChatPresentationMode", "Prose");
        draft.Set("ProximityChatModeObfuscationRanges.Normal", "0");
        string prose = SetupWizardSpeechPreview.Message("chat.obfuscation", draft, 5);
        Assert.Contains("waves.", prose);
        Assert.Contains('*', prose);
        draft.Set("EnableLanguageSystem", "0");
        Assert.DoesNotContain('*', SetupWizardSpeechPreview.Message("chat.obfuscation", draft, 5));
        draft.Set("DisableRPChat", "1");
        Assert.Contains("PipPlayer", SetupWizardSpeechPreview.Message("chat.language", draft, 0));
    }

    [Fact]
    public void IncompleteDraftInputDoesNotThrowOrBecomeUnlimited()
    {
        var draft = Draft();
        foreach (var value in new[] { "", "-", "NaN", "1e999", "999999999999999999999" })
        {
            draft.Set("SignLanguageRange", value);
            draft.Set("ProximityChatModeDistances.Normal", value);
            draft.Set("ProximityChatModeObfuscationRanges.Normal", value);
            Assert.NotEmpty(SetupWizardSpeechPreview.Message("chat.ranges", draft, double.PositiveInfinity));
            Assert.False(SetupWizardSpeechPreview.Receives("chat.ranges", draft, ProximityChatMode.Normal, new(1, 80, 0)));
        }
    }

    [VisualTheory]
    [InlineData(1.0, 130)]
    [InlineData(1.25, 180)]
    public void SceneShowsListenerMotionAndSigningDeliveryWithoutWorldEffects(double scale, int height)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        var draft = Draft();
        draft.Set("SignLanguageRange", "16");
        using var near = Render(draft, "chat.signlanguage", 0, scale, height);
        using var behind = Render(draft, "chat.signlanguage", 8, scale, height);
        Assert.False(near.Data.SequenceEqual(behind.Data));
        draft.Set("RequireLineOfSightForSignLanguage", "0");
        using var throughWall = Render(draft, "chat.signlanguage", 8, scale, height);
        Assert.False(behind.Data.SequenceEqual(throughWall.Data));
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0, 130)]
    [InlineData(1.25, 180)]
    public void SightObstacleAppearsOnlyInSigningScenes(double scale, int height)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        var draft = Draft();
        draft.Set("SignLanguageRange", "35");
        using var signing = Render(draft, "chat.signlanguage", 0, scale, height);
        using var hearing = Render(draft, "chat.ranges", 0, scale, height);
        using var fading = Render(draft, "chat.obfuscation", 0, scale, height);
        int x = (int)(99 * scale), y = (int)(((height - 25) / 2d - 3.5) * scale);
        int offset = y * signing.Stride + x * 4;
        Assert.True(signing.Data[offset + 2] > hearing.Data[offset + 2] + 40,
            "The stone sight obstacle must be visible for signing.");
        Assert.Equal(hearing.Data[offset..(offset + 4)], fading.Data[offset..(offset + 4)]);
    }

    private static SetupWizardDraft Draft() => new(SetupWizardCaptureScenes.DefaultValues(), SetupWizardCaptureScenes.DefaultLanguages());

    private static ImageSurface Render(SetupWizardDraft draft, string page, double time, double scale, int height)
    {
        var surface = new ImageSurface(Format.Argb32, (int)Math.Ceiling(310 * scale), (int)Math.Ceiling(height * scale));
        using var context = new Context(surface);
        SetupWizardSpeechPreview.Draw(context, surface, ElementBounds.FixedSize(310, height), page, draft, time);
        surface.Flush();
        return surface;
    }
    [Fact]
    public void SoundAndSightStaySeparateAndInvalidDraftsRemainIntact()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        var behindWall = SetupWizardSpeechPreview.Listener(8.5);
        draft.Set("RequireClearSoundPathForSpeech.Normal", "1");
        Assert.False(SetupWizardSpeechPreview.Receives("chat.occlusion", draft, ProximityChatMode.Normal, behindWall));
        draft.Set("ProximityChatModeDistances.Normal", "-1");
        Assert.True(SetupWizardSpeechPreview.Receives("chat.occlusion", draft, ProximityChatMode.Normal, behindWall));
        Assert.Contains(ConfigAdminSettingRegistry.ValidateConfig(SetupWizardTextAppearancePreview.Config(draft)), e => e.Contains("unlimited"));
        draft.Set("ProximityChatModeDistances.Normal", "35"); draft.Set("RequireClearSoundPathForSpeech.Normal", "0");
        draft.Set("SpeechOcclusionWallPenaltyBlocks", "28");
        Assert.Equal(0, SetupWizardSpeechPreview.ObscuredFraction(draft, ProximityChatMode.Normal, behindWall, audible: false));
        Assert.True(SetupWizardSpeechPreview.ObscuredFraction(draft, ProximityChatMode.Normal, behindWall) > 0);
        Assert.False(SetupWizardSpeechPreview.Receives("chat.occlusion", draft, ProximityChatMode.Normal, behindWall));
        draft.Set("SpeechOcclusionWallPenaltyBlocks", "0");
        Assert.True(SetupWizardSpeechPreview.Receives("chat.occlusion", draft, ProximityChatMode.Normal, behindWall));
        draft.Set("SightBlockingBlockCodePatterns", "game:glass-*"); draft.Set("SightPassThroughBlockCodePatterns", "game:stone-*");
        Assert.Contains("glass: blocked", SetupWizardTextAppearancePreview.Sight(draft));
        Assert.Contains("stone: clear", SetupWizardTextAppearancePreview.Sight(draft));
        Assert.True(SetupWizardSpeechPreview.Receives("chat.occlusion", draft, ProximityChatMode.Normal, behindWall));
        draft.Set("SightPassThroughBlockCodePatterns", "game:glass-*");
        Assert.Contains(ConfigAdminSettingRegistry.ValidateConfig(SetupWizardTextAppearancePreview.Config(draft)), e => e.Contains("both"));
        draft.Set("SightPassThroughBlockCodePatterns", "unqualified");
        Assert.Contains(ConfigAdminSaveWorkflow.ApplyValues(SetupWizardTextAppearancePreview.Config(draft), draft.Values), e => e.Contains("fully qualified"));
        Assert.Equal("unqualified", draft.Get("SightPassThroughBlockCodePatterns"));
    }
}
