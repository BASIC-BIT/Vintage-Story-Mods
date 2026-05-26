using System.Collections.Generic;

namespace thebasics.ModSystems.ChatHistory.Models;

public sealed class ChatHistoryQueryResult
{
    public List<ChatHistoryEntry> Entries { get; set; } = new();
    public int TotalMatches { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
}
