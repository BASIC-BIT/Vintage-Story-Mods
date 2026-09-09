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
        var original = new TheBasicsNotesViewMessage { Success = true, TargetPlayerUid = "player", Scope = "admin", AdminNotes = [new() { Text = "unsaved" }] };
        method.Invoke(dialog, [original, true]);
        var stateField = typeof(PlayerNotesDialog).GetField("_draftState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((DialogDraftState)stateField.GetValue(dialog)!).TryBeginRequest("submitted");
        method.Invoke(dialog, [new TheBasicsNotesViewMessage { Success = false }, false]);
        typeof(PlayerNotesDialog).GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog).Should().BeSameAs(original);
        var notes = (List<PlayerNoteEntryMessage>)typeof(PlayerNotesDialog).GetField("_adminNotes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
        notes.Should().ContainSingle().Which.Text.Should().Be("unsaved");
        ((DialogDraftState)stateField.GetValue(dialog)!).TryBeginRequest("retry").Should().BeTrue();
    }

    [Fact]
    public void TimedOutLanguageSave_RetainsRenameIdentityUntilAuthoritativeRefresh()
    {
        var dialog = (LanguageConfigDialog)RuntimeHelpers.GetUninitializedObject(typeof(LanguageConfigDialog));
        var entry = new LanguageConfigEntryMessage { OriginalName = "Old", Name = "New" };
        var reloads = 0;
        SetField(dialog, "_languages", new List<LanguageConfigEntryMessage> { entry });
        SetField(dialog, "_selectedIndex", -1);
        SetField(dialog, "_draftState", new DialogDraftState("loaded"));
        SetField(dialog, "_submittedNames", LanguageConfigDialog.CaptureSubmittedNames([entry]));
        SetField(dialog, "_onReload", (Action)(() => reloads++));

        dialog.OnRequestTimedOut();
        dialog.OnRequestTimedOut();

        reloads.Should().Be(1);
        GetField<bool>(dialog, "_requiresAuthoritativeRefresh").Should().BeTrue();
        GetField<Dictionary<LanguageConfigEntryMessage, string>>(dialog, "_submittedNames")
            .Should().ContainKey(entry).WhoseValue.Should().Be("New");
    }

    [Fact]
    public void TimedOutNotesSave_RetainsAssignedIdLookupUntilAuthoritativeRefresh()
    {
        var dialog = (PlayerNotesDialog)RuntimeHelpers.GetUninitializedObject(typeof(PlayerNotesDialog));
        var note = new PlayerNoteEntryMessage { Id = string.Empty, Title = "Draft", Text = "Body" };
        var submitted = new Dictionary<PlayerNoteEntryMessage, PlayerNoteEntryMessage> { [note] = new() { Title = note.Title, Text = note.Text } };
        var reloads = new List<TheBasicsNotesSaveMessage>();
        SetField(dialog, "_view", new TheBasicsNotesViewMessage { Success = true, Scope = "admin", TargetPlayerUid = "player" });
        SetField(dialog, "_adminNotes", new List<PlayerNoteEntryMessage> { note });
        SetField(dialog, "_personalNotes", new List<PlayerNoteEntryMessage>());
        SetField(dialog, "_adminLedger", new AdminNoteLedgerMessage());
        SetField(dialog, "_personalLedger", new PersonalNoteLedgerMessage());
        SetField(dialog, "_draftState", new DialogDraftState("loaded"));
        SetField(dialog, "_submittedNotes", submitted);
        SetField(dialog, "_onReload", (Action<TheBasicsNotesSaveMessage>)(message => reloads.Add(message)));

        dialog.OnRequestTimedOut();
        dialog.OnRequestTimedOut();

        reloads.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Scope = "admin",
            TargetPlayerUid = "player",
            Reload = true
        });
        GetField<bool>(dialog, "_requiresAuthoritativeRefresh").Should().BeTrue();
        GetField<Dictionary<PlayerNoteEntryMessage, PlayerNoteEntryMessage>>(dialog, "_submittedNotes")
            .Should().BeSameAs(submitted);
    }

    private static void SetField<T>(object instance, string name, T value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    }
}
