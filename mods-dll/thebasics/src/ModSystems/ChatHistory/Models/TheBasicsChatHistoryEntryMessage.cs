using ProtoBuf;

namespace thebasics.ModSystems.ChatHistory.Models;

[ProtoContract]
public class TheBasicsChatHistoryEntryMessage
{
    [ProtoMember(1)]
    public string Id { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string TimestampUtc { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Source { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string ChatKind { get; set; } = string.Empty;

    [ProtoMember(5)]
    public string SenderPlayerUid { get; set; } = string.Empty;

    [ProtoMember(6)]
    public string SenderPlayerName { get; set; } = string.Empty;

    [ProtoMember(7)]
    public string SenderNickname { get; set; } = string.Empty;

    [ProtoMember(8)]
    public int ChannelId { get; set; }

    [ProtoMember(9)]
    public string ChannelName { get; set; } = string.Empty;

    [ProtoMember(10)]
    public string ProximityMode { get; set; } = string.Empty;

    [ProtoMember(11)]
    public string Language { get; set; } = string.Empty;

    [ProtoMember(12)]
    public string MessageText { get; set; } = string.Empty;

    [ProtoMember(13)]
    public string FormattedMessage { get; set; } = string.Empty;

    [ProtoMember(14)]
    public string RawEventMessage { get; set; } = string.Empty;

    [ProtoMember(15)]
    public string ClientData { get; set; } = string.Empty;

    [ProtoMember(16)]
    public bool IsFromCommand { get; set; }

    [ProtoMember(17)]
    public bool IsPlayerChat { get; set; }

    [ProtoMember(18)]
    public int RecipientCount { get; set; }

    [ProtoMember(19)]
    public int PendingRecipientCount { get; set; }

    [ProtoMember(20)]
    public string SenderPosition { get; set; } = string.Empty;

    [ProtoMember(21)]
    public string PlacedPosition { get; set; } = string.Empty;
}
