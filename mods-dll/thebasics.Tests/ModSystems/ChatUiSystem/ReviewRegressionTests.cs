using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.Notes.Models;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class ReviewRegressionTests
{
    [Fact]
    public void DuplicateSheetIds_AreIncludedInSnapshotWithoutThrowing()
    {
        var view = new CharacterSheetViewMessage { Fields = [
            new() { FieldId = "duplicate", CanEdit = true, Value = "first" },
            new() { FieldId = "duplicate", CanEdit = true, Value = "second" }] };
        var method = typeof(CharacterSheetDialog).GetMethod("SnapshotValues", BindingFlags.Static | BindingFlags.NonPublic)!;
        var snapshot = (string)method.Invoke(null, [view])!;
        snapshot.Should().Contain("first").And.Contain("second");
    }

    [Fact]
    public void SecondRename_UsesAcknowledgedNameWithoutReplacingNewerInput()
    {
        var entry = new LanguageConfigEntryMessage { OriginalName = "Old", Name = "Newest", Description = "new text" };
        LanguageConfigDialog.RebaseSavedNames([entry], new() { [entry] = " New " },
            [new() { OriginalName = "New", Name = "New", Description = "old text" }]);
        entry.OriginalName.Should().Be("New");
        entry.Name.Should().Be("Newest");
        entry.Description.Should().Be("new text");
    }

    [Fact]
    public void ReplacingNotesWithError_ReleasesPendingRequest()
    {
        var dialog = (PlayerNotesDialog)RuntimeHelpers.GetUninitializedObject(typeof(PlayerNotesDialog));
        var method = typeof(PlayerNotesDialog).GetMethod("SetDraft", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(dialog, [new TheBasicsNotesViewMessage { Success = true, TargetPlayerUid = "player" }, true]);
        var stateField = typeof(PlayerNotesDialog).GetField("_draftState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((DialogDraftState)stateField.GetValue(dialog)!).TryBeginRequest("submitted");
        method.Invoke(dialog, [new TheBasicsNotesViewMessage { Success = false }, false]);
        ((DialogDraftState)stateField.GetValue(dialog)!).TryBeginRequest("retry").Should().BeTrue();
    }
}
