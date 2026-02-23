using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

// Debug-only performance instrumentation.
// Records timing for known hot paths and logs slow events + periodic summaries.
internal static class PerfStats
{
    private struct Entry
    {
        public long Count;
        public long TotalTicks;
        public long MaxTicks;
    }

    private static readonly Dictionary<string, Entry> _entries = new();

    private static long _nextSummaryTickMs;

    // Keep logs sparse: this is only for investigating hitch reports.
    private const double SlowEventThresholdMs = 200;
    private const long SummaryIntervalMs = 30000;

    public static long Timestamp() => Stopwatch.GetTimestamp();

    public static void Record(ICoreClientAPI api, string key, long startTicks, long endTicks, string details = null)
    {
        if (api == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        // Hard gate: never record unless debug mode is enabled.
        if (!ChatUiSystem.IsDebugModeEnabled())
        {
            return;
        }

        var elapsedTicks = endTicks - startTicks;
        if (elapsedTicks <= 0)
        {
            return;
        }

        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new Entry();
        }

        entry.Count++;
        entry.TotalTicks += elapsedTicks;
        if (elapsedTicks > entry.MaxTicks)
        {
            entry.MaxTicks = elapsedTicks;
        }

        _entries[key] = entry;

        var elapsedMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
        if (elapsedMs >= SlowEventThresholdMs)
        {
            api.Logger.Warning($"[THEBASICS][perf] slow {key}: {elapsedMs:F1}ms{(string.IsNullOrWhiteSpace(details) ? "" : " - " + details)}");
        }

        MaybeLogSummary(api);
    }

    private static void MaybeLogSummary(ICoreClientAPI api)
    {
        var nowMs = Environment.TickCount64;
        if (nowMs < _nextSummaryTickMs)
        {
            return;
        }

        _nextSummaryTickMs = nowMs + SummaryIntervalMs;

        if (_entries.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append("[THEBASICS][perf] summary (last ").Append(SummaryIntervalMs / 1000).Append("s): ");

        var first = true;
        foreach (var kvp in _entries)
        {
            var entry = kvp.Value;
            if (entry.Count <= 0)
            {
                continue;
            }

            var avgMs = (entry.TotalTicks / (double)entry.Count) * 1000.0 / Stopwatch.Frequency;
            var maxMs = entry.MaxTicks * 1000.0 / Stopwatch.Frequency;

            if (!first) sb.Append(" | ");
            first = false;
            sb.Append(kvp.Key).Append(": avg=").Append(avgMs.ToString("F2")).Append("ms")
                .Append(", max=").Append(maxMs.ToString("F1")).Append("ms")
                .Append(", n=").Append(entry.Count);
        }

        api.Logger.Debug(sb.ToString());

        // Reset counters for the next window.
        var keys = new List<string>(_entries.Keys);
        foreach (var key in keys)
        {
            _entries[key] = new Entry();
        }
    }
}
