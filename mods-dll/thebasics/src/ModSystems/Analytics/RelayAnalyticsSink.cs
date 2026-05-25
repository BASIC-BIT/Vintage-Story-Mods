using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using thebasics.Configs;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Analytics;

public sealed class RelayAnalyticsSink : IAnalyticsSink
{
    private readonly ICoreServerAPI _api;
    private readonly AnalyticsConfig _config;
    private readonly Uri _endpoint;
    private readonly string _modVersion;
    private readonly ConcurrentQueue<AnalyticsEvent> _queue = new();
    private readonly HttpClient _httpClient;
    private int _isFlushing;
    private volatile bool _disposed;

    public RelayAnalyticsSink(ICoreServerAPI api, AnalyticsConfig config, Uri endpoint, string modVersion)
    {
        _api = api;
        _config = config;
        _endpoint = endpoint;
        _modVersion = string.IsNullOrWhiteSpace(modVersion) ? "unknown" : modVersion;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public bool IsEnabled => !_disposed;

    public void Track(string eventName, IDictionary<string, object> properties)
    {
        if (_disposed || string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        while (_queue.Count >= _config.MaxQueuedEvents && _queue.TryDequeue(out _))
        {
            // Drop oldest analytics events first; gameplay must never wait on telemetry backlog.
        }

        _queue.Enqueue(new AnalyticsEvent(eventName, BuildProperties(properties)));
    }

    public async Task FlushAsync()
    {
        if (_disposed || _queue.IsEmpty || Interlocked.Exchange(ref _isFlushing, 1) == 1)
        {
            return;
        }

        var batch = new List<Dictionary<string, object>>();
        try
        {
            while (batch.Count < _config.MaxBatchEvents && _queue.TryDequeue(out var analyticsEvent))
            {
                batch.Add(new Dictionary<string, object>
                {
                    ["name"] = analyticsEvent.Name,
                    ["properties"] = analyticsEvent.Properties,
                    ["timestamp"] = analyticsEvent.TimestampUtc.ToString("O")
                });
            }

            if (batch.Count == 0)
            {
                return;
            }

            var payload = new Dictionary<string, object>
            {
                ["source"] = "thebasics",
                ["batch_schema_version"] = 1,
                ["server_install_id"] = _config.ServerInstallId,
                ["consent_level"] = _config.ConsentLevel,
                ["mod_id"] = "thebasics",
                ["mod_version"] = _modVersion,
                ["game_version"] = GameVersion.LongGameVersion,
                ["events"] = batch
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(_endpoint, content).ConfigureAwait(false);

            if (!_config.DebugLogTelemetry)
            {
                return;
            }

            if (response.IsSuccessStatusCode)
            {
                _api.Logger.Notification($"THEBASICS analytics: sent {batch.Count} event(s), relay status {(int)response.StatusCode}.");
            }
            else
            {
                _api.Logger.Warning($"THEBASICS analytics: relay rejected {batch.Count} event(s), status {(int)response.StatusCode}; batch dropped.");
            }
        }
        catch (Exception e)
        {
            // Dequeued analytics batches are intentionally not requeued: telemetry must never
            // grow an unbounded retry backlog or affect gameplay if the relay is unavailable.
            if (_config.DebugLogTelemetry)
            {
                _api.Logger.Warning($"THEBASICS analytics: failed to send {batch.Count} event(s) ({e.GetType().Name}); batch dropped.");
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isFlushing, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }

    private Dictionary<string, object> BuildProperties(IDictionary<string, object> properties)
    {
        var result = new Dictionary<string, object>
        {
            ["event_schema_version"] = 1,
            ["mod_id"] = "thebasics",
            ["mod_version"] = _modVersion,
            ["game_version"] = GameVersion.LongGameVersion,
            ["analytics_consent_level"] = _config.ConsentLevel,
            ["online_player_count_bucket"] = BucketCount(GetOnlinePlayerCount())
        };

        if (properties != null)
        {
            foreach (var property in properties)
            {
                if (string.IsNullOrWhiteSpace(property.Key))
                {
                    continue;
                }

                result[property.Key] = SanitizeValue(property.Value);
            }
        }

        return result;
    }

    private int GetOnlinePlayerCount()
    {
        try
        {
            return _api.World?.AllOnlinePlayers?.Length ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static object SanitizeValue(object value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (value is string or decimal || value.GetType().IsPrimitive)
        {
            return value;
        }

        return value is Enum enumValue ? enumValue.ToString() : value.ToString();
    }

    private static string BucketCount(int count)
    {
        return count switch
        {
            <= 0 => "0",
            <= 5 => "1-5",
            <= 10 => "6-10",
            <= 20 => "11-20",
            <= 50 => "21-50",
            <= 100 => "51-100",
            _ => "101+"
        };
    }
}
