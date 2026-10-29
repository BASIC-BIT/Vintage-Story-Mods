using System;
using FluentAssertions;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardSheetDraftTests
{
    [Fact]
    public void SnapshotsRefreshAndResetAreIndependentAndPreserveOtherEdits()
    {
        var source = new[] { new CharacterSheetFieldConfigEntryMessage { Id = "one", Label = "Original" } };
        var draft = new SetupWizardDraft(new[] { new ConfigAdminSettingValue { Key = "EnableChatter", Value = "1" } }, fields: source);
        source[0].Label = "External";
        draft.SheetFields[0].Label.Should().Be("Original");
        draft.OriginalSheetFields[0].Label = "External";
        draft.SheetFieldsChanged.Should().BeFalse();
        draft.RefreshSheetFields(new[] { source[0] });
        draft.SheetFields[0].Label.Should().Be("External");
        draft.SheetFields[0].Label = "Edit";
        draft.Set("EnableChatter", "0");
        draft.Languages.Add(new LanguageConfigEntryMessage());
        draft.RefreshSheetFields(new[] { new CharacterSheetFieldConfigEntryMessage { Id = "replacement" } });
        draft.SheetFields[0].Label.Should().Be("Edit");
        draft.ChangedKeys.Should().Contain("CharacterSheetFields");
        draft.ResetSheetFields();
        draft.SheetFields[0].Label.Should().Be("External");
        draft.Get("EnableChatter").Should().Be("0");
        draft.Languages.Should().ContainSingle();
        draft.SheetFieldsChanged.Should().BeFalse();
    }

    [Fact]
    public void ResetBlocksPendingAndUnknownSavesAndScalarSerializationIgnoresFields()
    {
        var draft = new SetupWizardDraft(Array.Empty<ConfigAdminSettingValue>(), fields: new[] { CharacterSheetFieldConfigAdmin.CreateBuilderEntry() });
        draft.SheetFields[0].Label = "Edit";
        draft.CreateSaveRequest(1, "run").Values.Should().BeEmpty();
        Action reset = () => draft.ResetSheetFields();
        reset.Should().Throw<InvalidOperationException>();
        draft.MarkSaveTimedOut();
        reset.Should().Throw<InvalidOperationException>();
    }
}
