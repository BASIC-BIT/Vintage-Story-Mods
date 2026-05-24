using System;
using thebasics.Models;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.Analytics;

public class AnalyticsClientSystem : ModSystem
{
    private ICoreClientAPI _api;
    private IClientNetworkChannel _channel;
    private AnalyticsConsentDialog _dialog;

    public override bool ShouldLoad(EnumAppSide side) => side.IsClient();

    public override void StartClientSide(ICoreClientAPI api)
    {
        _api = api;
        _channel = api.Network.RegisterChannel(AnalyticsSystem.AnalyticsChannelName)
            .RegisterMessageType<AnalyticsClientReadyMessage>()
            .RegisterMessageType<AnalyticsConsentPromptMessage>()
            .RegisterMessageType<AnalyticsConsentChoiceMessage>()
            .RegisterMessageType<AnalyticsConsentResultMessage>()
            .SetMessageHandler<AnalyticsConsentPromptMessage>(OnPrompt)
            .SetMessageHandler<AnalyticsConsentResultMessage>(OnResult);

        _api.Event.PlayerJoin += OnPlayerJoin;
    }

    public override void Dispose()
    {
        _dialog?.TryClose();
        _dialog = null;
        if (_api?.Event != null)
        {
            _api.Event.PlayerJoin -= OnPlayerJoin;
        }

        base.Dispose();
    }

    private void OnPlayerJoin(IClientPlayer byPlayer)
    {
        if (byPlayer?.PlayerUID != _api.World?.Player?.PlayerUID)
        {
            return;
        }

        try
        {
            _channel?.SendPacket(new AnalyticsClientReadyMessage());
        }
        catch (Exception e)
        {
            _api.Logger.Warning($"THEBASICS analytics: failed to send client ready ({e.GetType().Name}).");
        }
    }

    private void OnPrompt(AnalyticsConsentPromptMessage message)
    {
        _api.Event.EnqueueMainThreadTask(() =>
        {
            try
            {
                _dialog?.TryClose();
                _dialog = new AnalyticsConsentDialog(_api, message, SendChoice);
                _dialog.TryOpen();
            }
            catch (Exception e)
            {
                _api.Logger.Warning($"THEBASICS analytics: failed to open consent dialog ({e.GetType().Name}).");
            }
        }, "thebasics-analytics-consent");
    }

    private void OnResult(AnalyticsConsentResultMessage message)
    {
        _api.Event.EnqueueMainThreadTask(() =>
        {
            _dialog?.TryClose();
            _dialog = null;

            if (!string.IsNullOrWhiteSpace(message?.Message))
            {
                _api.ShowChatMessage(message.Message);
            }
        }, "thebasics-analytics-result");
    }

    private void SendChoice(string consentLevel)
    {
        try
        {
            _channel?.SendPacket(new AnalyticsConsentChoiceMessage
            {
                ConsentLevel = consentLevel
            });
        }
        catch (Exception e)
        {
            _api.Logger.Warning($"THEBASICS analytics: failed to send consent choice ({e.GetType().Name}).");
            _api.ShowChatMessage("The BASICs analytics: failed to send consent choice. Use /basicsanalytics instead.");
        }
    }
}
