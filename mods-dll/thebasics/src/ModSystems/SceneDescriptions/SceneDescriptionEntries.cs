#nullable enable
using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneDescriptionEntry(string id, SceneDescriptionData data)
{
    internal string Id { get; } = id;
    internal SceneDescriptionData Data { get; } = data;
}

public sealed class SceneDescriptionEntries
{
    internal const string LegacyId = "legacy";
    internal const int MaxEntries = 8;
    private const string EntriesAttribute = "sceneEntries";

    private readonly List<SceneDescriptionEntry> _entries = [];

    internal IReadOnlyList<SceneDescriptionEntry> Entries => _entries;
    internal SceneDescriptionEntry Primary => _entries[0];

    public SceneDescriptionEntries() => _entries.Add(new SceneDescriptionEntry(LegacyId, new SceneDescriptionData()));

    internal SceneDescriptionEntry? Add(SceneDescriptionData? data)
    {
        if (_entries.Count >= MaxEntries) return null;
        var entry = new SceneDescriptionEntry(NewId(), (data ?? new SceneDescriptionData()).Clone().Normalize());
        entry.Data.ApplyAppearance(Primary.Data);
        _entries.Add(entry);
        return entry;
    }

    internal void SyncAppearance()
    {
        for (var index = 1; index < _entries.Count; index++)
            _entries[index].Data.ApplyAppearance(Primary.Data);
    }

    internal SceneDescriptionEntry? Find(string id)
    {
        foreach (var entry in _entries)
            if (entry.Id == id) return entry;
        return null;
    }

    internal bool Remove(string id)
    {
        if (_entries.Count == 1) return false;
        var entry = Find(id);
        if (entry == null) return false;
        // The next primary retains the marker's current shared style.
        if (entry == Primary) SyncAppearance();
        return _entries.Remove(entry);
    }

    internal static SceneDescriptionEntries ReadFrom(ITreeAttribute? attributes)
    {
        var result = new SceneDescriptionEntries();
        result._entries.Clear();
        var saved = attributes?.GetTreeAttribute(EntriesAttribute);
        if (saved != null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var count = Math.Clamp(saved.GetInt("count"), 0, MaxEntries);
            for (var index = 0; index < count; index++)
            {
                var child = saved.GetTreeAttribute(index.ToString());
                if (child == null) continue;
                var id = child.GetString("id", string.Empty);
                if (!ValidId(id) || !seen.Add(id))
                {
                    // Repairs must agree on client and server before the next save syncs the tree.
                    var repair = index + 1;
                    do { id = repair++.ToString("x32"); } while (!seen.Add(id));
                }
                result._entries.Add(new SceneDescriptionEntry(id, SceneDescriptionData.ReadFrom(child)));
            }
        }

        if (result._entries.Count == 0)
            result._entries.Add(new SceneDescriptionEntry(LegacyId, SceneDescriptionData.ReadFrom(attributes)));
        result.SyncAppearance();
        return result;
    }

    internal void WriteTo(ITreeAttribute attributes, bool includeReadStamps = false)
    {
        SyncAppearance();
        var saved = new TreeAttribute();
        saved.SetInt("count", _entries.Count);
        for (var index = 0; index < _entries.Count; index++)
        {
            var child = new TreeAttribute();
            child.SetString("id", _entries[index].Id);
            _entries[index].Data.WriteTo(child, includeReadStamps);
            saved[index.ToString()] = child;
        }
        attributes[EntriesAttribute] = saved;
    }

    private static bool ValidId(string id) => id == LegacyId || Guid.TryParseExact(id, "N", out _);
    private static string NewId() => Guid.NewGuid().ToString("N");
}
