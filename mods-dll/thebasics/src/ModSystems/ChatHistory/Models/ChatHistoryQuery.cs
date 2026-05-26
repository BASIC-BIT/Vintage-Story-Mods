namespace thebasics.ModSystems.ChatHistory.Models;

public sealed class ChatHistoryQuery
{
    public string SearchText { get; set; } = string.Empty;
    public string Player { get; set; } = string.Empty;
    public int? ChannelId { get; set; }
    public string ChatKind { get; set; } = string.Empty;
    public string ProximityMode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string FromUtc { get; set; } = string.Empty;
    public string ToUtc { get; set; } = string.Empty;
    public int Offset { get; set; }
    public int Limit { get; set; }
}
