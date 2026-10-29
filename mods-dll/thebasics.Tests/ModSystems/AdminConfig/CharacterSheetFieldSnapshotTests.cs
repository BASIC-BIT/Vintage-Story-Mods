using System;
using System.Linq;
using FluentAssertions;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class CharacterSheetFieldSnapshotTests
{
    [Fact]
    public void ClonePreservesEveryPropertyWithoutSharingEntries()
    {
        var source = new CharacterSheetFieldConfigEntryMessage { Id = "old", Label = "Label", AutoGenerateId = true };
        foreach (var property in typeof(CharacterSheetFieldConfigEntryMessage).GetProperties())
            property.SetValue(source, property.PropertyType == typeof(bool) ? false : property.Name);
        var clone = CharacterSheetFieldConfigAdmin.CloneEntries(new[] { source });
        clone.Single().Should().BeEquivalentTo(source);
        clone[0].Label = "Changed";
        source.Label.Should().Be("Label");
        CharacterSheetFieldConfigAdmin.CloneEntries(null).Should().BeEmpty();
    }

    [Fact]
    public void EqualityChecksEveryPersistedPropertyAndOrderButIgnoresClientFlag()
    {
        var source = new CharacterSheetFieldConfigEntryMessage();
        CharacterSheetFieldConfigAdmin.EntriesEqual(new[] { source }, new[] { source with { AutoGenerateId = true } }).Should().BeTrue();
        foreach (var property in typeof(CharacterSheetFieldConfigEntryMessage).GetProperties().Where(p => p.Name != "AutoGenerateId"))
        {
            var changed = source with { };
            property.SetValue(changed, property.PropertyType == typeof(bool) ? false : "changed");
            CharacterSheetFieldConfigAdmin.EntriesEqual(new[] { source }, new[] { changed }).Should().BeFalse(property.Name);
        }
        var second = source with { Id = "second" };
        CharacterSheetFieldConfigAdmin.EntriesEqual(new[] { source, second }, new[] { second, source }).Should().BeFalse();
    }

    [Fact]
    public void BuilderCreatesStableUniqueIdsAndDefaults()
    {
        var first = CharacterSheetFieldConfigAdmin.CreateBuilderEntry();
        var second = CharacterSheetFieldConfigAdmin.CreateBuilderEntry();
        first.Id.Should().MatchRegex("^field-[0-9a-f]{32}$");
        second.Id.Should().NotBe(first.Id);
        var id = first.Id;
        first.Label = "New label";
        first.Id.Should().Be(id);
        first.Should().BeEquivalentTo(new CharacterSheetFieldConfigEntryMessage { Id = id, OriginalId = "", Label = "New label", Description = "", Type = "string", Optional = true, Options = "", BindTo = "", MaxLength = "0", Visibility = "public", ShowInLook = true, EditorRows = "0", LayoutSection = "body", Width = "full", AutoGenerateId = false });
    }
}
