namespace thebasics.ModSystems.ChatHistory.Models;

public sealed record ChatHistoryEntry
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public string TimestampUtc { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ChatKind { get; set; } = string.Empty;
    public string SenderPlayerUid { get; set; } = string.Empty;
    public string SenderPlayerName { get; set; } = string.Empty;
    public string SenderNickname { get; set; } = string.Empty;
    public int ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public string ProximityMode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public string FormattedMessage { get; set; } = string.Empty;
    public string RawEventMessage { get; set; } = string.Empty;
    public string ClientData { get; set; } = string.Empty;
    public bool IsFromCommand { get; set; }
    public bool IsPlayerChat { get; set; }
    public int RecipientCount { get; set; }
    public int PendingRecipientCount { get; set; }
    public double? SenderX { get; set; }
    public double? SenderY { get; set; }
    public double? SenderZ { get; set; }
    public double? PlacedX { get; set; }
    public double? PlacedY { get; set; }
    public double? PlacedZ { get; set; }
}
