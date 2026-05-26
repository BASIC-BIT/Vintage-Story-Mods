namespace thebasics.ModSystems.ChatHistory.Models;

public sealed class ChatHistoryRetentionResult
{
    public int OriginalCount { get; set; }
    public int KeptCount { get; set; }
    public int RemovedCount => OriginalCount - KeptCount;
}
