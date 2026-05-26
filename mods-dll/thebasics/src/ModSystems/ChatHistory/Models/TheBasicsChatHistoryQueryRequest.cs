using ProtoBuf;

namespace thebasics.ModSystems.ChatHistory.Models;

[ProtoContract]
public class TheBasicsChatHistoryQueryRequest
{
    [ProtoMember(1)]
    public string SearchText { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Player { get; set; } = string.Empty;

    [ProtoMember(3)]
    public int ChannelId { get; set; }

    [ProtoMember(4)]
    public bool HasChannelId { get; set; }

    [ProtoMember(5)]
    public string ChatKind { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string ProximityMode { get; set; } = string.Empty;

    [ProtoMember(7)]
    public string Language { get; set; } = string.Empty;

    [ProtoMember(8)]
    public string FromUtc { get; set; } = string.Empty;

    [ProtoMember(9)]
    public string ToUtc { get; set; } = string.Empty;

    [ProtoMember(10)]
    public int Offset { get; set; }

    [ProtoMember(11)]
    public int Limit { get; set; }

    [ProtoMember(12)]
    public bool Export { get; set; }
}
