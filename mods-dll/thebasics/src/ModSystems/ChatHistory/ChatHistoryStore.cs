using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using thebasics.ModSystems.ChatHistory.Models;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ChatHistory;

public sealed class ChatHistoryStore
{
    private const int CurrentVersion = 1;

    private readonly string _path;
    private readonly Action<string> _warning;

    public ChatHistoryStore(ICoreServerAPI api)
    {
        if (api == null) throw new ArgumentNullException(nameof(api));

        var savegameId = SanitizePathPart(api.WorldManager?.SaveGame?.SavegameIdentifier ?? "default");
        var baseDir = Path.Combine(api.GetOrCreateDataPath("ModData"), "thebasics", "chat-history", savegameId);
        _path = Path.Combine(baseDir, "chat-history.jsonl");
        _warning = message => api.Logger.Warning("[thebasics] " + message);
    }

    public ChatHistoryStore(string path, Action<string> warning = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _warning = warning ?? (_ => { });
    }

    public void Append(IEnumerable<ChatHistoryEntry> entries)
    {
        var normalizedEntries = (entries ?? Array.Empty<ChatHistoryEntry>())
            .Where(entry => entry != null)
            .Select(Normalize)
            .ToList();

        if (normalizedEntries.Count == 0)
        {
            return;
        }

        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var writer = new StreamWriter(_path, append: true);
        foreach (var entry in normalizedEntries)
        {
            writer.WriteLine(JsonConvert.SerializeObject(entry, Formatting.None));
        }
    }

    public List<ChatHistoryEntry> LoadAll()
    {
        var entries = new List<ChatHistoryEntry>();
        if (!File.Exists(_path))
        {
            return entries;
        }

        var lineNumber = 0;
        foreach (var line in File.ReadLines(_path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonConvert.DeserializeObject<ChatHistoryEntry>(line);
                if (entry != null)
                {
                    entries.Add(Normalize(entry));
                }
            }
            catch (Exception ex)
            {
                _warning($"Skipped corrupt chat history line {lineNumber}. {ex.Message}");
            }
        }

        return entries;
    }

    public ChatHistoryQueryResult Query(ChatHistoryQuery query, int defaultLimit, int maxLimit)
    {
        query ??= new ChatHistoryQuery();
        var limit = NormalizeLimit(query.Limit, defaultLimit, maxLimit);
        var offset = Math.Max(0, query.Offset);
        var matches = LoadAll()
            .Where(entry => Matches(entry, query))
            .OrderByDescending(GetTimestamp)
            .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
            .ToList();

        return new ChatHistoryQueryResult
        {
            Entries = matches.Skip(offset).Take(limit).ToList(),
            TotalMatches = matches.Count,
            Offset = offset,
            Limit = limit
        };
    }

    public ChatHistoryRetentionResult ApplyRetention(int retentionDays, int maxEntries, DateTime utcNow)
    {
        var entries = LoadAll();
        var kept = entries;

        if (retentionDays > 0)
        {
            var cutoff = utcNow.ToUniversalTime().AddDays(-retentionDays);
            kept = kept.Where(entry => GetTimestamp(entry) >= cutoff).ToList();
        }

        if (maxEntries > 0 && kept.Count > maxEntries)
        {
            kept = kept
                .OrderByDescending(GetTimestamp)
                .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
                .Take(maxEntries)
                .OrderBy(GetTimestamp)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .ToList();
        }

        if (kept.Count != entries.Count)
        {
            Rewrite(kept);
        }

        return new ChatHistoryRetentionResult
        {
            OriginalCount = entries.Count,
            KeptCount = kept.Count
        };
    }

    public int PurgeAll()
    {
        var count = LoadAll().Count;
        Rewrite(Array.Empty<ChatHistoryEntry>());
        return count;
    }

    public int PurgeBefore(DateTime cutoffUtc)
    {
        var entries = LoadAll();
        var kept = entries.Where(entry => GetTimestamp(entry) >= cutoffUtc.ToUniversalTime()).ToList();
        if (kept.Count != entries.Count)
        {
            Rewrite(kept);
        }

        return entries.Count - kept.Count;
    }

