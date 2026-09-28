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
    [ProtoMember(2)] public List<string> ReadOrder { get; set; } = new();
}

/// <summary>
/// A read mark pairs a marker position with the <see cref="SceneDescriptionData.ReadStamp"/> it
/// carried when the player read it, so any edit, re-placement, or explicit clear (all of which issue
/// a fresh stamp) makes the marker unread again for everyone. Also holds the client-side cache.
/// </summary>
internal static class SceneReadMarks
{
    internal const int MaxEntries = 4096;

    private static SceneReadMarksMessage ClientMarks = new();

    internal static string Key(BlockPos pos) =>
        pos == null ? string.Empty : pos.X + "/" + pos.Y + "/" + pos.Z + "/" + pos.dimension;

    internal static bool IsRead(IReadOnlyDictionary<string, long> marks, string key, long stamp) =>
        stamp != 0 && marks != null && marks.TryGetValue(key, out var read) && read == stamp;

    /// <summary>Records a mark, or clears it when <paramref name="stamp"/> is 0.</summary>
    internal static void Set(SceneReadMarksMessage message, string key, long stamp)
    {
        if (message == null || string.IsNullOrEmpty(key)) return;
        var marks = message.Marks ??= new Dictionary<string, long>();
        // Old saves have only field 1. Content stamps give the best available initial order.
        if (message.ReadOrder == null || message.ReadOrder.Count != marks.Count)
            message.ReadOrder = marks.OrderBy(entry => entry.Value).Select(entry => entry.Key).ToList();
        var order = message.ReadOrder;
        order.Remove(key);
        if (stamp == 0)
        {
            marks.Remove(key);
            return;
        }

        marks[key] = stamp;
        order.Add(key);
        while (marks.Count > MaxEntries)
        {
            marks.Remove(order[0]);
            order.RemoveAt(0);
        }
    }

    internal static bool IsRead(BlockPos pos, long stamp) => IsRead(ClientMarks.Marks, Key(pos), stamp);

    internal static void ReplaceClientMarks(SceneReadMarksMessage message)
    {
        ClientMarks = message?.Marks == null ? new SceneReadMarksMessage() : message;
    }

    internal static void SetClientMark(BlockPos pos, long stamp) => Set(ClientMarks, Key(pos), stamp);

    internal static void ClearClientMarks() => ClientMarks = new SceneReadMarksMessage();
}
