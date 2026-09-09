using FluentAssertions;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.Notes.Models;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class NotesDraftReconciliationTests
{
    [Fact]
    public void SaveAcknowledgement_PreservesNewTextAndAdoptsServerIdentity()
    {
        var note = new PlayerNoteEntryMessage { Title = "new title", Text = "new text" };
        var draft = new List<PlayerNoteEntryMessage> { note };
        var submitted = new Dictionary<PlayerNoteEntryMessage, PlayerNoteEntryMessage>
        {
            [note] = new() { Title = "title", Text = " text\r\n" }
        };
        var saved = new List<PlayerNoteEntryMessage>
        {
            new() { Id = "server-id", Title = "title", Text = "text", CreatedUtc = "created", AuthorName = "author" }
        };
        PlayerNotesDialog.ReconcileSavedNotes(draft, submitted, saved);
        draft[0].Id.Should().Be("server-id");
        draft[0].Title.Should().Be("new title");
        draft[0].Text.Should().Be("new text");
        draft[0].CreatedUtc.Should().Be("created");
        draft[0].AuthorName.Should().Be("author");
        saved[0].Text.Should().Be("text");
    }

    [Fact]
    public void SaveAcknowledgement_DoesNotReinsertDeletedNotesOrMatchAnExistingIdenticalNote()
    {
        var existing = new PlayerNoteEntryMessage { Id = "existing", Text = "same" };
        var added = new PlayerNoteEntryMessage { Text = "edited" };
        var deleted = new PlayerNoteEntryMessage { Text = "deleted" };
        var draft = new List<PlayerNoteEntryMessage> { added, existing };
        var submitted = new Dictionary<PlayerNoteEntryMessage, PlayerNoteEntryMessage>
        {
            [existing] = new() { Id = "existing", Text = "same" },
            [added] = new() { Text = "same" },
            [deleted] = new() { Text = "deleted" }
        };
        PlayerNotesDialog.ReconcileSavedNotes(draft, submitted,
            [new() { Id = "existing", Text = "same" }, new() { Id = "new-id", Text = "same" }, new() { Id = "deleted-id", Text = "deleted" }]);
        draft.Should().HaveCount(2);
        draft[0].Id.Should().Be("new-id");
        draft[0].Text.Should().Be("edited");
    }
}
