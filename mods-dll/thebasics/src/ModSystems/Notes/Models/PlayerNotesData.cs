using System.Collections.Generic;

namespace thebasics.ModSystems.Notes.Models;

public class PlayerNotesData
{
    public int Version { get; set; } = 1;

    public List<PlayerNoteEntryMessage> AdminNotes { get; set; } = new();

    public List<AdminNoteLedgerMessage> AdminLedgers { get; set; } = new();

    public List<PersonalNoteLedgerMessage> PersonalLedgers { get; set; } = new();

    public List<PlayerNoteEntryMessage> PersonalNotes { get; set; } = new();
}
