using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Models;
namespace thebasics.Tests.ModSystems.ChatUiSystem;
public class SetupWizardSearchTests
{
    public SetupWizardSearchTests() => LangTestHelper.EnsureEnglish();
    [Fact]
    public void CurrentDraftEntriesKeepIdentityAcrossRenameReorderAndDuplicateLabels()
    {
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), LanguageConfigAdmin.BuildEntries(config), CharacterSheetFieldConfigAdmin.BuildEntries(config));
        var panel = new SetupWizardSearchPanel();
        var entry = draft.Languages[0]; entry.Description = "unique unsaved words";
        var result = Assert.Single(ConfigAdminSearch.Find(panel.Index(draft), "unique unsaved words"));
        entry.Name = ""; draft.Languages.Reverse();
        draft.Languages.Add(new LanguageConfigEntryMessage { Name = "", Description = "unique unsaved words" });
        var matches = ConfigAdminSearch.Find(panel.Index(draft), "unique unsaved words");
        Assert.Equal(2, matches.Count); Assert.NotEqual(matches[0].EntryId, matches[1].EntryId);
        Assert.Same(entry, panel.ResolveLanguage(draft, result.EntryId));
        Assert.Equal("", entry.Name);
        var field = draft.SheetFields[0]; field.Description = "fresh template definition";
        var fieldResult = Assert.Single(ConfigAdminSearch.Find(panel.Index(draft), "fresh template definition"));
        field.Label = ""; draft.SheetFields.Reverse();
        draft.SheetFields.Add(new CharacterSheetFieldConfigEntryMessage { Id = "new", Label = "", Description = "fresh template definition" });
        Assert.Equal(2, ConfigAdminSearch.Find(panel.Index(draft), "fresh template definition").Count);
        Assert.Same(field, panel.ResolveField(draft, fieldResult.EntryId));
        Assert.DoesNotContain(panel.Index(draft), item => item.Key == "Answers");
    }
    [Fact]
    public void EveryScalarHasOneCanonicalDestinationAndSearchUsesUnsavedValues()
    {
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("ProximityChatName", "Uncommitted vicinity");
        var results = new SetupWizardSearchPanel().Index(draft);
        Assert.True(Assert.Single(ConfigAdminSearch.Find(results, "ProximityChatName Uncommitted")).Unsaved);
        foreach (var setting in ConfigAdminSettingRegistry.Settings)
            Assert.Contains(setting.Key, SetupWizardCatalog.FindPageForSetting(setting.Key)!.SettingKeys);
        Assert.Equal(SetupWizardCatalog.Pages.First(p => p.SettingKeys.Contains("MaxNicknameLength")), SetupWizardCatalog.FindPageForSetting("MaxNicknameLength"));
    }
}
