using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.SceneDescriptions;

/// <summary>One player's read marks, also the join hand-off message on the "thebasics" channel.</summary>
[ProtoContract]
public sealed class SceneReadMarksMessage
{
    [ProtoMember(1)] public Dictionary<string, long> Marks { get; set; } = new();
}

/// <summary>
/// A read mark pairs a marker position with the <see cref="SceneDescriptionData.ReadStamp"/> it
/// carried when the player read it, so any edit, re-placement, or explicit clear (all of which issue
/// a fresh stamp) makes the marker unread again for everyone. Also holds the client-side cache.
/// </summary>
internal static class SceneReadMarks
{
    internal const int MaxEntries = 4096;

    private static readonly Dictionary<string, long> ClientMarks = new();

    internal static string Key(BlockPos pos) =>
        pos == null ? string.Empty : pos.X + "/" + pos.Y + "/" + pos.Z + "/" + pos.dimension;

    internal static bool IsRead(IReadOnlyDictionary<string, long> marks, string key, long stamp) =>
        stamp != 0 && marks != null && marks.TryGetValue(key, out var read) && read == stamp;

    /// <summary>Records a mark, or clears it when <paramref name="stamp"/> is 0.</summary>
    internal static void Set(Dictionary<string, long> marks, string key, long stamp)
    {
        if (marks == null || string.IsNullOrEmpty(key)) return;
        if (stamp == 0)
        {
            marks.Remove(key);
            return;
        }

        marks[key] = stamp;
        // Stamps are wall-clock milliseconds, so the smallest ones are the oldest reads.
        if (marks.Count <= MaxEntries) return;
        foreach (var stale in marks.OrderBy(entry => entry.Value).Take(marks.Count - MaxEntries).Select(entry => entry.Key).ToArray())
        {
            marks.Remove(stale);
        }
    }

    internal static bool IsRead(BlockPos pos, long stamp) => IsRead(ClientMarks, Key(pos), stamp);

    internal static void ReplaceClientMarks(SceneReadMarksMessage message)
    {
        ClientMarks.Clear();
        if (message?.Marks == null) return;
        foreach (var mark in message.Marks) Set(ClientMarks, mark.Key, mark.Value);
    }

    internal static void SetClientMark(BlockPos pos, long stamp) => Set(ClientMarks, Key(pos), stamp);

    internal static void ClearClientMarks() => ClientMarks.Clear();
}
