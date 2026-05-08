using FluentAssertions;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class LanguageConfigAdminTests
{
    [Fact]
    public void BuildEntries_RoundTripsDefaultLanguages()
    {
        var config = CreateConfig();

        var entries = LanguageConfigAdmin.BuildEntries(config);
        var success = LanguageConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeTrue(string.Join("\n", errors));
        config.Languages.Select(language => language.Name).Should().Equal("Common", "Tradeband");
        config.Languages[0].Syllables.Should().Contain("al");
    }

    [Fact]
    public void TryApplyEntries_RejectsDuplicateNames()
    {
        var config = CreateConfig();
        var entries = LanguageConfigAdmin.BuildEntries(config);
        entries[1].Name = "common";

        var success = LanguageConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("duplicated"));
        config.Languages.Select(language => language.Name).Should().Equal("Common", "Tradeband");
    }

    [Fact]
    public void TryApplyEntries_RejectsDuplicatePrefixes()
    {
        var config = CreateConfig();
        var entries = LanguageConfigAdmin.BuildEntries(config);
        entries[1].Prefix = "C";

        var success = LanguageConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("prefix") && error.Contains("duplicated"));
    }

    [Theory]
    [InlineData("Babble", "bb")]
    [InlineData("Sign", "sg")]
    [InlineData("Test", "babble")]
    [InlineData("Test", "sign")]
    public void TryApplyEntries_RejectsReservedBuiltInIdentifiers(string name, string prefix)
    {
        var config = CreateConfig();
        var entries = new List<LanguageConfigEntryMessage>
        {
            ValidEntry() with { Name = name, Prefix = prefix }
        };

        var success = LanguageConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("reserved"));
    }

    [Theory]
    [InlineData("bad-prefix")]
    [InlineData("bad prefix")]
    [InlineData(":bad")]
    public void TryApplyEntries_RejectsPrefixesTheChatParserCannotRead(string prefix)
    {
        var config = CreateConfig();
        var entries = new List<LanguageConfigEntryMessage> { ValidEntry() with { Prefix = prefix } };

        var success = LanguageConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("letters, numbers, and underscore"));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#GGDDCC")]
    public void TryApplyEntries_RejectsInvalidColors(string color)
    {
        var config = CreateConfig();
        var entries = new List<LanguageConfigEntryMessage> { ValidEntry() with { Color = color } };

        var success = LanguageConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("hex color"));
    }

    [Fact]
    public void TryApplyEntries_RejectsEmptySyllables()
    {
        var config = CreateConfig();
        var entries = new List<LanguageConfigEntryMessage> { ValidEntry() with { Syllables = "" } };

        var success = LanguageConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("at least one syllable"));
    }

    [Fact]
    public void TryApplyEntries_NormalizesCommaSeparatedGrantArrays()
    {
        var config = CreateConfig();
        var entries = new List<LanguageConfigEntryMessage>
        {
            ValidEntry() with
            {
                GrantedToClasses = "hunter, hunter, tailor",
                GrantedToTraits = "strongback, forager",
                GrantedToModels = "wolf, wolftailored",
                GrantedToModelGroups = "canine, canine"
            }
        };

        var success = LanguageConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeTrue(string.Join("\n", errors));
        var language = config.Languages.Single();
        language.GrantedToClasses.Should().Equal("hunter", "tailor");
        language.GrantedToTraits.Should().Equal("strongback", "forager");
        language.GrantedToModels.Should().Equal("wolf", "wolftailored");
        language.GrantedToModelGroups.Should().Equal("canine");
    }

    [Fact]
    public void BuildRenameMap_TracksChangedExistingNames()
    {
        var entries = new List<LanguageConfigEntryMessage>
        {
            ValidEntry() with { OriginalName = "Old", Name = "New" },
            ValidEntry() with { OriginalName = "Stable", Name = "Stable", Prefix = "stable" }
        };

        var map = LanguageConfigAdmin.BuildRenameMap(entries);

        map.Should().ContainKey("Old").WhoseValue.Should().Be("New");
        map.Should().NotContainKey("Stable");
    }

    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    private static LanguageConfigEntryMessage ValidEntry()
    {
        return new LanguageConfigEntryMessage
        {
            OriginalName = "Test",
            Name = "Test",
            Description = "Test language",
            Prefix = "test",
            Color = "#AABBCC",
            Syllables = "ta, ro",
            Default = true
        };
    }
}
