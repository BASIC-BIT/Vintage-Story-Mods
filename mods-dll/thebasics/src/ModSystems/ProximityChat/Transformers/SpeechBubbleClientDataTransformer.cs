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
        var bubbleTextToSend = (bubbleTextVtml ?? string.Empty).Trim();
        if (bubbleTextToSend.Length == 0)
        {
            return context;
        }

        // Match vanilla behavior: the data string contains &lt; and &gt; which the client unescapes.
        bubbleTextToSend = VtmlUtils.EscapeVtml(bubbleTextToSend);

        // Add kind marker for client-side styling of emote/env bubbles.
        // Speech bubbles will render via vanilla unless VTML is present.
        var kind = context.HasFlag(MessageContext.IS_ENVIRONMENTAL) ? "env" :
            context.HasFlag(MessageContext.IS_EMOTE) ? "emote" :
            null;

        if (kind != null)
        {
            // Append kind using a rarely-used separator to avoid collisions with user text.
            // Client patch will parse `\u001fkind:`.
            context.SetMetadata("clientData", $"from:{(int)entity.EntityId},msg:{bubbleTextToSend}\u001fkind:{kind}");
            return context;
        }

        context.SetMetadata("clientData", $"from:{(int)entity.EntityId},msg:{bubbleTextToSend}");
        return context;
    }
}