    public ChatHistoryExportResult Export(ChatHistoryQuery query, string exportPath)
    {
        query ??= new ChatHistoryQuery();
        var entries = LoadAll()
            .Where(entry => Matches(entry, query))
            .OrderBy(GetTimestamp)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();

        if (query.Offset > 0)
        {
            entries = entries.Skip(query.Offset).ToList();
        }

        if (query.Limit > 0)
        {
            entries = entries.Take(query.Limit).ToList();
        }

        var dir = Path.GetDirectoryName(exportPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (var writer = new StreamWriter(exportPath, append: false))
        {
            foreach (var entry in entries)
            {
                writer.WriteLine(JsonConvert.SerializeObject(Normalize(entry), Formatting.None));
            }
        }

        return new ChatHistoryExportResult
        {
            Path = exportPath,
            ExportedCount = entries.Count
        };
    }

    public static ChatHistoryEntry Normalize(ChatHistoryEntry entry)
    {
        entry ??= new ChatHistoryEntry();
        entry.Version = Math.Max(CurrentVersion, entry.Version);
        entry.Id = string.IsNullOrWhiteSpace(entry.Id) ? GenerateId() : entry.Id.Trim();
        entry.TimestampUtc = NormalizeTimestamp(entry.TimestampUtc);
        entry.Source = NormalizeText(entry.Source);
        entry.ChatKind = NormalizeText(entry.ChatKind);
        entry.SenderPlayerUid = NormalizeText(entry.SenderPlayerUid);
        entry.SenderPlayerName = NormalizeText(entry.SenderPlayerName);
        entry.SenderNickname = NormalizeText(entry.SenderNickname);
        entry.ChannelName = NormalizeText(entry.ChannelName);
        entry.ProximityMode = NormalizeText(entry.ProximityMode);
        entry.Language = NormalizeText(entry.Language);
        entry.MessageText = NormalizeText(entry.MessageText, trim: false);
        entry.FormattedMessage = NormalizeText(entry.FormattedMessage, trim: false);
        entry.RawEventMessage = NormalizeText(entry.RawEventMessage, trim: false);
        entry.ClientData = NormalizeText(entry.ClientData, trim: false);
        entry.RecipientCount = Math.Max(0, entry.RecipientCount);
        entry.PendingRecipientCount = Math.Max(0, entry.PendingRecipientCount);
        return entry;
    }

    private void Rewrite(IEnumerable<ChatHistoryEntry> entries)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmp = _path + ".tmp";
        using (var writer = new StreamWriter(tmp, append: false))
        {
            foreach (var entry in entries.Select(Normalize))
            {
                writer.WriteLine(JsonConvert.SerializeObject(entry, Formatting.None));
            }
        }

        File.Move(tmp, _path, overwrite: true);
    }

    private static bool Matches(ChatHistoryEntry entry, ChatHistoryQuery query)
    {
        return ContainsText(entry, query.SearchText) &&
               MatchesPlayer(entry, query.Player) &&
               MatchesChannel(entry, query.ChannelId) &&
               MatchesExact(entry.ChatKind, query.ChatKind) &&
               MatchesExact(entry.ProximityMode, query.ProximityMode) &&
               MatchesExact(entry.Language, query.Language) &&
               IsInRange(entry, query.FromUtc, query.ToUtc);
    }

    private static bool ContainsText(ChatHistoryEntry entry, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return Contains(entry.MessageText, searchText) ||
               Contains(entry.FormattedMessage, searchText) ||
               Contains(entry.RawEventMessage, searchText);
    }

    private static bool MatchesPlayer(ChatHistoryEntry entry, string player)
    {
        if (string.IsNullOrWhiteSpace(player))
        {
            return true;
        }

        return Contains(entry.SenderPlayerUid, player) ||
               Contains(entry.SenderPlayerName, player) ||
               Contains(entry.SenderNickname, player);
    }

    private static bool MatchesChannel(ChatHistoryEntry entry, int? channelId)
    {
        return channelId == null || entry.ChannelId == channelId.Value;
    }

    private static bool MatchesExact(string value, string expected)
    {
        return string.IsNullOrWhiteSpace(expected) || string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInRange(ChatHistoryEntry entry, string fromUtc, string toUtc)
    {
        var timestamp = GetTimestamp(entry);
        if (!string.IsNullOrWhiteSpace(fromUtc) && TryParseUtc(fromUtc, out var from) && timestamp < from)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(toUtc) && TryParseUtc(toUtc, out var to) && timestamp > to)
        {
            return false;
        }

        return true;
    }

    private static bool Contains(string value, string searchText)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int NormalizeLimit(int requested, int defaultLimit, int maxLimit)
    {
        defaultLimit = defaultLimit <= 0 ? 50 : defaultLimit;
        maxLimit = maxLimit <= 0 ? defaultLimit : maxLimit;
        return Math.Clamp(requested <= 0 ? defaultLimit : requested, 1, maxLimit);
    }

    private static DateTime GetTimestamp(ChatHistoryEntry entry)
    {
        return TryParseUtc(entry.TimestampUtc, out var timestamp) ? timestamp : DateTime.MinValue;
    }

    private static bool TryParseUtc(string value, out DateTime timestamp)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }

    private static string NormalizeTimestamp(string timestampUtc)
    {
        return TryParseUtc(timestampUtc, out var parsed)
            ? parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            : DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string NormalizeText(string value, bool trim = true)
    {
        value ??= string.Empty;
        value = value.Replace("\r\n", "\n").Replace('\r', '\n');
        return trim ? value.Trim() : value;
    }

    private static string GenerateId()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8];
    }

    public static string SanitizePathPart(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "default";
        }

        var span = raw.AsSpan();
        var buffer = new char[span.Length];
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            buffer[i] = char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_';
        }

        var result = new string(buffer);
        return result.Length > 64 ? result[..64] : result;
    }
}
