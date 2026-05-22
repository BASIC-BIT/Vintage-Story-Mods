using FluentAssertions;
using thebasics.ModSystems.Notes;
using thebasics.ModSystems.Notes.Models;

namespace thebasics.Tests.ModSystems.Notes;

public class PlayerNotesStoreTests
{
    [Fact]
    public void Normalize_InitializesCollectionsAndRemovesNullEntries()
    {
        var data = new PlayerNotesData
        {
            Version = 0,
            AdminNotes = [null!, new PlayerNoteEntryMessage { Id = "admin-1" }],
            AdminLedgers = [null!, new AdminNoteLedgerMessage { TargetPlayerUid = "target-1" }],
            PersonalLedgers = [null!, new PersonalNoteLedgerMessage { AuthorPlayerUid = "author-1", TargetPlayerUid = "target-1" }],
            PersonalNotes = [null!, new PlayerNoteEntryMessage { Id = "personal-1" }]
        };

        var normalized = PlayerNotesStore.Normalize(data);

        normalized.Version.Should().Be(1);
        normalized.AdminNotes.Should().ContainSingle(note => note.Id == "admin-1");
        normalized.AdminLedgers.Should().ContainSingle(ledger => ledger.TargetPlayerUid == "target-1");
        normalized.PersonalLedgers.Should().ContainSingle(ledger => ledger.AuthorPlayerUid == "author-1" && ledger.TargetPlayerUid == "target-1");
        normalized.PersonalNotes.Should().ContainSingle(note => note.Id == "personal-1");
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllNoteBuckets()
    {
        var path = CreateTempStorePath(out var tempDir);
        try
        {
            var store = new PlayerNotesStore(path);
            var saved = new PlayerNotesData
            {
                AdminNotes = [new PlayerNoteEntryMessage { Id = "admin-1", Kind = PlayerNotesConstants.KindAdmin, Title = "Staff title", Text = "Staff note" }],
                AdminLedgers = [new AdminNoteLedgerMessage { TargetPlayerUid = "target-1", Text = "Ledger text" }],
                PersonalLedgers = [new PersonalNoteLedgerMessage { AuthorPlayerUid = "author-1", TargetPlayerUid = "target-1", Text = "Private freeform text" }],
                PersonalNotes = [new PlayerNoteEntryMessage { Id = "personal-1", Kind = PlayerNotesConstants.KindPersonal, Text = "Personal note" }]
            };

            store.Save(saved).Should().BeTrue();

            var loaded = store.Load();
            loaded.AdminNotes.Should().ContainSingle(note => note.Id == "admin-1" && note.Title == "Staff title" && note.Text == "Staff note");
            loaded.AdminLedgers.Should().ContainSingle(ledger => ledger.TargetPlayerUid == "target-1" && ledger.Text == "Ledger text");
            loaded.PersonalLedgers.Should().ContainSingle(ledger => ledger.AuthorPlayerUid == "author-1" && ledger.TargetPlayerUid == "target-1" && ledger.Text == "Private freeform text");
            loaded.PersonalNotes.Should().ContainSingle(note => note.Id == "personal-1" && note.Text == "Personal note");
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void Load_WhenJsonIsInvalid_ReturnsEmptyDataAndWarns()
    {
        var path = CreateTempStorePath(out var tempDir);
        var warnings = new List<string>();
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(path, "not json");
            var store = new PlayerNotesStore(path, warnings.Add);

            var loaded = store.Load();

            loaded.AdminNotes.Should().BeEmpty();
            loaded.AdminLedgers.Should().BeEmpty();
            loaded.PersonalLedgers.Should().BeEmpty();
            loaded.PersonalNotes.Should().BeEmpty();
            warnings.Should().ContainSingle(message => message.Contains("Failed to load player notes store"));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static string CreateTempStorePath(out string tempDir)
    {
        tempDir = Path.Combine(Path.GetTempPath(), "thebasics-tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(tempDir, "notes.json");
    }

    private static void DeleteTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
