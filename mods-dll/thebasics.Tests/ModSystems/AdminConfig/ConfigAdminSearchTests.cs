using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.CharacterSheets.Models;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class ConfigAdminSearchTests
{
    public ConfigAdminSearchTests() => LangTestHelper.EnsureEnglish();
    [Fact]
    public void Search_matches_all_words_across_metadata_and_current_draft_values()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var loaded = ConfigAdminSettingRegistry.Settings.ToDictionary(setting => setting.Key, setting => setting.GetValue(config));
        var draft = new Dictionary<string, string>(loaded) { ["ProximityChatTabPosition"] = "AfterGeneral" };
        var entries = ConfigAdminSearch.Build(config, draft, loaded);
        var result = Assert.Single(ConfigAdminSearch.Find(entries, "  PROXIMITYCHATTABPOSITION\tAfterGeneral  "));
        Assert.True(result.Unsaved);
        Assert.Equal(ConfigSearchDestination.Setting, result.Destination);
        Assert.Equal("Chat tabs", result.Group);
        Assert.Empty(ConfigAdminSearch.Find(entries, "ProximityChatTabPosition impossible-value"));
        Assert.Empty(ConfigAdminSearch.Find(entries, " \t "));
        draft["ProximityChatTabPosition"] = loaded["ProximityChatTabPosition"];
        Assert.False(ConfigAdminSearch.Build(config, draft, loaded).Single(entry => entry.Key == result.Key).Unsaved);
    }

    [Fact]
    public void Search_indexes_language_values_and_field_definitions_with_editor_destinations()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        config.Languages[0] = config.Languages[0] with { Description = "crystalline vocabulary" };
        config.CharacterSheetFields = new List<CharacterSheetFieldDefinition>
        {
            new() { Id = "occupation", Label = "Occupation", Description = "Your trade", Type = "select", Options = new List<string> { "Glassblower", "Scribe" } }
        };
        var draft = ConfigAdminSettingRegistry.Settings.ToDictionary(setting => setting.Key, setting => setting.GetValue(config));
        var entries = ConfigAdminSearch.Build(config, draft, draft);
        var language = Assert.Single(ConfigAdminSearch.Find(entries, "crystalline vocabulary"));
        Assert.Equal(ConfigSearchDestination.Language, language.Destination);
        Assert.Equal(config.Languages[0].Name, language.EntryId);
        Assert.Equal("Description", language.Key);
        var field = Assert.Single(ConfigAdminSearch.Find(entries, "occupation glassblower"));
        Assert.Equal(ConfigSearchDestination.CharacterField, field.Destination);
        Assert.Equal("occupation", field.EntryId);
        Assert.Equal("Options", field.Key);
    }
}
