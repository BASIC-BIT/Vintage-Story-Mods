using System.Collections.Generic;
using ProtoBuf;

namespace thebasics.ModSystems.ChatHistory.Models;

[ProtoContract]
public class TheBasicsChatHistoryResultMessage
{
    [ProtoMember(1)]
    public bool Success { get; set; } = true;

    [ProtoMember(2)]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(3)]
    public TheBasicsChatHistoryQueryRequest Query { get; set; } = new();

    [ProtoMember(4)]
    public List<TheBasicsChatHistoryEntryMessage> Entries { get; set; } = new();

    [ProtoMember(5)]
    public int TotalMatches { get; set; }

    [ProtoMember(6)]
    public int Offset { get; set; }

    [ProtoMember(7)]
    public int Limit { get; set; }

    [ProtoMember(8)]
    public bool CanManage { get; set; }

    [ProtoMember(9)]
    public int ExportedCount { get; set; }

    [ProtoMember(10)]
    public string ExportPath { get; set; } = string.Empty;
}
