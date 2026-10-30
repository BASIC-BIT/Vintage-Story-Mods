using thebasics.Models;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardLanguageDraftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetCannotDiscardLanguageStateBeforePendingSaveIsResolved(bool timedOut)
    {
        var draft = new SetupWizardDraft([], [Row()]);
        draft.Languages[0].Name = "Coastal";
        draft.CreateSaveRequest(1, "run");
        if (timedOut) draft.MarkSaveTimedOut();
        Assert.Throws<InvalidOperationException>(draft.ResetLanguages);
        Assert.Equal("Coastal", draft.Languages[0].Name);
        Assert.Equal("Harbor", draft.OriginalLanguages[0].Name);
    }

    private static LanguageConfigEntryMessage Row() => new()
    {
        Name = "Harbor",
        OriginalName = "Harbor",
        Description = "A language of the coast.",
        Prefix = "harbor",
        Syllables = "sho, ra",
        Color = "#68B8A2",
        Default = true,
        GrantedToClasses = "hunter",
        GrantedToTraits = "keeneyes"
    };

    [Fact]
    public void SnapshotsAreDetachedAndOnlyActualEditsBecomeDirty()
    {
        var incoming = Row();
        var draft = new SetupWizardDraft([], [incoming]);
        incoming.Name = "Outside";
        Assert.Equal("Harbor", draft.Languages[0].Name);
        Assert.False(draft.IsDirty);
        draft.Languages[0].Color = "#112233";
        Assert.Equal(new[] { "Languages" }, draft.ChangedKeys);
        Assert.Equal("#68B8A2", draft.OriginalLanguages[0].Color);
        draft.Languages[0].Color = "#68B8A2";
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void SaveIncludesDetachedLanguagePatchAndFullOriginalsAlongsideScalarChanges()
    {
        var draft = new SetupWizardDraft([new() { Key = "EnableGlobalOOC", Value = "1" }], [Row()]);
        draft.Set("EnableGlobalOOC", "0");
        draft.Languages[0].Name = "Coastal";
        var request = draft.CreateSaveRequest(1, "run");
        Assert.Single(request.Values);
        Assert.Equal("Coastal", Assert.Single(request.Languages).Name);
        Assert.Equal("Harbor", Assert.Single(request.OriginalLanguages).Name);
        draft.Languages[0].Name = "Newer edit";
        Assert.Equal("Coastal", request.Languages[0].Name);
        Assert.Equal("hunter", request.Languages[0].GrantedToClasses);
        Assert.True(draft.IsSavePending);
    }

    [Fact]
    public void UnchangedLanguagesAreNotSubmittedAndOpeningDoesNotInventDefaults()
    {
        var draft = new SetupWizardDraft([], [Row()]);
        var request = draft.CreateSaveRequest(1, "run");
        Assert.Empty(request.Languages);
        Assert.Empty(request.OriginalLanguages);
        Assert.Empty(new SetupWizardDraft([]).Languages);
    }

    [Fact]
    public void SaveAcknowledgesRenamesWithoutDiscardingEditsMadeWhilePending()
    {
        var draft = new SetupWizardDraft([], [Row()]);
        draft.Languages[0].Name = "Coastal";
        draft.CreateSaveRequest(1, "run");
        draft.Languages[0].Color = "#112233";
        var saved = Row() with { Name = "Coastal", OriginalName = "Coastal" };
        Assert.True(draft.MergeSaveResult(Result(1, true, saved)));
        Assert.Equal("#112233", draft.Languages[0].Color);
        Assert.Equal("Coastal", draft.Languages[0].OriginalName);
        Assert.True(draft.IsDirty);
        var retry = draft.CreateSaveRequest(2, "run");
        Assert.Equal("Coastal", retry.Languages[0].OriginalName);
        Assert.Equal("#68B8A2", retry.OriginalLanguages[0].Color);
    }

    [Fact]
    public void SuccessReplacesUnchangedSubmittedDraftAndIgnoresUnrelatedResponses()
    {
        var draft = new SetupWizardDraft([], [Row()]);
        draft.Languages[0].Color = "#112233";
        draft.CreateSaveRequest(1, "run");
        Assert.False(draft.MergeSaveResult(Result(2, true, Row())));
        var saved = Row() with { Color = "#112233" };
        Assert.True(draft.MergeSaveResult(Result(1, true, saved)));
        saved.Color = "#998877";
        Assert.False(draft.IsDirty);
        Assert.Equal("#112233", draft.Languages[0].Color);
        Assert.False(draft.IsSavePending);
    }

    [Fact]
    public void FailureKeepsEditsAndRefreshKeepsDirtyLanguages()
    {
        var draft = new SetupWizardDraft([], [Row()]);
        draft.Languages[0].Color = "#112233";
        draft.RefreshLanguages([Row() with { Color = "#445566" }]);
        Assert.Equal("#112233", draft.Languages[0].Color);
        draft.CreateSaveRequest(1, "run");
        Assert.True(draft.MergeSaveResult(Result(1, false, Row() with { Color = "#445566" })));
        Assert.Equal("#112233", draft.Languages[0].Color);
        Assert.Equal("#445566", draft.OriginalLanguages[0].Color);
        Assert.True(draft.IsDirty);
        Assert.False(draft.IsSavePending);
    }

    [Fact]
    public void ReviewShowsFieldChangesRemovalAndOrderWithoutTreatingRenameAsRemoval()
    {
        var first = Row();
        var second = Row() with { OriginalName = "Hill", Name = "Hill", Prefix = "hill" };
        var current = first with { Name = "Coastal", Color = "#112233", GrantedToClasses = "tailor" };
        var rows = SetupWizardDialog.LanguageReviewRows([first, second], [current]);
        Assert.Contains(rows, row => row.Title == "Coastal: Text color" && row.Detail == "#68B8A2  >  #112233");
        Assert.Contains(rows, row => row.Title == "Coastal: Classes" && row.Detail == "hunter  >  tailor");
        Assert.Contains(rows, row => row.Title == "Remove language: Hill");
        Assert.DoesNotContain(rows, row => row.Title == "Remove language: Harbor");
    }

    [Fact]
    public void TimedOutRenameReadbackRebasesIdentityAndPreservesNewerEdits()
    {
        var draft = new SetupWizardDraft([new() { Key = "EnableGlobalOOC", Value = "1" }], Canonical(Row()));
        draft.Set("EnableGlobalOOC", "0");
        draft.Languages[0].Name = "Coastal";
        draft.CreateSaveRequest(1, "run");
        draft.Languages[0].Name = "Mountain";
        draft.Languages[0].Color = "#112233";
        draft.Set("EnableGlobalOOC", "1");

        draft.MarkSaveTimedOut();
        draft.CancelRequest();

        Assert.True(draft.HasUnconfirmedSave);
        Assert.False(draft.IsSavePending);
        Assert.Throws<InvalidOperationException>(() => draft.CreateSaveRequest(2, "run"));
        Assert.False(draft.MergeSaveResult(Result(1, true, Row() with { Name = "Coastal", OriginalName = "Coastal" })));
        var saved = Row() with { Name = "Coastal", OriginalName = "Coastal" };
        var readback = Readback(saved);
        readback.Values = [new() { Key = "EnableGlobalOOC", Value = "0" }];
        Assert.True(draft.MergeSaveReadback(readback));

        Assert.False(draft.HasUnconfirmedSave);
        Assert.Equal("Mountain", draft.Languages[0].Name);
        Assert.Equal("Coastal", draft.Languages[0].OriginalName);
        Assert.Equal("#112233", draft.Languages[0].Color);
        Assert.Equal("1", draft.Get("EnableGlobalOOC"));
        Assert.Equal("0", draft.GetOriginal("EnableGlobalOOC"));
        var retry = draft.CreateSaveRequest(2, "run");
        var current = CurrentConfig(saved);
        current.EnableGlobalOOC = false;
        Assert.True(new SetupWizardState(current).TryValidatePatch(current, retry, out var next, out _, out _, out var errors), string.Join("; ", errors));
        Assert.Equal("Mountain", next.Languages[0].Name);
        Assert.True(next.EnableGlobalOOC);
    }

    [Fact]
    public void TimedOutSaveReadbackRetainsUnsavedRenameWhenServerDidNotApplyIt()
    {
        var draft = new SetupWizardDraft([], Canonical(Row()));
        draft.Languages[0].Name = "Coastal";
        draft.CreateSaveRequest(1, "run");
        draft.MarkSaveTimedOut();

        Assert.True(draft.MergeSaveReadback(Readback(Row())));

        Assert.Equal("Coastal", draft.Languages[0].Name);
        Assert.Equal("Harbor", draft.Languages[0].OriginalName);
        Assert.True(draft.IsDirty);
        var retry = draft.CreateSaveRequest(2, "run");
        var current = CurrentConfig(Row());
        Assert.True(new SetupWizardState(current).TryValidatePatch(current, retry, out _, out _, out _, out var errors), string.Join("; ", errors));
    }

    [Fact]
    public void TimedOutSavedRenameWithNoNewerEditAdoptsCanonicalServerValues()
    {
        var draft = new SetupWizardDraft([], Canonical(Row()));
        draft.Languages[0].Name = " Coastal ";
        draft.Languages[0].Syllables = "sho, sho, ra";
        draft.CreateSaveRequest(1, "run");
        draft.MarkSaveTimedOut();

        Assert.True(draft.MergeSaveReadback(Readback(Row() with { Name = "Coastal", OriginalName = "Coastal" })));

        Assert.Equal("Coastal", draft.Languages[0].Name);
        Assert.Equal("sho, ra", draft.Languages[0].Syllables);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void TimedOutRenameThenLocalRevertStillRebasesTheSubmittedIdentity()
    {
        var draft = new SetupWizardDraft([], Canonical(Row()));
        draft.Languages[0].Name = "Coastal";
        draft.CreateSaveRequest(1, "run");
        draft.Languages[0].Name = "Harbor";
        draft.MarkSaveTimedOut();

        Assert.True(draft.MergeSaveReadback(Readback(Row() with { Name = "Coastal", OriginalName = "Coastal" })));

        Assert.Equal("Harbor", draft.Languages[0].Name);
        Assert.Equal("Coastal", draft.Languages[0].OriginalName);
        Assert.True(draft.IsDirty);
    }

    [Fact]
    public void LanguageEditAfterScalarSubmissionDoesNotBecomeATimedOutLanguageSave()
    {
        var draft = new SetupWizardDraft([new() { Key = "EnableGlobalOOC", Value = "1" }], Canonical(Row()));
        draft.Set("EnableGlobalOOC", "0");
        Assert.Empty(draft.CreateSaveRequest(1, "run").Languages);
        draft.Languages[0].Name = "Coastal";
        draft.MarkSaveTimedOut();
        var readback = Readback(Row());
        readback.Values = [new() { Key = "EnableGlobalOOC", Value = "0" }];

        Assert.True(draft.MergeSaveReadback(readback));

        Assert.Equal("Coastal", draft.Languages[0].Name);
        Assert.Equal("Harbor", draft.Languages[0].OriginalName);
        Assert.Equal(new[] { "Languages" }, draft.ChangedKeys);
    }

    [Fact]
    public void FailedReadbackKeepsUnknownOutcomeUntilAuthoritativeOpenArrives()
    {
        var draft = new SetupWizardDraft([], Canonical(Row()));
        draft.Languages[0].Name = "Coastal";
        draft.CreateSaveRequest(1, "run");
        draft.MarkSaveTimedOut();

        Assert.False(draft.MergeSaveReadback(Result(2, true, Row())));
        var denied = Readback(Row());
        denied.Success = false;
        Assert.False(draft.MergeSaveReadback(denied));
        Assert.True(draft.HasUnconfirmedSave);
        Assert.True(draft.MergeSaveReadback(Readback(Row())));
        Assert.False(draft.HasUnconfirmedSave);
    }

    [Fact]
    public void ResetAfterForeignRenameConflictAdoptsFreshIdentitiesAndKeepsScalarEdits()
    {
        var draft = new SetupWizardDraft([new() { Key = "EnableGlobalOOC", Value = "1" }], Canonical(Row()));
        draft.Set("EnableGlobalOOC", "0");
        draft.Languages[0].Color = "#112233";
        draft.CreateSaveRequest(1, "run");
        var fresh = Row() with { Name = "Coastal", OriginalName = "Coastal" };
        var failure = Result(1, false, Canonical(fresh)[0]);
        failure.ConflictKeys = ["Languages"];
        Assert.True(draft.MergeSaveResult(failure));
        Assert.Equal("Harbor", draft.Languages[0].OriginalName);

        draft.ResetLanguages();

        Assert.False(draft.LanguagesChanged);
        Assert.Equal("Coastal", draft.Languages[0].OriginalName);
        Assert.Equal("0", draft.Get("EnableGlobalOOC"));
        draft.Languages[0].Color = "#778899";
        var retry = draft.CreateSaveRequest(2, "run");
        var current = CurrentConfig(fresh);
        Assert.True(new SetupWizardState(current).TryValidatePatch(current, retry, out var next, out _, out _, out var errors), string.Join("; ", errors));
        Assert.Equal("Coastal", next.Languages[0].Name);
        Assert.Equal("#778899", next.Languages[0].Color);
    }

    private static ModConfig CurrentConfig(LanguageConfigEntryMessage entry)
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        Assert.True(LanguageConfigAdmin.TryApplyEntries(config, [entry], out var errors), string.Join("; ", errors));
        return config;
    }

    private static List<LanguageConfigEntryMessage> Canonical(LanguageConfigEntryMessage entry) =>
        LanguageConfigAdmin.BuildEntries(CurrentConfig(entry));

    private static TheBasicsSetupWizardResultMessage Readback(LanguageConfigEntryMessage row) => new()
    {
        Kind = SetupWizardResultKind.Open,
        RequestId = 2,
        RunId = "run",
        Success = true,
        Languages = Canonical(row)
    };

    private static TheBasicsSetupWizardResultMessage Result(long id, bool success, LanguageConfigEntryMessage row) => new()
    {
        Kind = SetupWizardResultKind.SaveResult,
        RequestId = id,
        RunId = "run",
        Success = success,
        Languages = [row]
    };
}
