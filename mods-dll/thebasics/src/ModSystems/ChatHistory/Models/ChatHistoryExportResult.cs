namespace thebasics.ModSystems.ChatHistory.Models;

public sealed class ChatHistoryExportResult
{
    public string Path { get; set; } = string.Empty;
    public int ExportedCount { get; set; }
}
