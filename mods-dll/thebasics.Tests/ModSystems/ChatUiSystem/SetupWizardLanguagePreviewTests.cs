using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.ProximityChat;
using thebasics.Utilities;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardLanguagePreviewTests
{
    [Fact]
    public void UsesTheSelectedConfiguredSyllablesAndColorWithoutChangingTheDraft()
    {
        var entry = Entry();
        var preview = SetupWizardLanguagePreview.Build(entry);
        LanguageConfigAdmin.ValidateEntries([entry with { Default = true }], out var languages);
        var expected = LanguageScrambler.ScrambleMessage("Meet me by the gate.", Assert.Single(languages));

        Assert.True(preview.IsValid);
        Assert.Empty(preview.Errors);
        Assert.Contains("Meet me by the gate.", preview.Understood);
        Assert.Contains("<font color=\"#67C9AD\">", preview.Understood);
        Assert.Contains("<i>" + expected + "</i>", preview.Unfamiliar);
        Assert.DoesNotContain("Meet me", preview.Unfamiliar);
        Assert.Equal(preview, SetupWizardLanguagePreview.Build(entry));
        Assert.False(entry.Default);
        Assert.Equal("River Cant", entry.Name);

        var changed = SetupWizardLanguagePreview.Build(entry with { Syllables = "zu", Color = "#DCB36A" });
        Assert.NotEqual(preview.Unfamiliar, changed.Unfamiliar);
        Assert.Contains("#DCB36A", changed.Understood);
        Assert.Contains("Zu", changed.Unfamiliar);
    }

    [Theory]
    [InlineData(ProximityChatPresentationModes.StandardRoleplay, "Pip says ", true)]
    [InlineData(ProximityChatPresentationModes.SimpleSpeech, "Pip: ", true)]
    [InlineData(ProximityChatPresentationModes.PlainProximity, "Pip: ", false)]
    [InlineData(ProximityChatPresentationModes.Prose, "Pip waves. ", true)]
    public void UsesCurrentPresentationAndEscapesSampleText(string presentation, string attribution, bool quoted)
    {
        var config = Config();
        config.ProximityChatPresentationMode = presentation;
        var preview = SetupWizardLanguagePreview.Build(Entry(), "<icon name=\"fire\"> & hello!", config);

        Assert.True(preview.IsValid);
        Assert.Contains(attribution, VtmlUtils.StripVtmlTags(preview.Understood));
        Assert.Contains("&lt;icon name=\"fire\"&gt; & hello!", preview.Understood);
        Assert.DoesNotContain("<icon", preview.Understood);
        Assert.DoesNotContain("<icon", preview.Unfamiliar);
        Assert.Equal(quoted, preview.Understood.Contains(">\"&lt;icon", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void DisabledLanguageFeaturesKeepBothListenersReadable(bool languageSystem, bool rpDisabled)
    {
        var config = Config();
        config.EnableLanguageSystem = languageSystem;
        config.DisableRPChat = rpDisabled;
        var preview = SetupWizardLanguagePreview.Build(Entry(), config: config);

        Assert.True(preview.IsValid);
        Assert.Equal(preview.Understood, preview.Unfamiliar);
        Assert.Contains(rpDisabled ? "PipPlayer says" : "Pip says", preview.Understood);
        Assert.DoesNotContain("#67C9AD", preview.Understood);
        Assert.DoesNotContain("<i>", preview.Unfamiliar);
    }

    [Fact]
    public void IncompleteOrUnsafeDraftsReturnEscapedValidationFeedbackInsteadOfSpeech()
    {
        foreach (var entry in new[]
        {
            Entry() with { Color = "red" },
            Entry() with { Syllables = " , " },
            Entry() with { Syllables = "<icon>" },
            Entry() with { Name = "<font color=red>" },
            Entry() with { Prefix = "bad prefix" },
            null
        })
        {
            var preview = SetupWizardLanguagePreview.Build(entry);
            Assert.False(preview.IsValid);
            Assert.NotEmpty(preview.Errors);
            Assert.All(preview.Errors, error => Assert.DoesNotContain('<', error));
            Assert.Empty(preview.Understood);
            Assert.Empty(preview.Unfamiliar);
        }
    }

    [Fact]
    public void UsesConfiguredQuotesAndHandlesEmptyText()
    {
        var config = Config();
        config.ChatDelimiters.Quote.Start = "[";
        config.ChatDelimiters.Quote.End = "]";
        var preview = SetupWizardLanguagePreview.Build(Entry(), "A \"quoted\" word.", config);

        Assert.Contains("[A \"quoted\" word.]", preview.Understood);
        Assert.Contains("[<i>", preview.Unfamiliar);
        Assert.DoesNotContain("Common", preview.Understood);
        Assert.True(SetupWizardLanguagePreview.Build(Entry(), null).IsValid);
        Assert.Equal("Pip says ", SetupWizardLanguagePreview.Build(Entry(), null).Understood);
    }

    private static LanguageConfigEntryMessage Entry() => new()
    {
        Name = "River Cant", Description = "Spoken along the rivers.", Prefix = "river",
        Syllables = "sha, vri, ko", Color = "#67C9AD"
    };

    private static ModConfig Config()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }
}
