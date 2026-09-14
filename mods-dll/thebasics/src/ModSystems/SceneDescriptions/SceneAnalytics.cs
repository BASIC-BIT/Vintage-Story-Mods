using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.ModSystems.Analytics;
namespace thebasics.ModSystems.SceneDescriptions;

internal static class SceneAnalytics
{
    internal static bool Written(SceneDescriptionData data) => !string.IsNullOrWhiteSpace(data.Title) || !string.IsNullOrWhiteSpace(data.Body);
    internal static Dictionary<string, object> Properties(SceneDescriptionData data) => new()
    {
        ["scene_display_mode"] = data.Display switch { SceneDescriptionDisplay.AlwaysNearby => "always_nearby", SceneDescriptionDisplay.OnInteraction => "on_interaction", _ => "when_targeted" },
        ["scene_content"] = Written(data) ? "written" : "empty",
        ["scene_locked"] = data.IsLocked,
        ["scene_body_shown"] = data.ShowBodyInBubble
    };
    internal static string SaveKind(SceneDescriptionData before, SceneDescriptionData after)
    {
        if (!Written(after)) return "empty";
        return Written(before) ? "edit" : "first_content";
    }
    internal static bool VisibleFrame(float opacity, double x, double y, int width, int height) =>
        opacity >= 0.5f && x >= 0 && x < width && y >= 0 && y < height;
    internal static void Track(SceneDescriptionData data, string action, string extraKey = null, string extraValue = null, string mount = null)
    {
        try
        {
            if (!AnalyticsService.IsEnabled) return;
            var properties = Properties(data);
            if (extraKey != null) properties[extraKey] = extraValue;
            if (mount != null) properties["scene_mount"] = mount;
            AnalyticsService.TrackFeatureUsed("scene_markers", action, properties: properties);
        }
        catch { /* Telemetry must not interrupt gameplay. */ }
    }
}

// Keys are process-local and never enter analytics properties. Both client and server bound retention.
internal sealed class SceneObservationGate
{
    internal const int MaxEntries = 4096;
    private readonly Dictionary<string, (long Start, long Last, long Sent)> _entries = new();
    internal int Count => _entries.Count;
    internal void Clear() => _entries.Clear();
    internal bool Observe(string key, long now)
    {
        var found = _entries.TryGetValue(key, out var state);
        if (!found) state = (now, now, long.MinValue);
        if (now < state.Last || now - state.Last > 250) state.Start = now;
        state.Last = now;
        var accepted = now - state.Start >= 1000 && (state.Sent == long.MinValue || now - state.Sent >= 60000);
        if (accepted) state.Sent = now;
        Store(key, state);
        return accepted;
    }
    internal bool Accept(string key, long now, long cooldown)
    {
        if (_entries.TryGetValue(key, out var previous) && now >= previous.Sent && now - previous.Sent < cooldown) return false;
        Store(key, (now, now, now));
        return true;
    }
    private void Store(string key, (long Start, long Last, long Sent) state)
    {
        if (!_entries.ContainsKey(key) && _entries.Count >= MaxEntries)
            _entries.Remove(_entries.MinBy(pair => pair.Value.Last).Key);
        _entries[key] = state;
    }
}
