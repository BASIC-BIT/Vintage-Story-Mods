using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

// Overrides the clientData string so the vanilla overhead speech bubble shows RP-processed text.
// Vintage Story's EntityShapeRenderer.OnChatMessage uses the `data` argument (clientData) to render the bubble,
// not the final chat line string.
public class SpeechBubbleClientDataTransformer : MessageTransformerBase
{
    private readonly LanguageSystem _languageSystem;

    public SpeechBubbleClientDataTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem = null) : base(chatSystem)
    {
        _languageSystem = languageSystem;
    }

    public override bool ShouldTransform(MessageContext context)
    {
        // Placed environmental messages use a dedicated network packet (PlacedEnvironmentMessage)
        // instead of clientData, so skip them here.
        if (context.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL))
        {
            return false;
        }

        var bubbleMode = OverheadChatBubbleModes.Normalize(_config.OverheadChatBubbleMode, _config.DisableRpOverheadBubbles);
        if (bubbleMode == OverheadChatBubbleModes.Off)
        {
            return true;
        }

        if (bubbleMode == OverheadChatBubbleModes.Vanilla)
        {
            return false;
        }

        // Emit clientData for in-world bubble messages so enhanced RP bubbles can render processed text.
        return context.HasFlag(MessageContext.IS_SPEECH)
            || context.HasFlag(MessageContext.IS_EMOTE)
            || context.HasFlag(MessageContext.IS_ENVIRONMENTAL)
            || (context.HasFlag(MessageContext.IS_OOC) && context.HasFlag(MessageContext.IS_PLAYER_CHAT));
    }

    public override MessageContext Transform(MessageContext context)
    {
        var bubbleMode = OverheadChatBubbleModes.Normalize(_config.OverheadChatBubbleMode, _config.DisableRpOverheadBubbles);
        if (bubbleMode == OverheadChatBubbleModes.Off)
        {
            context.Metadata.Remove("clientData");
            return context;
        }

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
        if (context.HasFlag(MessageContext.IS_ENVIRONMENTAL) &&
            context.TryGetMetadata(MessageContext.BUBBLE_TEXT_BASE, out string baseBubbleText) &&
            !string.IsNullOrWhiteSpace(baseBubbleText))
        {
            bubbleTextVtml = baseBubbleText.Trim();
        }

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
            var presentationMode = ProximityChatPresentationModes.Normalize(_config.ProximityChatPresentationMode);
            if (presentationMode == ProximityChatPresentationModes.Prose)
            {
                bubbleTextVtml = ChatHelper.FormatProseMessage(
                    bubbleTextVtml,
                    bubbleLang,
                    _config,
                    languageEnabled,
                    text =>
                    {
                        if (!languageEnabled || _languageSystem == null)
                        {
                            return text;
                        }

                        var processed = text;
                        _languageSystem.ProcessMessage(context.ReceivingPlayer, ref processed, bubbleLang);
                        return processed;
                    },
                    context.GetMetadata<string>(MessageContext.FORMATTED_NAME));

            }
            else
            {
                if (ProximityChatPresentationModes.UsesSpeechQuotes(presentationMode))
                {
                    bubbleTextVtml = ChatHelper.WrapSpeechQuotes(bubbleTextVtml, bubbleLang, _config, languageEnabled);
                }

                // Mirror chatbox: sign language speech is italicized in bubbles.
                if (languageEnabled && bubbleLang == LanguageSystem.SignLanguage)
                {
                    bubbleTextVtml = ChatHelper.Italic(bubbleTextVtml);
                }

            }
        }

        // Apply language color wrapping for speech bubbles so they match the chatbox formatting.
        // This runs after LanguageTransformer (which scrambles unknown languages) but before
        // ICSpeechFormatTransformer (which adds language color to the chatbox line).
        // We must apply the color here because ICSpeechFormatTransformer runs after us.
        if (languageEnabled &&
            context.HasFlag(MessageContext.IS_SPEECH) &&
            ProximityChatPresentationModes.Normalize(_config.ProximityChatPresentationMode) != ProximityChatPresentationModes.Prose)
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
