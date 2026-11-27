using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;

namespace thebasics.ModSystems.ChatUiSystem;

internal sealed class ChatDisplayClear
{
    private readonly Dictionary<LimitedList<string>, int> _hiddenByBuffer = new();

    internal void Clear(LimitedList<string> buffer)
    {
        if (buffer.Count > 0) _hiddenByBuffer[buffer] = buffer.Count;
    }

    internal void Undo(LimitedList<string> buffer) => _hiddenByBuffer.Remove(buffer);

    internal int HiddenCount(LimitedList<string> buffer) => _hiddenByBuffer.GetValueOrDefault(buffer);

    internal void BeforeAppend(LimitedList<string> buffer)
    {
        var hidden = HiddenCount(buffer);
        if (hidden == 0 || !buffer.IsFull()) return;
        if (hidden == 1) Undo(buffer);
        else _hiddenByBuffer[buffer] = hidden - 1;
    }

    internal IEnumerable<string> Visible(LimitedList<string> buffer) => buffer.Skip(HiddenCount(buffer));

    internal void Reset() => _hiddenByBuffer.Clear();
}
