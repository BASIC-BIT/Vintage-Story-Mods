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
        builder.Append(" ");

        // Process the emote content
        var trimmedMessage = content.Trim();
        var splitMessage = trimmedMessage.Split('"');

        var language = context.GetMetadata<Language>(MessageContext.LANGUAGE);
        var chatMode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());

        var languageEnabled = _config.EnableLanguageSystem && !_config.DisableRPChat;

        for (var i = 0; i < splitMessage.Length; i++)
        {
            if (i % 2 == 0)
            {
                // Narrative parts outside quotes
                builder.Append(splitMessage[i]);
            }
            else
            {
                var text = splitMessage[i];

                // Add quotes based on language type (or default quotes when languages are disabled)
                var delimiters = _config.ChatDelimiters;
                var quoteDelimiter = (languageEnabled && language == LanguageSystem.SignLanguage) ? delimiters.SignLanguageQuote : delimiters.Quote;

                if (languageEnabled)
                {
                    _languageSystem.ProcessMessage(context.ReceivingPlayer, ref text, language);
                }

                text = $"{quoteDelimiter.Start}{text}{quoteDelimiter.End}";

                if (languageEnabled)
                {
                    text = ChatHelper.LangColor(text, language);
                    if (language == LanguageSystem.SignLanguage)
                    {
                        text = ChatHelper.Italic(text);
                    }
                }

                builder.Append(text);
            }
        }

        context.Message = builder.ToString();
        return context;
    }

}
