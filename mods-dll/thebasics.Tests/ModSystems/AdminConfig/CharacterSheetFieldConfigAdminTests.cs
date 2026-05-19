using FluentAssertions;
using ProtoBuf;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.CharacterSheets.Models;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class CharacterSheetFieldConfigAdminTests
{
    [Fact]
    public void BuildEntries_RoundTripsDefaultCharacterSheetFields()
    {
        var config = CreateConfig();

        var entries = CharacterSheetFieldConfigAdmin.BuildEntries(config);
        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeTrue(string.Join("\n", errors));
        config.CharacterSheetFields.Should().NotBeEmpty();
        config.CharacterSheetFields.Should().Contain(field => field.Id == "fullName" && field.BindTo == "thebasics.fullName");
    }

    [Fact]
    public void TryApplyEntries_RejectsDuplicateKeys()
    {
        var config = CreateConfig();
        var entries = CharacterSheetFieldConfigAdmin.BuildEntries(config);
        entries[1].OriginalId = string.Empty;
        entries[1].Id = entries[0].Id;

        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("duplicated"));
    }

    [Fact]
    public void TryApplyEntries_RejectsRenamingSavedKey()
    {
        var config = CreateConfig();
        var entry = ValidEntry() with
        {
            OriginalId = "summary",
            Id = "first-impression"
        };

        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, [entry], out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("cannot rename saved field keys"));
    }

    [Theory]
    [InlineData("thebasics.fullName", CharacterSheetFieldTypes.Number)]
    [InlineData("thebasics.nickname", CharacterSheetFieldTypes.Option)]
    public void TryApplyEntries_RejectsNonStringBoundFields(string bindTo, string type)
    {
        var config = CreateConfig();
        var entry = ValidEntry() with
        {
            BindTo = bindTo,
            Type = type,
            Options = type == CharacterSheetFieldTypes.Option ? "One, Two" : string.Empty
        };

        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, [entry], out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("requires a string field"));
    }

    [Fact]
    public void TryApplyEntries_RejectsDuplicateBindTargets()
    {
        var config = CreateConfig();
        var entries = new List<CharacterSheetFieldConfigEntryMessage>
        {
            ValidEntry() with { OriginalId = "fullName", Id = "fullName", BindTo = "thebasics.fullName" },
            ValidEntry() with { OriginalId = "legalName", Id = "legalName", Label = "Legal Name", BindTo = "thebasics.fullName" }
        };

        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, entries, out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("already used"));
    }

    [Fact]
    public void TryApplyEntries_RejectsUnknownBindTarget()
    {
        var config = CreateConfig();
        var entry = ValidEntry() with { BindTo = "thebasics.other" };

        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, [entry], out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("bind target must be blank"));
    }

    [Fact]
    public void TryApplyEntries_RejectsOptionFieldWithoutOptions()
    {
        var config = CreateConfig();
        var entry = ValidEntry() with { Type = CharacterSheetFieldTypes.Option, Options = string.Empty };

        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, [entry], out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("at least one option"));
    }

    [Fact]
    public void TryApplyEntries_RejectsInvalidNumericInputs()
    {
        var config = CreateConfig();
        var entry = ValidEntry() with { MaxLength = "abc", EditorRows = "xyz", Type = CharacterSheetFieldTypes.LongString };

        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, [entry], out var errors);

        success.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("max length must be a whole number"));
        errors.Should().Contain(error => error.Contains("editor rows must be a whole number"));
    }

    [Fact]
    public void TryApplyEntries_AppliesDescriptionAndOptions()
    {
        var config = CreateConfig();
        var entry = ValidEntry() with
        {
            Description = "Visible to admins when editing fields.",
            Type = CharacterSheetFieldTypes.Option,
            Options = "Calm, Guarded, Calm"
        };

        var success = CharacterSheetFieldConfigAdmin.TryApplyEntries(config, [entry], out var errors);

        success.Should().BeTrue(string.Join("\n", errors));
        config.CharacterSheetFields.Should().ContainSingle();
        config.CharacterSheetFields[0].Description.Should().Be("Visible to admins when editing fields.");
        config.CharacterSheetFields[0].Options.Should().Equal("Calm", "Guarded");
    }

    [Fact]
    public void SaveMessage_RoundTripsFalseBooleanFieldSettings()
    {
        var message = new TheBasicsCharacterSheetFieldConfigSaveMessage
        {
            Fields =
            [
                ValidEntry() with
                {
                    Optional = false,
                    ShowInLook = false
                }
            ]
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;

        var roundTripped = Serializer.Deserialize<TheBasicsCharacterSheetFieldConfigSaveMessage>(stream);

        roundTripped.Fields.Should().ContainSingle();
        roundTripped.Fields[0].Optional.Should().BeFalse();
        roundTripped.Fields[0].ShowInLook.Should().BeFalse();
    }

    [Theory]
    [InlineData("Full Name", "full-name")]
    [InlineData("  Nickname  ", "nickname")]
    [InlineData("***", "field")]
    public void GenerateSuggestedId_BuildsExpectedSlug(string label, string expected)
    {
        CharacterSheetFieldConfigAdmin.GenerateSuggestedId(label).Should().Be(expected);
    }

    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    private static CharacterSheetFieldConfigEntryMessage ValidEntry()
    {
        return new CharacterSheetFieldConfigEntryMessage
        {
            OriginalId = "summary",
            Id = "summary",
            Label = "Summary",
            Description = "First impression shown to readers.",
            Type = CharacterSheetFieldTypes.String,
            Optional = true,
            Options = string.Empty,
            BindTo = string.Empty,
            MaxLength = "120",
            Visibility = CharacterSheetFieldVisibilities.Public,
            ShowInLook = true,
            EditorRows = "0",
            LayoutSection = CharacterSheetLayoutSections.Body,
            Width = CharacterSheetFieldWidths.Full
        };
    }
}
