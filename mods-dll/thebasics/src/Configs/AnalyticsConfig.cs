using System;

namespace thebasics.Configs;

public class AnalyticsConfig
{
    private const string DefaultEndpointHost = "thebasics-analytics-relay.basic-bit-1001.workers.dev";
    private const string LegacyCloudflareZoneEndpointHost = "analytics.basicbit.net";
    public static readonly string DefaultEndpointUrl = $"https://{DefaultEndpointHost}/v1/events/batch";
    private static readonly string LegacyCloudflareZoneEndpointUrl = $"https://{LegacyCloudflareZoneEndpointHost}/v1/events/batch";

    public string ConsentLevel { get; set; } = AnalyticsConsentLevels.Unknown;

    public int ConsentVersionAccepted { get; set; }

    public string ServerInstallId { get; set; } = string.Empty;

    public string EndpointUrl { get; set; } = DefaultEndpointUrl;

    public bool PromptAdminsToOptIn { get; set; } = true;

    public string LastPromptUtc { get; set; } = string.Empty;

    public double PromptRepeatHours { get; set; } = 24;

    public bool AllowRemoteFeatureFlags { get; set; }

    public bool AllowErrorTelemetry { get; set; } = true;

    public bool AllowPerformanceTelemetry { get; set; }

    public int FlushIntervalSeconds { get; set; } = 60;

    public int MaxQueuedEvents { get; set; } = 500;

    public int MaxBatchEvents { get; set; } = 50;

    public bool DebugLogTelemetry { get; set; }

    public void InitializeDefaultsIfNeeded()
    {
        ConsentLevel = AnalyticsConsentLevels.Normalize(ConsentLevel);
        var endpointUrl = EndpointUrl?.Trim();
        EndpointUrl = string.IsNullOrWhiteSpace(endpointUrl) ||
                      string.Equals(endpointUrl, LegacyCloudflareZoneEndpointUrl, StringComparison.OrdinalIgnoreCase)
            ? DefaultEndpointUrl
            : endpointUrl;
        FlushIntervalSeconds = Math.Clamp(FlushIntervalSeconds, 10, 3600);
        MaxQueuedEvents = Math.Clamp(MaxQueuedEvents, 25, 10000);
        MaxBatchEvents = Math.Clamp(MaxBatchEvents, 1, 50);
        PromptRepeatHours = Math.Clamp(PromptRepeatHours, 1, 168);
    }

    public bool AllowsRemoteAnalytics()
    {
        return AnalyticsConsentLevels.AllowsRemoteAnalytics(ConsentLevel) &&
               ConsentVersionAccepted >= AnalyticsConsentLevels.CurrentConsentVersion;
    }

    public bool RequiresConsentChoice()
    {
        return ConsentLevel == AnalyticsConsentLevels.Unknown ||
               (AnalyticsConsentLevels.AllowsRemoteAnalytics(ConsentLevel) &&
                ConsentVersionAccepted < AnalyticsConsentLevels.CurrentConsentVersion);
    }
}

public static class AnalyticsConsentLevels
{
    public const int CurrentConsentVersion = 1;

    public const string Unknown = "unknown";
    public const string Disabled = "disabled";
    public const string Server = "server";
    public const string Personalized = "personalized";

    public static string Normalize(string value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            Server => Server,
            Personalized => Personalized,
            Disabled => Disabled,
            "off" => Disabled,
            "none" => Disabled,
            _ => Unknown
        };
    }

    public static bool AllowsRemoteAnalytics(string value)
    {
        var normalized = Normalize(value);
        return normalized is Server or Personalized;
    }

    public static bool AllowsPersonalizedAnalytics(string value)
    {
        return Normalize(value) == Personalized;
    }
}
