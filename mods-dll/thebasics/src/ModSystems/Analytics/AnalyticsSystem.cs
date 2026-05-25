using System;
using System.Collections.Generic;
using System.Globalization;
using thebasics.Configs;
using thebasics.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Analytics;

public class AnalyticsSystem : BaseBasicModSystem
{
    public const string AnalyticsChannelName = "thebasicsanalytics";
    private const string AnalyticsConfigName = "the_basics_analytics.json";
    private const string AnalyticsCommand = "basicsanalytics";
    private const int ConsentPromptDelayMs = 3000;
    private const int ConsentPromptMaxAttempts = 10;
    private const int ShutdownFlushTimeoutMs = 750;

    private AnalyticsConfig _analyticsConfig;
    private IServerNetworkChannel _analyticsChannel;
    private long _flushListenerId;
    private readonly HashSet<string> _analyticsReadyPlayers = new();

    protected override void BasicStartServerSide()
    {
        _analyticsConfig = LoadAnalyticsConfig();
        RegisterNetworkChannel();
        ConfigureAnalyticsSink();
        RegisterCommands();
        HookEvents();

        AnalyticsService.Track("server started", new Dictionary<string, object>
        {
            ["remote_feature_flags_allowed"] = _analyticsConfig.AllowRemoteFeatureFlags,
            ["error_telemetry_allowed"] = _analyticsConfig.AllowErrorTelemetry,
            ["performance_telemetry_allowed"] = _analyticsConfig.AllowPerformanceTelemetry,
            ["personalized_analytics_requested"] = AnalyticsConsentLevels.AllowsPersonalizedAnalytics(_analyticsConfig.ConsentLevel)
        });
        AnalyticsService.TrackConfigSnapshot(Config);

        _flushListenerId = API.World.RegisterGameTickListener(dt =>
        {
            _ = AnalyticsService.FlushAsync();
        }, _analyticsConfig.FlushIntervalSeconds * 1000);
    }

    public override void Dispose()
    {
        try
        {
            AnalyticsService.Track("server stopped", new Dictionary<string, object>());
            var flushTask = AnalyticsService.FlushAsync();
            if (!flushTask.Wait(ShutdownFlushTimeoutMs) && _analyticsConfig?.DebugLogTelemetry == true)
            {
                API?.Logger.Warning("THEBASICS analytics: shutdown flush timed out; final event may be dropped.");
            }
        }
        catch
        {
            // Best-effort only.
        }

        if (_flushListenerId != 0)
        {
            API?.World?.UnregisterGameTickListener(_flushListenerId);
            _flushListenerId = 0;
        }

        if (API?.Event != null)
        {
            API.Event.PlayerJoin -= OnPlayerJoin;
            API.Event.PlayerDisconnect -= OnPlayerDisconnect;
        }

        AnalyticsService.Shutdown();
        base.Dispose();
    }

    private AnalyticsConfig LoadAnalyticsConfig()
    {
        AnalyticsConfig analyticsConfig;
        try
        {
            analyticsConfig = API.LoadModConfig<AnalyticsConfig>(AnalyticsConfigName);
        }
        catch (Exception e)
        {
            API.Server.LogError($"The BASICs: Failed to load analytics config '{AnalyticsConfigName}'. Remote analytics disabled. (Exception type: {e.GetType().Name})");
            analyticsConfig = new AnalyticsConfig();
        }

        if (analyticsConfig == null)
        {
            analyticsConfig = new AnalyticsConfig();
            API.StoreModConfig(analyticsConfig, AnalyticsConfigName);
        }

        analyticsConfig.InitializeDefaultsIfNeeded();
        API.StoreModConfig(analyticsConfig, AnalyticsConfigName);
        return analyticsConfig;
    }

    private void ConfigureAnalyticsSink()
    {
        if (!_analyticsConfig.AllowsRemoteAnalytics())
        {
            AnalyticsService.Configure(NoopAnalyticsSink.Instance);
            return;
        }

        EnsureServerInstallId();

        if (!TryGetAnalyticsEndpoint(out var endpoint))
        {
            AnalyticsService.Configure(NoopAnalyticsSink.Instance);
            API.Logger.Warning("THEBASICS analytics: consent is enabled, but the configured analytics endpoint URL is invalid. Remote events will not be sent.");
            return;
        }

        AnalyticsService.Configure(new RelayAnalyticsSink(API, _analyticsConfig, endpoint, Mod.Info.Version));
    }

    private void RegisterCommands()
    {
        API.ChatCommands.GetOrCreate(AnalyticsCommand)
            .WithAlias("thebasicsanalytics")
            .WithDescription(Lang.Get("thebasics:analytics-cmd-desc"))
            .WithArgs(API.ChatCommands.Parsers.OptionalWordRange("mode", "status", "server", "personalized", "off", "prompt"))
            .RequiresPrivilege(Privilege.root)
            .RequiresPlayer()
            .HandleWith(HandleAnalyticsCommand);
    }

