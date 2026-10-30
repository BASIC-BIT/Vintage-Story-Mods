using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Models;
namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardSearchTests
{
    public SetupWizardSearchTests() => LangTestHelper.EnsureEnglish();
    [Theory]
    [InlineData("placement")]
    [InlineData("move")]
    [InlineData("remove")]
    public void SearchSelectedIdenticalSheetRecordsMutateByReference(string operation)
    {
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), fields: CharacterSheetFieldConfigAdmin.BuildEntries(config));
        var first = draft.SheetFields[0];
        draft.SheetFields.Clear(); draft.SheetFields.Add(first);
        var selected = first with { }; draft.SheetFields.Add(selected);
        var panel = new SetupWizardSearchPanel();
        var result = panel.Index(draft).Last(item => item.Destination == ConfigSearchDestination.CharacterField && item.Key == "LayoutSection");
        using var editor = new SetupWizardSheetEditor(null, draft, null, null);
        editor.SelectSearchField(panel.ResolveField(draft, result.EntryId), result.Key);
        Assert.Same(selected, editor.Selected);
        if (operation == "placement") editor.SetPlacement(first.LayoutSection == "body" ? "header-side" : "body");
        else if (operation == "move") { Assert.True(editor.CanMove(-1)); editor.MoveSelected(-1); }
        else Assert.True(editor.RemoveSelected(true));
        Assert.Single(draft.SheetFields, item => ReferenceEquals(item, first));
        Assert.Equal(operation == "remove" ? 0 : 1, draft.SheetFields.Count(item => ReferenceEquals(item, selected)));
        if (operation == "move") Assert.Same(selected, draft.SheetFields[0]);
        if (operation == "placement") Assert.NotEqual(first.LayoutSection, selected.LayoutSection);
    }

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
