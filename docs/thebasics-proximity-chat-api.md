# The BASICs Proximity Chat API

The BASICs exposes a small public server-side API for mods that want to observe processed proximity chat without subscribing to vanilla chat events or duplicating The BASICs parsing rules.

## Access

```csharp
using thebasics.ModSystems.ProximityChat;

var proximityApi = api.ModLoader.GetModSystem<RPProximityChatSystem>() as ITheBasicsProximityChatApi;
if (proximityApi != null)
{
    proximityApi.ProximityChatMessageProcessed += OnTheBasicsProximityChatMessage;
}
```

## Event

`ProximityChatMessageProcessed` fires once for a proximity message that The BASICs accepted, logged, and delivered to at least one immediate or delayed recipient.

Event handlers are isolated: if one add-on throws, The BASICs logs the failure once and continues running the remaining handlers.

## Event Data

`ProximityChatMessageEventArgs` includes:

- `SendingPlayer`: the player who sent the message.
- `Recipients`: immediate recipients determined by The BASICs.
- `PendingRecipients`: delayed sign-language recipients waiting for line of sight.
- `GroupId`: the chat group The BASICs processed.
- `Kind`: speech, emote, environmental, placed environmental, local OOC, or global OOC.
- `ProcessedMessage`: sender-phase message after The BASICs parsing/sanitization.
- `RenderedMessage`: The BASICs' trusted VTML-formatted log/chat representation.
- `PlainTextMessage`: VTML-stripped text for non-VTML surfaces such as Discord/webhooks.
- `Mode`: proximity mode when one applies.
- `Language`: language metadata when one applies; otherwise `null`.
- `FromCommand`: whether the message originated from a The BASICs command path.

## Integration Guidance

Prefer this event over `ICoreServerAPI.Event.PlayerChat` when integrating with The BASICs proximity chat. The vanilla event sees the raw player input and can miss The BASICs' accepted/re-emitted proximity messages because The BASICs consumes the original chat event to preserve proximity privacy.

Do not mutate player chat state from this event. Treat it as an observation/relay hook for logging, Discord/webhook bridges, analytics, RP add-ons, and moderation tools.

Use `PlainTextMessage` for external services unless the destination understands Vintage Story VTML.
