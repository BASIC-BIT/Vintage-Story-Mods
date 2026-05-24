using System.Text;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class EmoteTransformer : MessageTransformerBase
{
    private readonly LanguageSystem _languageSystem;
    public EmoteTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem) : base(chatSystem)
    {
        _languageSystem = languageSystem;
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_EMOTE);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var content = context.Message;
        var builder = new StringBuilder();

        var formattedName = context.GetMetadata<string>(MessageContext.FORMATTED_NAME);
        builder.Append(formattedName);
        var needsNameSeparator = !string.IsNullOrEmpty(formattedName);

        // Process the emote content
        var trimmedMessage = content.Trim();
        var splitMessage = trimmedMessage.Split('"');

        var language = context.GetMetadata<Language>(MessageContext.LANGUAGE);
        var languageEnabled = _config.EnableLanguageSystem && !_config.DisableRPChat;

        for (var i = 0; i < splitMessage.Length; i++)
        {
            if (i % 2 == 0)
            {
                AppendNarrative(builder, splitMessage[i], context, ref needsNameSeparator);
            }
            else
            {
                AppendQuotedSpeech(builder, splitMessage[i], language, languageEnabled, context, ref needsNameSeparator);
            }
        }

        context.Message = builder.ToString();
        return context;
    }

    private void AppendNarrative(StringBuilder builder, string narrative, MessageContext context, ref bool needsNameSeparator)
    {
        if (string.IsNullOrEmpty(narrative))
        {
            return;
        }

        if (needsNameSeparator)
        {
            narrative = " " + narrative;
            needsNameSeparator = false;
        }

        builder.Append(ChatHelper.Color(narrative, ChatVisualPreferenceResolver.GetEmoteColor(context.ReceivingPlayer, _config)));
    }

    private void AppendQuotedSpeech(StringBuilder builder, string text, Language language, bool languageEnabled, MessageContext context, ref bool needsNameSeparator)
    {
        var delimiters = _config.ChatDelimiters;
        var quoteDelimiter = languageEnabled && language == LanguageSystem.SignLanguage
            ? delimiters.SignLanguageQuote
            : delimiters.Quote;

        if (languageEnabled)
        {
            _languageSystem.ProcessMessage(context.ReceivingPlayer, ref text, language);
        }

        text = $"{quoteDelimiter.Start}{text}{quoteDelimiter.End}";
        text = PrefixSeparatorIfNeeded(text, ref needsNameSeparator);

        if (languageEnabled)
        {
            text = FormatQuotedLanguageText(text, language, context);
        }

        builder.Append(text);
    }

    private static string PrefixSeparatorIfNeeded(string text, ref bool needsNameSeparator)
    {
        if (!needsNameSeparator)
        {
            return text;
        }

        needsNameSeparator = false;
        return " " + text;
    }

    private static string FormatQuotedLanguageText(string text, Language language, MessageContext context)
    {
        if (language == LanguageSystem.SignLanguage)
        {
            text = ChatHelper.Italic(text);
        }

        return ChatVisualPreferenceResolver.FormatLanguageText(text, language, context.ReceivingPlayer);
    }

}
