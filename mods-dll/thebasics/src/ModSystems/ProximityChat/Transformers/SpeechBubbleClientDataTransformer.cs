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
        // Always emit clientData for in-world bubble messages so vanilla bubbles keep working.
        // The config controls *what* text we place into the bubble, not whether a bubble exists.
        return context.HasFlag(MessageContext.IS_SPEECH)
            || context.HasFlag(MessageContext.IS_EMOTE)
            || context.HasFlag(MessageContext.IS_ENVIRONMENTAL)
            || (context.HasFlag(MessageContext.IS_OOC) && context.HasFlag(MessageContext.IS_PLAYER_CHAT));
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

        // Wrap speech in configurable quote delimiters, matching ICSpeechFormatTransformer:
        // sign language uses SignLanguageQuote (default: single quotes), others use Quote (default: double quotes).
        // Applied BEFORE the language color tag so the quotes sit inside <font color="...">
        // and the entire bubble (quotes included) renders in the language color.
        Language bubbleLang = null;
        var languageEnabled = _config.EnableLanguageSystem && !_config.DisableRPChat;
        if (languageEnabled)
        {
            context.TryGetMetadata(MessageContext.LANGUAGE, out bubbleLang);
        }

        if (context.HasFlag(MessageContext.IS_SPEECH))
        {
            var delimiters = _config.ChatDelimiters;
            var quoteDelimiter = (languageEnabled && bubbleLang == LanguageSystem.SignLanguage)
                ? delimiters.SignLanguageQuote
                : delimiters.Quote;
            bubbleTextVtml = $"{quoteDelimiter.Start}{bubbleTextVtml}{quoteDelimiter.End}";

            // Mirror chatbox: sign language speech is italicized in bubbles.
            if (languageEnabled && bubbleLang == LanguageSystem.SignLanguage)
            {
                bubbleTextVtml = ChatHelper.Italic(bubbleTextVtml);
            }
        }

        // Apply language color wrapping for speech bubbles so they match the chatbox formatting.
        // This runs after LanguageTransformer (which scrambles unknown languages) but before
        // ICSpeechFormatTransformer (which adds language color to the chatbox line).
        // We must apply the color here because ICSpeechFormatTransformer runs after us.
        if (languageEnabled && context.HasFlag(MessageContext.IS_SPEECH))
        {
            if (bubbleLang != null)
            {
                bubbleTextVtml = ChatHelper.LangColor(bubbleTextVtml, bubbleLang);
            }
        }

        var bubbleTextToSend = (bubbleTextVtml ?? string.Empty).Trim();
        if (bubbleTextToSend.Length == 0)
        {
            return context;
        }

        // Match vanilla behavior: the data string contains &lt; and &gt; which the client unescapes.
        bubbleTextToSend = VtmlUtils.EscapeVtml(bubbleTextToSend);

        // Build marker segment for client-side styling.
        // Kind marker: differentiates emote/env/OOC for border color styling.
        // Mode marker: carries yell/whisper for speech bubble size scaling.
        var kind = context.HasFlag(MessageContext.IS_ENVIRONMENTAL) ? "env" :
            context.HasFlag(MessageContext.IS_EMOTE) ? "emote" :
            context.HasFlag(MessageContext.IS_OOC) ? "ooc" :
            null;

        // For speech messages, include the chat mode so the client can scale the bubble.
        string mode = null;
        if (context.HasFlag(MessageContext.IS_SPEECH) &&
            context.TryGetMetadata(MessageContext.CHAT_MODE, out ProximityChatMode chatMode))
        {
            mode = chatMode switch
            {
                ProximityChatMode.Yell => "yell",
                ProximityChatMode.Whisper => "whisper",
                _ => null // Normal is the default — no marker needed.
            };
        }

        // Encode markers in the key segment (before the first ':') using unit separator
        // so vanilla clients don't display them and they can't collide with user text.
        var markers = "";
        if (kind != null) markers += $"\u001fkind={kind}";
        if (mode != null) markers += $"\u001fmode={mode}";

        context.SetMetadata("clientData", $"from:{(int)entity.EntityId},msg{markers}:{bubbleTextToSend}");
        return context;
    }
}
