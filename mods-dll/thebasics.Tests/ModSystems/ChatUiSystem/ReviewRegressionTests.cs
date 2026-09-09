using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NSubstitute;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.Notes.Models;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class ReviewRegressionTests
{
    [Fact]
    public void MutableLanguageRecord_RemainsFindableAfterEditing()
    {
        var entry = new LanguageConfigEntryMessage { OriginalName = "Old", Name = "New" };
        var submitted = LanguageConfigDialog.CaptureSubmittedNames([entry]);
        entry.Name = "Newest";
        LanguageConfigDialog.RebaseSavedNames([entry], submitted, [new() { OriginalName = "New", Name = "New" }]);
        entry.OriginalName.Should().Be("New");
    }

    [Fact]
    public void ExhaustedTransportRetries_NotifyTheRequestOwner()
    {
        var channel = Substitute.For<IClientNetworkChannel>();
        var api = Substitute.For<ICoreClientAPI>();
        var failed = false;
        var draft = new DialogDraftState("loaded");
        draft.TryBeginRequest("submitted");
        using var safe = new SafeClientNetworkChannel(channel, api, new() { MaxRetries = 0 });
        safe.SendPacketSafely(new CharacterSheetSaveRequest(), () => { failed = true; draft.CancelRequest(); });
        failed.Should().BeTrue();
        safe.PendingActionCount.Should().Be(0);
        draft.TryBeginRequest("retry").Should().BeTrue();
        draft.IsDirty("submitted").Should().BeTrue();
    }

    [Fact]
    public void ConnectedSend_DoesNotReleaseRequestBeforeResponse()
    {
        var channel = Substitute.For<IClientNetworkChannel>();
        channel.Connected.Returns(true);
        var api = Substitute.For<ICoreClientAPI>();
        var failed = false;
        using var safe = new SafeClientNetworkChannel(channel, api);
        safe.SendPacketSafely(new CharacterSheetSaveRequest(), () => failed = true);
        safe.ClearPendingActions();
        failed.Should().BeFalse();
    }

    [Fact]
    public void DuplicateSheetIds_AreIncludedInSnapshotWithoutThrowing()
    {
        var view = new CharacterSheetViewMessage
        {
            Fields = [
            new() { FieldId = "duplicate", CanEdit = true, Value = "first" },
            new() { FieldId = "duplicate", CanEdit = true, Value = "second" }]
        };
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
