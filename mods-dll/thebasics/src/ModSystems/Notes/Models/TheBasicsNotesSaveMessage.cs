using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.Notes.Models;

[ProtoContract]
public class TheBasicsNotesSaveMessage
{
    [ProtoMember(1)]
    public string Scope { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string TargetPlayerUid { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string TargetQuery { get; set; } = string.Empty;

    [ProtoMember(4)]
    public bool Reload { get; set; }

    [ProtoMember(5)]
    public List<PlayerNoteEntryMessage> AdminNotes { get; set; } = new();

    [ProtoMember(6)]
    public AdminNoteLedgerMessage AdminLedger { get; set; } = new();

    [ProtoMember(7)]
    public List<PlayerNoteEntryMessage> PersonalNotes { get; set; } = new();

    [ProtoMember(8)]
    public PersonalNoteLedgerMessage PersonalLedger { get; set; } = new();
}
