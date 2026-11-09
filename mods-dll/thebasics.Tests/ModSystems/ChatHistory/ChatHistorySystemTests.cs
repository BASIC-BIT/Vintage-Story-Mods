using System.Reflection;
using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.ChatHistory;
using thebasics.ModSystems.ChatHistory.Models;

namespace thebasics.Tests.ModSystems.ChatHistory;

public class ChatHistorySystemTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "thebasics-tests", Guid.NewGuid().ToString("N"));
    private readonly ChatHistoryStore _store;
    private readonly ChatHistorySystem _system;
    private string StorePath => Path.Combine(_directory, "chat-history.jsonl");

    public ChatHistorySystemTests()
    {
        Directory.CreateDirectory(_directory);
        _store = new ChatHistoryStore(StorePath);
        _system = new ChatHistorySystem { Config = new ModConfig() };
        typeof(ChatHistorySystem).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_system, _store);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(false, 1, 0)]
    [InlineData(false, 0, 1)]
    [InlineData(true, 0, 0)]
    public void WorldSave_WithoutAutomaticRetention_DoesNotReadExistingHistory(bool enabled, int days, int maxEntries)
    {
        _system.Config.EnableChatHistory = enabled;
        _system.Config.ChatHistoryRetentionDays = days;
        _system.Config.ChatHistoryMaxEntries = maxEntries;
        _store.Append([Entry("existing", DateTime.UtcNow.AddDays(-10))]);
        var original = File.ReadAllBytes(StorePath);

        // An exclusive handle makes any attempted history scan fail deterministically.
        using (File.Open(StorePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Action save = SaveWorld;
            save.Should().NotThrow();
        }

        File.ReadAllBytes(StorePath).Should().Equal(original);
    }

    [Fact]
    public void WorldSave_AfterDisablingHistory_FlushesPendingEntriesAndPreservesOldHistory()
    {
        _store.Append([Entry("old", DateTime.UtcNow.AddDays(-10))]);
        Enqueue(Entry("pending", DateTime.UtcNow));
        _system.Config.EnableChatHistory = false;
        _system.Config.ChatHistoryRetentionDays = 1;
        _system.Config.ChatHistoryMaxEntries = 1;

        SaveWorld();
        SaveWorld();

        _store.LoadAll().Select(entry => entry.Id).Should().Equal("old", "pending");
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void WorldSave_WhenEnabled_FlushesBeforeApplyingRetention(int days, int maxEntries)
    {
        _system.Config.EnableChatHistory = true;
        _system.Config.ChatHistoryRetentionDays = days;
        _system.Config.ChatHistoryMaxEntries = maxEntries;
        _store.Append([Entry("old", DateTime.UtcNow.AddDays(-10))]);
        Enqueue(Entry("pending", DateTime.UtcNow));

        SaveWorld();

        _store.LoadAll().Should().ContainSingle(entry => entry.Id == "pending");
    }

    [Fact]
    public void ExplicitRetention_WithUnlimitedHistory_StillFlushesAndReturnsCounts()
    {
        _system.Config.ChatHistoryRetentionDays = 0;
        _system.Config.ChatHistoryMaxEntries = 0;
        _store.Append([Entry("old", DateTime.UtcNow.AddDays(-10))]);
        Enqueue(Entry("pending", DateTime.UtcNow));

        var result = _system.ApplyRetention();

        result.OriginalCount.Should().Be(2);
        result.KeptCount.Should().Be(2);
        result.RemovedCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_AfterDisablingHistory_FlushesPendingEntries()
    {
        Enqueue(Entry("pending", DateTime.UtcNow));
        _system.Config.EnableChatHistory = false;

        _system.Dispose();

        _store.LoadAll().Should().ContainSingle(entry => entry.Id == "pending");
    }

    private void SaveWorld() => Invoke("Event_GameWorldSave");
    private void Enqueue(ChatHistoryEntry entry) => Invoke("Enqueue", entry);
    private void Invoke(string method, params object[] arguments) =>
        typeof(ChatHistorySystem).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_system, arguments);

    private static ChatHistoryEntry Entry(string id, DateTime timestamp) => new()
    {
        Id = id,
        TimestampUtc = timestamp.ToString("O"),
        MessageText = "synthetic test entry"
    };

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
