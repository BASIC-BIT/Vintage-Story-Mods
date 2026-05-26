using FluentAssertions;
using thebasics.ModSystems.ChatHistory;
using thebasics.ModSystems.ChatHistory.Models;

namespace thebasics.Tests.ModSystems.ChatHistory;

public class ChatHistoryStoreTests
{
    [Fact]
    public void AppendAndLoad_RoundTripsStructuredEntries()
    {
        var path = CreateTempStorePath(out var tempDir);
        try
        {
            var store = new ChatHistoryStore(path);
            var entry = CreateEntry("1", "2026-05-24T12:00:00Z", "Alice", "hello there") with
            {
                ChatKind = ChatHistoryConstants.KindSpeech,
                ProximityMode = "Whisper",
                Language = "Common",
                RecipientCount = 0,
                SenderX = 1.25,
                SenderY = 2.5,
                SenderZ = 3.75
            };

            store.Append([entry]);

            var loaded = store.LoadAll();
            loaded.Should().ContainSingle();
            loaded[0].Id.Should().Be("1");
            loaded[0].SenderPlayerName.Should().Be("Alice");
            loaded[0].MessageText.Should().Be("hello there");
            loaded[0].RecipientCount.Should().Be(0);
            loaded[0].ProximityMode.Should().Be("Whisper");
            loaded[0].Language.Should().Be("Common");
            loaded[0].SenderX.Should().Be(1.25);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void LoadAll_SkipsCorruptLinesAndKeepsRecoverableEntries()
    {
        var path = CreateTempStorePath(out var tempDir);
        var warnings = new List<string>();
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllLines(path,
            [
                "not json",
                "{\"Id\":\"ok\",\"TimestampUtc\":\"2026-05-24T12:00:00Z\",\"MessageText\":\"kept\"}"
            ]);
            var store = new ChatHistoryStore(path, warnings.Add);

            var loaded = store.LoadAll();

            loaded.Should().ContainSingle(entry => entry.Id == "ok" && entry.MessageText == "kept");
            warnings.Should().ContainSingle(message => message.Contains("Skipped corrupt chat history line 1"));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void Query_FiltersByTextPlayerKindAndTimeRange()
    {
        var path = CreateTempStorePath(out var tempDir);
        try
        {
            var store = new ChatHistoryStore(path);
            store.Append(
            [
                CreateEntry("1", "2026-05-24T12:00:00Z", "Alice", "secret whisper") with { ChatKind = ChatHistoryConstants.KindSpeech },
                CreateEntry("2", "2026-05-24T13:00:00Z", "Bob", "normal words") with { ChatKind = ChatHistoryConstants.KindOoc },
                CreateEntry("3", "2026-05-24T14:00:00Z", "Alice", "later whisper") with { ChatKind = ChatHistoryConstants.KindSpeech }
            ]);

            var result = store.Query(new ChatHistoryQuery
            {
                SearchText = "whisper",
                Player = "ali",
                ChatKind = ChatHistoryConstants.KindSpeech,
                FromUtc = "2026-05-24T12:30:00Z",
                ToUtc = "2026-05-24T14:30:00Z",
                Limit = 10
            }, defaultLimit: 50, maxLimit: 100);

            result.TotalMatches.Should().Be(1);
            result.Entries.Should().ContainSingle(entry => entry.Id == "3");
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void Query_ReturnsNewestFirstAndPaginates()
    {
        var path = CreateTempStorePath(out var tempDir);
        try
        {
            var store = new ChatHistoryStore(path);
            store.Append(
            [
                CreateEntry("1", "2026-05-24T12:00:00Z", "Alice", "first"),
                CreateEntry("2", "2026-05-24T13:00:00Z", "Alice", "second"),
                CreateEntry("3", "2026-05-24T14:00:00Z", "Alice", "third")
            ]);

            var result = store.Query(new ChatHistoryQuery { Offset = 1, Limit = 1 }, defaultLimit: 50, maxLimit: 100);

            result.TotalMatches.Should().Be(3);
            result.Entries.Should().ContainSingle(entry => entry.Id == "2");
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void ApplyRetention_AppliesAgeAndCountCaps()
    {
        var path = CreateTempStorePath(out var tempDir);
        try
        {
            var store = new ChatHistoryStore(path);
            store.Append(
            [
                CreateEntry("old", "2026-05-20T12:00:00Z", "Alice", "old"),
                CreateEntry("middle", "2026-05-23T12:00:00Z", "Alice", "middle"),
                CreateEntry("new", "2026-05-24T12:00:00Z", "Alice", "new")
            ]);

            var result = store.ApplyRetention(retentionDays: 3, maxEntries: 1, utcNow: DateTime.Parse("2026-05-25T12:00:00Z").ToUniversalTime());

            result.OriginalCount.Should().Be(3);
            result.KeptCount.Should().Be(1);
            store.LoadAll().Should().ContainSingle(entry => entry.Id == "new");
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void ApplyRetention_WithZeroCapsKeepsEverything()
    {
        var path = CreateTempStorePath(out var tempDir);
        try
        {
            var store = new ChatHistoryStore(path);
            store.Append(
            [
                CreateEntry("1", "2026-05-20T12:00:00Z", "Alice", "old"),
                CreateEntry("2", "2026-05-24T12:00:00Z", "Alice", "new")
            ]);

            var result = store.ApplyRetention(retentionDays: 0, maxEntries: 0, utcNow: DateTime.Parse("2026-05-25T12:00:00Z").ToUniversalTime());

            result.RemovedCount.Should().Be(0);
            store.LoadAll().Should().HaveCount(2);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void PurgeBefore_RemovesEntriesOlderThanCutoff()
    {
        var path = CreateTempStorePath(out var tempDir);
        try
        {
            var store = new ChatHistoryStore(path);
            store.Append(
            [
                CreateEntry("old", "2026-05-20T12:00:00Z", "Alice", "old"),
                CreateEntry("new", "2026-05-24T12:00:00Z", "Alice", "new")
            ]);

            var removed = store.PurgeBefore(DateTime.Parse("2026-05-22T00:00:00Z").ToUniversalTime());

            removed.Should().Be(1);
            store.LoadAll().Should().ContainSingle(entry => entry.Id == "new");
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void Export_WritesMatchingEntriesAsJsonLines()
    {
        var path = CreateTempStorePath(out var tempDir);
        try
        {
            var exportPath = Path.Combine(tempDir, "exports", "chat.jsonl");
            var store = new ChatHistoryStore(path);
            store.Append(
            [
                CreateEntry("1", "2026-05-24T12:00:00Z", "Alice", "secret whisper"),
                CreateEntry("2", "2026-05-24T13:00:00Z", "Bob", "normal words")
            ]);

            var result = store.Export(new ChatHistoryQuery { SearchText = "secret" }, exportPath);

            result.ExportedCount.Should().Be(1);
            result.Path.Should().Be(exportPath);
            File.ReadAllLines(exportPath).Should().ContainSingle(line => line.Contains("secret whisper"));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static ChatHistoryEntry CreateEntry(string id, string timestamp, string playerName, string message)
    {
        return new ChatHistoryEntry
        {
            Id = id,
            TimestampUtc = timestamp,
            Source = ChatHistoryConstants.SourceTheBasics,
            ChatKind = ChatHistoryConstants.KindPlayerChat,
            SenderPlayerUid = playerName.ToLowerInvariant() + "-uid",
            SenderPlayerName = playerName,
            ChannelId = 1,
            ChannelName = "General",
            MessageText = message
        };
    }

    private static string CreateTempStorePath(out string tempDir)
    {
        tempDir = Path.Combine(Path.GetTempPath(), "thebasics-tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(tempDir, "chat-history.jsonl");
    }

    private static void DeleteTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
