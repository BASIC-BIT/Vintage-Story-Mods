using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat.Transformers;

// Captures a baseline (non-recipient-specific) bubble text for speech.
// This allows overhead bubbles to fall back to a vanilla-like message when the server
// does not want per-recipient RP text (language/obfuscation) in bubbles.
public class SpeechBubbleBaseTextTransformer : MessageTransformerBase
{
    public SpeechBubbleBaseTextTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        // Only relevant for speech; emotes are formatted in the recipient phase.
        return context.HasFlag(MessageContext.IS_SPEECH) && !context.HasMetadata(MessageContext.BUBBLE_TEXT_BASE);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var baseText = (context.Message ?? string.Empty).Trim();
        if (baseText.Length > 0)
        {
            context.SetMetadata(MessageContext.BUBBLE_TEXT_BASE, baseText);
        }

        return context;
    }
}
