using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.Notes.Models;

[ProtoContract]
public class TheBasicsNotesViewMessage
{
    [ProtoMember(1)]
    public bool Success { get; set; } = true;

    [ProtoMember(2)]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Scope { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string TargetPlayerName { get; set; } = string.Empty;

    [ProtoMember(6)]
    public bool ShowAdminNotes { get; set; }

    [ProtoMember(7)]
    public bool ShowAdminLedger { get; set; }

    [ProtoMember(8)]
    public bool ShowPersonalNotes { get; set; }

    [ProtoMember(9)]
    public bool CanEditAdminNotes { get; set; }

    [ProtoMember(10)]
    public bool CanEditAdminLedger { get; set; }

    [ProtoMember(11)]
    public bool CanEditPersonalNotes { get; set; }

    [ProtoMember(12)]
    public List<PlayerNoteEntryMessage> AdminNotes { get; set; } = new();

    [ProtoMember(13)]
    public AdminNoteLedgerMessage AdminLedger { get; set; } = new();

    [ProtoMember(14)]
    public List<PlayerNoteEntryMessage> PersonalNotes { get; set; } = new();

    [ProtoMember(15)]
    public PersonalNoteLedgerMessage PersonalLedger { get; set; } = new();

    [ProtoMember(16)]
    public int MaxNoteLength { get; set; }

    [ProtoMember(17)]
    public int MaxFreeformNoteLength { get; set; }
}
