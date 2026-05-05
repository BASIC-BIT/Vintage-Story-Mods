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
    private readonly DistanceObfuscationSystem _distanceObfuscationSystem;

    public SpeechBubbleClientDataTransformer(
        RPProximityChatSystem chatSystem,
        LanguageSystem languageSystem = null,
        DistanceObfuscationSystem distanceObfuscationSystem = null) : base(chatSystem)
    {
        _languageSystem = languageSystem;
        _distanceObfuscationSystem = distanceObfuscationSystem;
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

        if (!TryGetValidEntityId(context, out var entityId))
        {
            return context;
        }

        var bubbleTextVtml = GetBubbleText(context);
        if (bubbleMode == OverheadChatBubbleModes.Vanilla)
        {
            return SetVanillaClientData(context, entityId, bubbleTextVtml);
        }

        return SetRpTextClientData(context, entityId, bubbleTextVtml);
    }

    private static bool TryGetValidEntityId(MessageContext context, out int entityId)
    {
        entityId = 0;
        var entity = context.SendingPlayer?.Entity;
        if (entity == null || entity.EntityId <= 0 || entity.EntityId > int.MaxValue)
        {
            return false;
        }

        entityId = (int)entity.EntityId;
        return true;
    }

    private static MessageContext SetVanillaClientData(MessageContext context, int entityId, string bubbleTextVtml)
    {
        var vanillaText = VtmlUtils.StripVtmlTags(bubbleTextVtml).Trim();
        if (vanillaText.Length > 0)
        {
            context.SetMetadata("clientData", $"from:{entityId},msg:{VtmlUtils.EscapeVtml(vanillaText)}");
        }

        return context;
    }

    private MessageContext SetRpTextClientData(MessageContext context, int entityId, string bubbleTextVtml)
    {
        var bubbleTextToSend = FormatRpBubbleText(context, bubbleTextVtml);
        if (bubbleTextToSend.Length == 0)
        {
            return context;
        }

        // Match vanilla behavior: the data string contains &lt; and &gt; which the client unescapes.
        bubbleTextToSend = VtmlUtils.EscapeVtml(bubbleTextToSend);

        var markers = BuildMarkers(context);
        context.SetMetadata("clientData", $"from:{entityId},msg{markers}:{bubbleTextToSend}");
        return context;
    }

    private string FormatRpBubbleText(MessageContext context, string bubbleTextVtml)
    {
        var bubbleText = (bubbleTextVtml ?? string.Empty).Trim();
        if (bubbleText.Length == 0)
        {
            return string.Empty;
        }

        Language bubbleLang = null;
        var languageEnabled = _config.EnableLanguageSystem && !_config.DisableRPChat;
        if (languageEnabled)
        {
            context.TryGetMetadata(MessageContext.LANGUAGE, out bubbleLang);
        }

        if (context.HasFlag(MessageContext.IS_SPEECH))
        {
            bubbleText = FormatSpeechBubbleText(context, bubbleText, bubbleLang, languageEnabled);
        }

        return ApplySpeechLanguageColor(context, bubbleText, bubbleLang, languageEnabled).Trim();
    }

    private string FormatSpeechBubbleText(MessageContext context, string bubbleText, Language bubbleLang, bool languageEnabled)
    {
        var presentationMode = ProximityChatPresentationModes.Normalize(_config.ProximityChatPresentationMode);
        if (presentationMode == ProximityChatPresentationModes.Prose)
        {
            return ChatHelper.FormatProseMessage(
                bubbleText,
                bubbleLang,
                _config,
                languageEnabled,
                text => ProcessProseQuotedText(context, text, bubbleLang, languageEnabled),
                context.GetMetadata<string>(MessageContext.FORMATTED_NAME));
        }

        if (ProximityChatPresentationModes.UsesSpeechQuotes(presentationMode))
        {
            bubbleText = ChatHelper.WrapSpeechQuotes(bubbleText, bubbleLang, _config, languageEnabled);
        }

        // Mirror chatbox: sign language speech is italicized in bubbles.
        return languageEnabled && bubbleLang == LanguageSystem.SignLanguage
            ? ChatHelper.Italic(bubbleText)
            : bubbleText;
    }

    private string ApplySpeechLanguageColor(MessageContext context, string bubbleText, Language bubbleLang, bool languageEnabled)
    {
        // Apply language color wrapping for speech bubbles so they match the chatbox formatting.
        // This runs after LanguageTransformer (which scrambles unknown languages) but before
        // ICSpeechFormatTransformer (which adds language color to the chatbox line).
        if (!languageEnabled || !context.HasFlag(MessageContext.IS_SPEECH) || bubbleLang == null)
        {
            return bubbleText;
        }

        return ProximityChatPresentationModes.Normalize(_config.ProximityChatPresentationMode) == ProximityChatPresentationModes.Prose
            ? bubbleText
            : ChatHelper.LangColor(bubbleText, bubbleLang);
    }

    private static string BuildMarkers(MessageContext context)
    {
        var kind = GetKindMarker(context);
        var mode = GetModeMarker(context);

        var markers = "";
        if (kind != null) markers += $"\u001fkind={kind}";
        if (mode != null) markers += $"\u001fmode={mode}";

        return markers;
    }

    private static string GetKindMarker(MessageContext context)
    {
        if (context.HasFlag(MessageContext.IS_ENVIRONMENTAL)) return "env";
        if (context.HasFlag(MessageContext.IS_EMOTE)) return "emote";
        if (context.HasFlag(MessageContext.IS_OOC)) return "ooc";

        return null;
    }

    private static string GetModeMarker(MessageContext context)
    {
        // For speech messages, include the chat mode so the client can scale the bubble.
        if (!context.HasFlag(MessageContext.IS_SPEECH) ||
            !context.TryGetMetadata(MessageContext.CHAT_MODE, out ProximityChatMode chatMode))
        {
            return null;
        }

        return chatMode switch
        {
            ProximityChatMode.Yell => "yell",
            ProximityChatMode.Whisper => "whisper",
            _ => null // Normal is the default; no marker needed.
        };
    }

    private string ProcessProseQuotedText(MessageContext context, string text, Language bubbleLang, bool languageEnabled)
    {
        var processed = text;

        if (languageEnabled && _languageSystem != null)
        {
            _languageSystem.ProcessMessage(context.ReceivingPlayer, ref processed, bubbleLang);
        }

        if (_distanceObfuscationSystem != null && context.SendingPlayer != null && context.ReceivingPlayer != null)
        {
            _distanceObfuscationSystem.ObfuscateMessage(context.SendingPlayer, context.ReceivingPlayer, ref processed);
        }

        return processed;
    }

    private static string GetBubbleText(MessageContext context)
    {
        var bubbleTextVtml = (context.Message ?? string.Empty).Trim();
        if (context.HasFlag(MessageContext.IS_ENVIRONMENTAL) &&
            context.TryGetMetadata(MessageContext.BUBBLE_TEXT_BASE, out string baseBubbleText) &&
            !string.IsNullOrWhiteSpace(baseBubbleText))
        {
            bubbleTextVtml = baseBubbleText.Trim();
        }

        return bubbleTextVtml;
    }
}
