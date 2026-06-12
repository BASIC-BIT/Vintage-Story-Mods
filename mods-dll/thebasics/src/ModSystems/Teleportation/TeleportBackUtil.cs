using System;
using thebasics.Extensions;
using thebasics.ModSystems.HomeSpawn;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Teleportation;

public static class TeleportBackUtil
{
    private const string BackLocationModDataKey = "BASIC_BACK_LOCATION";

    [ThreadStatic]
    private static int _recordingSuppressed;

    public static bool IsRecordingSuppressed => _recordingSuppressed > 0;

    public static IDisposable SuppressRecording()
    {
        _recordingSuppressed++;
        return new RecordingSuppressionScope();
    }

    public static void RecordPreviousLocation(IServerPlayer player)
    {
        if (IsRecordingSuppressed || player?.Entity == null)
        {
            return;
        }

        player.SetModData(BackLocationModDataKey, new TeleportBackEntry
        {
            Location = HomeSpawnLocation.From(player.Entity.Pos),
            RecordedUtcTicks = DateTime.UtcNow.Ticks
        });
    }

    public static bool TryGetPreviousLocation(IServerPlayer player, int expiresAfterSeconds, out HomeSpawnLocation location, out bool expired)
    {
        location = null;
        expired = false;

        var entry = player?.GetModData<TeleportBackEntry>(BackLocationModDataKey);
        if (entry?.Location == null || entry.RecordedUtcTicks <= 0)
        {
            return false;
        }

        expired = IsExpired(entry.RecordedUtcTicks, expiresAfterSeconds, DateTime.UtcNow);
        if (expired)
        {
            return false;
        }

        location = entry.Location;
        return true;
    }

    public static void ClearPreviousLocation(IServerPlayer player)
    {
        player?.SetModdata(BackLocationModDataKey, null);
    }

    internal static bool IsExpired(long recordedUtcTicks, int expiresAfterSeconds, DateTime nowUtc)
    {
        return expiresAfterSeconds > 0 && nowUtc - new DateTime(recordedUtcTicks, DateTimeKind.Utc) > TimeSpan.FromSeconds(expiresAfterSeconds);
    }

    private sealed class RecordingSuppressionScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseRecordingSuppression();
        }

        private static void ReleaseRecordingSuppression()
        {
            _recordingSuppressed = Math.Max(0, _recordingSuppressed - 1);
        }
    }
}
