using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.ChatUiSystem;
namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardSheetReviewTests
{
    [Theory]
    [InlineData("option", "Red,Blue", "0")]
    [InlineData("longstring", "", "6")]
    public void Inactive_type_roundtrip_is_not_an_effective_edit(string type, string options, string rows)
    {
        var before = new[] { SetupWizardSheetEditorTests.Field("name", "body") };
        var draft = new SetupWizardDraft(Array.Empty<ConfigAdminSettingValue>(), fields: before);
        var field = draft.SheetFields[0];
        field.Type = type; field.Options = options; field.EditorRows = rows; field.Type = "string";
        Assert.False(draft.SheetFieldsChanged);
        Assert.DoesNotContain("CharacterSheetFields", draft.ChangedKeys);
        Assert.Empty(SetupWizardSheetReview.Rows(before, draft.SheetFields, true));
        Assert.Empty(draft.CreateSaveRequest(1, "run").SheetFields);
        field.Type = type;
        Assert.Equal(options, field.Options); Assert.Equal(rows, field.EditorRows);
        Assert.True(draft.SheetFieldsChanged);
    }

    [Theory]
    [InlineData("option", "Red,,Blue", "0")]
    [InlineData("longstring", "", "invalid")]
    public void Invalid_active_type_values_remain_dirty_and_fail_validation(string type, string options, string rows)
    {
        var draft = new SetupWizardDraft(Array.Empty<ConfigAdminSettingValue>(), fields: new[] { SetupWizardSheetEditorTests.Field("name", "body") });
        var field = draft.SheetFields[0]; field.Type = type; field.Options = options; field.EditorRows = rows;
        Assert.True(draft.SheetFieldsChanged);
        Assert.NotEmpty(SetupWizardSheetReview.Rows(draft.OriginalSheetFields, draft.SheetFields, true));
        var errors = CharacterSheetFieldConfigAdmin.ValidateEntries(SetupWizardState.NormalizeSheetDraft(draft.SheetFields), out _)
            .Concat(SetupWizardState.ValidateSheetChoices(draft.SheetFields));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Pending_save_preserves_later_inactive_input_using_raw_submission()
    {
        var before = new[] { SetupWizardSheetEditorTests.Field("name", "body") };
        var draft = new SetupWizardDraft(Array.Empty<ConfigAdminSettingValue>(), fields: before);
        draft.SheetFields[0].Label = "Saved label";
        var sent = draft.CreateSaveRequest(1, "run");
        draft.SheetFields[0].Options = "Retained,Later";
        Assert.True(draft.MergeSaveResult(new TheBasicsSetupWizardResultMessage { Kind = SetupWizardResultKind.SaveResult, RequestId = 1, RunId = "run", Success = true, SheetFields = sent.SheetFields }));
        Assert.Equal("Retained,Later", draft.SheetFields[0].Options);
        Assert.False(draft.SheetFieldsChanged);
        draft.SheetFields[0].Type = "option";
        Assert.Equal("Retained,Later", draft.SheetFields[0].Options);
        Assert.True(draft.SheetFieldsChanged);
    }

    [Fact]
    public void Net_reverted_edits_have_no_rows_or_warnings()
    {
        var before = new[] { SetupWizardSheetEditorTests.Field("name", "body") };
        var after = CharacterSheetFieldConfigAdmin.CloneEntries(before); after[0].Type = "number"; after[0].Type = "string";
        Assert.Empty(SetupWizardSheetReview.Rows(before, after, true));
    }
    [Fact]
    public void Review_groups_readable_changes_and_exact_consequences_without_ids()
    {
        var before = new[] { SetupWizardSheetEditorTests.Field("internal", "body") with { Label = "Appearance" }, SetupWizardSheetEditorTests.Field("removed-id", "body") with { Label = "Background" } };
        var after = CharacterSheetFieldConfigAdmin.CloneEntries(before.Take(1));
        after[0].Type = "longstring"; after[0].BindTo = "thebasics.fullName"; after[0].Optional = false;
        var rows = SetupWizardSheetReview.Rows(before, after, true);
        Assert.Equal(2, rows.Count); Assert.Equal("Background", rows[0].Title); Assert.Equal(SetupWizardSheetReview.RemovalWarning, rows[0].Detail);
        Assert.Contains("Short text > Long text", rows[1].Detail); Assert.Contains("Character name", rows[1].Detail);
        Assert.Contains(SetupWizardSheetReview.RulesWarning, rows[1].Detail); Assert.Contains(SetupWizardSheetReview.BindingWarning, rows[1].Detail);
        Assert.Contains("block roleplay", rows[1].Detail); Assert.DoesNotContain("internal", string.Join(" ", rows));
        after[0].Visibility = "admin";
        Assert.DoesNotContain("block roleplay", string.Join(" ", SetupWizardSheetReview.Rows(before, after, true)));
        after[0].Visibility = "public";
        after[0].BindTo = "thebasics.nickname";
        Assert.DoesNotContain("block roleplay", string.Join(" ", SetupWizardSheetReview.Rows(before, after, true, true)));
        Assert.DoesNotContain("block roleplay", string.Join(" ", SetupWizardSheetReview.Rows(before, after, false)));
    }
    [Theory]
    [InlineData("First,,Second")]
    [InlineData("First,first")]
    public void Wizard_choice_validation_rejects_empty_and_duplicate_rows_but_preserves_inactive_values(string options)
    {
        var field = SetupWizardSheetEditorTests.Field("choice", "body") with { Type = "option", Options = options };
        Assert.NotEmpty(SetupWizardState.ValidateSheetChoices(new[] { field }));
        field.Type = "string"; Assert.Empty(SetupWizardState.ValidateSheetChoices(new[] { field })); Assert.Equal(options, field.Options);
    }
    [Fact]
    public void Journey_allows_only_fixed_builder_actions_and_never_accepts_contents_as_choice()
    {
        foreach (var action in new[] { "sheet_select", "sheet_add", "sheet_remove", "sheet_move", "sheet_preview", "sheet_reset" })
        {
            Assert.True(AnalyticsService.IsValidSetupWizardJourney("0123456789abcdef0123456789abcdef", 1, "characters.builder", action));
            foreach (var privateValue in new[] { "Appearance", "Private description", "Red,Blue", "field-secret", "Pip maps quiet roads" })
                Assert.False(AnalyticsService.IsValidSetupWizardJourney("0123456789abcdef0123456789abcdef", 1, "characters.builder", action, privateValue));
        }
    }
}
