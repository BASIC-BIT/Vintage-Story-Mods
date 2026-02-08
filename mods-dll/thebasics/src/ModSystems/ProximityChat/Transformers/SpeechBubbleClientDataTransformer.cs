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
        return _config.OverrideSpeechBubblesWithRpText && context.HasFlag(MessageContext.IS_SPEECH);
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

        // Important: vanilla overhead chat bubbles render *plain* text textures.
        // Any VTML tags will show literally (e.g. "<i>"), so we strip tags for bubble rendering.
        var bubbleTextVtml = (context.Message ?? string.Empty).Trim();
        var bubbleTextPlain = VtmlUtils.StripVtmlTags(bubbleTextVtml, _chatSystem.API.Logger).Trim();
        if (bubbleTextPlain.Length == 0)
        {
            return context;
        }

        // Match vanilla behavior: the data string contains &lt; and &gt; which the client unescapes.
        bubbleTextPlain = VtmlUtils.EscapeVtml(bubbleTextPlain);

        context.SetMetadata("clientData", $"from:{(int)entity.EntityId},msg:{bubbleTextPlain}");
        return context;
    }
}
