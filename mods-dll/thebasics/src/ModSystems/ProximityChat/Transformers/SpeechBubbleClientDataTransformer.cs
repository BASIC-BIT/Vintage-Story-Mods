using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

// Overrides the clientData string so the vanilla overhead speech bubble shows RP-processed text.
// Vintage Story's EntityShapeRenderer.OnChatMessage uses the `data` argument (clientData) to render the bubble,
// not the final chat line string.
public class SpeechBubbleClientDataTransformer : MessageTransformerBase
{
    public SpeechBubbleClientDataTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return _config.OverrideSpeechBubblesWithRpText &&
            (context.HasFlag(MessageContext.IS_SPEECH) || context.HasFlag(MessageContext.IS_EMOTE) || context.HasFlag(MessageContext.IS_ENVIRONMENTAL));
    }

    public override MessageContext Transform(MessageContext context)
    {
        var entity = context.SendingPlayer?.Entity;
        if (entity == null)
        {
            return context;
        }

        if (entity.EntityId <= 0 || entity.EntityId > int.MaxValue)
        {
            return context;
        }

        // Vanilla overhead chat bubbles render plain text textures.
        // If VTML rendering is enabled client-side, we can send VTML and let the client render it.
        // Otherwise, strip VTML tags so they don't show literally.

        var bubbleTextVtml = (context.Message ?? string.Empty).Trim();

        // Emotes are rendered above the sender's head, so including the sender name is redundant.
        // The EmoteTransformer prefixes the formatted name; remove it for bubble display.
        if (context.HasFlag(MessageContext.IS_EMOTE) && context.TryGetMetadata(MessageContext.FORMATTED_NAME, out string formattedName) && !string.IsNullOrWhiteSpace(formattedName))
        {
            var prefix = formattedName + " ";
            if (bubbleTextVtml.StartsWith(prefix))
            {
                bubbleTextVtml = bubbleTextVtml[prefix.Length..].TrimStart();
            }
        }
        var bubbleTextToSend = _config.RenderSpeechBubblesWithVtml
            ? bubbleTextVtml
            : VtmlUtils.StripVtmlTags(bubbleTextVtml, _chatSystem.API.Logger);

        bubbleTextToSend = (bubbleTextToSend ?? string.Empty).Trim();
        if (bubbleTextToSend.Length == 0)
        {
            return context;
        }

        // Match vanilla behavior: the data string contains &lt; and &gt; which the client unescapes.
        bubbleTextToSend = VtmlUtils.EscapeVtml(bubbleTextToSend);

        // Add optional kind marker for client-side styling.
        // Only include when VTML bubble rendering is enabled; vanilla bubbles would show the marker text.
        if (_config.RenderSpeechBubblesWithVtml)
        {
            var kind = context.HasFlag(MessageContext.IS_ENVIRONMENTAL) ? "env" :
                context.HasFlag(MessageContext.IS_EMOTE) ? "emote" :
                context.HasFlag(MessageContext.IS_SPEECH) ? "speech" :
                null;

            if (kind != null)
            {
                context.SetMetadata("clientData", $"from:{(int)entity.EntityId},msg:{bubbleTextToSend},kind:{kind}");
                return context;
            }
        }

        context.SetMetadata("clientData", $"from:{(int)entity.EntityId},msg:{bubbleTextToSend}");
        return context;
    }
}