    private void RegisterNetworkChannel()
    {
        _analyticsChannel = API.Network.RegisterChannel(AnalyticsChannelName)
            .RegisterMessageType<AnalyticsClientReadyMessage>()
            .RegisterMessageType<AnalyticsConsentPromptMessage>()
            .RegisterMessageType<AnalyticsConsentChoiceMessage>()
            .RegisterMessageType<AnalyticsConsentResultMessage>()
            .SetMessageHandler<AnalyticsClientReadyMessage>(OnClientReadyMessage)
            .SetMessageHandler<AnalyticsConsentChoiceMessage>(OnConsentChoiceMessage);
    }

    private void HookEvents()
    {
        API.Event.PlayerJoin += OnPlayerJoin;
        API.Event.PlayerDisconnect += OnPlayerDisconnect;
    }

    private TextCommandResult HandleAnalyticsCommand(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var mode = args.Parsers[0].IsMissing ? "status" : ((string)args.Parsers[0].GetValue()).ToLowerInvariant();

        return mode switch
        {
            "server" => SetConsent(AnalyticsConsentLevels.Server),
            "personalized" => SetConsent(AnalyticsConsentLevels.Personalized),
            "off" => SetConsent(AnalyticsConsentLevels.Disabled),
            "prompt" => SendConsentPrompt(player),
            _ => new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = BuildStatusMessage()
            }
        };
    }

    private TextCommandResult SetConsent(string consentLevel)
    {
        var previousConsent = _analyticsConfig.ConsentLevel;
        _analyticsConfig.ConsentLevel = AnalyticsConsentLevels.Normalize(consentLevel);
        _analyticsConfig.ConsentVersionAccepted = AnalyticsConsentLevels.CurrentConsentVersion;

        if (_analyticsConfig.AllowsRemoteAnalytics())
        {
            EnsureServerInstallId();
        }

        StoreAnalyticsConfig();
        ConfigureAnalyticsSink();

        if (_analyticsConfig.AllowsRemoteAnalytics())
        {
            AnalyticsService.Track("analytics consent changed", new Dictionary<string, object>
            {
                ["previous_consent_level"] = previousConsent,
                ["new_consent_level"] = _analyticsConfig.ConsentLevel,
                ["personalized_analytics_requested"] = AnalyticsConsentLevels.AllowsPersonalizedAnalytics(_analyticsConfig.ConsentLevel)
            });
            AnalyticsService.TrackConfigSnapshot(Config);
        }

        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = BuildConsentSavedMessage()
        };
    }

    private TextCommandResult SendConsentPrompt(IServerPlayer player)
    {
        SendConsentPromptSurface(player);
        MarkConsentPromptSent();
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = Lang.Get("thebasics:analytics-prompt-sent")
        };
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        if (!ShouldQueueConsentPrompt(player))
        {
            return;
        }

        QueueConsentPrompt(player, ConsentPromptMaxAttempts);
    }

    private void QueueConsentPrompt(IServerPlayer player, int attemptsRemaining)
    {
        API.Event.RegisterCallback(_ => TrySendQueuedConsentPrompt(player, attemptsRemaining), ConsentPromptDelayMs);
    }

    private void TrySendQueuedConsentPrompt(IServerPlayer player, int attemptsRemaining)
    {
        try
        {
            if (!ShouldQueueConsentPrompt(player))
            {
                return;
            }

            if (!IsConsentPromptDue())
            {
                return;
            }

            if (player.ConnectionState == EnumClientState.Playing && player.HasPrivilege(Privilege.root))
            {
                if (IsClientReadyForAnalytics(player))
                {
                    SendConsentPromptSurface(player);
                    MarkConsentPromptSent();
                    return;
                }

                if (attemptsRemaining <= 1)
                {
                    SendConsentPromptMessages(player);
                    MarkConsentPromptSent();
                    return;
                }

                QueueConsentPrompt(player, attemptsRemaining - 1);
                return;
            }

            if (attemptsRemaining > 1)
            {
                QueueConsentPrompt(player, attemptsRemaining - 1);
            }
        }
        catch
        {
            // Player may have disconnected during the delayed prompt.
        }
    }

    private bool ShouldPrompt(IServerPlayer player)
    {
        if (!ShouldQueueConsentPrompt(player))
        {
            return false;
        }

        if (player.ConnectionState != EnumClientState.Playing)
        {
            return false;
        }

        if (!player.HasPrivilege(Privilege.root))
        {
            return false;
        }

        return IsConsentPromptDue();
    }

    private void OnClientReadyMessage(IServerPlayer player, AnalyticsClientReadyMessage message)
    {
        if (!string.IsNullOrWhiteSpace(player?.PlayerUID))
        {
            _analyticsReadyPlayers.Add(player.PlayerUID);
        }

        if (!ShouldPrompt(player))
        {
            return;
        }

        SendConsentPromptSurface(player);
        MarkConsentPromptSent();
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        if (!string.IsNullOrWhiteSpace(player?.PlayerUID))
        {
            _analyticsReadyPlayers.Remove(player.PlayerUID);
        }
    }

    private bool ShouldQueueConsentPrompt(IServerPlayer player)
    {
        return player != null &&
               _analyticsConfig.PromptAdminsToOptIn &&
               _analyticsConfig.RequiresConsentChoice();
    }

    private bool IsClientReadyForAnalytics(IServerPlayer player)
    {
        return !string.IsNullOrWhiteSpace(player?.PlayerUID) &&
               _analyticsReadyPlayers.Contains(player.PlayerUID);
    }

    private bool IsConsentPromptDue()
    {
        if (!DateTime.TryParse(_analyticsConfig.LastPromptUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastPromptUtc))
        {
            return true;
        }

        return DateTime.UtcNow - lastPromptUtc.ToUniversalTime() >= TimeSpan.FromHours(_analyticsConfig.PromptRepeatHours);
    }

    private void EnsureServerInstallId()
    {
        if (string.IsNullOrWhiteSpace(_analyticsConfig.ServerInstallId))
        {
            _analyticsConfig.ServerInstallId = Guid.NewGuid().ToString("N");
            StoreAnalyticsConfig();
        }
    }

    private void StoreAnalyticsConfig()
    {
        _analyticsConfig.InitializeDefaultsIfNeeded();
        API.StoreModConfig(_analyticsConfig, AnalyticsConfigName);
    }

    private static void SendConsentPromptMessages(IServerPlayer player)
    {
        player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:analytics-consent-prompt-intro"), EnumChatType.Notification);
        player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:analytics-consent-prompt-privacy"), EnumChatType.Notification);
        player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:analytics-consent-prompt-actions", AnalyticsCommand), EnumChatType.Notification);
    }

    private void SendConsentPromptSurface(IServerPlayer player)
    {
        try
        {
            _analyticsChannel?.SendPacket(new AnalyticsConsentPromptMessage
            {
                CurrentConsentLevel = _analyticsConfig.ConsentLevel,
                ConsentVersion = AnalyticsConsentLevels.CurrentConsentVersion,
                CommandName = AnalyticsCommand
            }, player);
        }
        catch
        {
            SendConsentPromptMessages(player);
        }
    }

    private void OnConsentChoiceMessage(IServerPlayer player, AnalyticsConsentChoiceMessage message)
    {
        if (player?.HasPrivilege(Privilege.root) != true)
        {
            SendConsentResult(player, false, Lang.Get("thebasics:analytics-consent-error-not-root"), _analyticsConfig.ConsentLevel);
            return;
        }

        var consentLevel = AnalyticsConsentLevels.Normalize(message?.ConsentLevel);
        if (consentLevel == AnalyticsConsentLevels.Unknown)
        {
            SendConsentResult(player, false, Lang.Get("thebasics:analytics-consent-error-invalid"), _analyticsConfig.ConsentLevel);
            return;
        }

        var result = SetConsent(consentLevel);
        SendConsentResult(player, result.Status == EnumCommandStatus.Success, result.StatusMessage, _analyticsConfig.ConsentLevel);
    }

    private void SendConsentResult(IServerPlayer player, bool success, string message, string consentLevel)
    {
        if (player == null)
        {
            return;
        }

        try
        {
            _analyticsChannel?.SendPacket(new AnalyticsConsentResultMessage
            {
                Success = success,
                Message = message,
                ConsentLevel = consentLevel
            }, player);
        }
        catch
        {
            player.SendMessage(GlobalConstants.CurrentChatGroup, message, EnumChatType.Notification);
        }
    }

    private void MarkConsentPromptSent()
    {
        _analyticsConfig.LastPromptUtc = DateTime.UtcNow.ToString("O");
        StoreAnalyticsConfig();
    }

    private string BuildStatusMessage()
    {
        var endpointStatus = TryGetAnalyticsEndpoint(out _)
            ? Lang.Get("thebasics:analytics-endpoint-configured")
            : Lang.Get("thebasics:analytics-endpoint-invalid");

        return Lang.Get(
            "thebasics:analytics-status",
            _analyticsConfig.ConsentLevel,
            _analyticsConfig.ConsentVersionAccepted,
            endpointStatus,
            string.IsNullOrWhiteSpace(_analyticsConfig.ServerInstallId) ? Lang.Get("thebasics:analytics-install-id-not-created") : Lang.Get("thebasics:analytics-install-id-created"));
    }

    private string BuildConsentSavedMessage()
    {
        var endpointSuffix = TryGetAnalyticsEndpoint(out _)
            ? string.Empty
            : " " + Lang.Get("thebasics:analytics-endpoint-invalid-warning");

        return _analyticsConfig.ConsentLevel switch
        {
            AnalyticsConsentLevels.Server => Lang.Get("thebasics:analytics-consent-server") + endpointSuffix,
            AnalyticsConsentLevels.Personalized => Lang.Get("thebasics:analytics-consent-personalized") + endpointSuffix,
            AnalyticsConsentLevels.Disabled => Lang.Get("thebasics:analytics-consent-disabled"),
            _ => BuildStatusMessage()
        };
    }

    private bool TryGetAnalyticsEndpoint(out Uri endpoint)
    {
        if (!Uri.TryCreate(_analyticsConfig.EndpointUrl, UriKind.Absolute, out endpoint))
        {
            return false;
        }

        return endpoint.Scheme == Uri.UriSchemeHttps;
    }
}
