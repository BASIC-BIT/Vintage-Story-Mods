using System.Text;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class EmoteTransformer : MessageTransformerBase
{
    private LanguageSystem _languageSystem;
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
        
        for (var i = 0; i < splitMessage.Length; i++)
        {
            if (i % 2 == 0)
            {
                // Narrative parts outside quotes
                builder.Append(splitMessage[i]);
            }
            else
            {
                // TODO: Find better way of applying language in emotes - ideally this happens in the language transformer
                // TODO: Cont. but because we don't split out the message, we either apply to apply the language before or after the emote transformer
                // TODO: Cont. both of which fuck up the messages
                var text = splitMessage[i];
                _languageSystem.ProcessMessage(context.SendingPlayer, ref text, language);

                // Process language for the quoted text

                // Add quotes based on language type
                var delimiters = _config.ChatDelimiters;
                var quoteDelimiter = language == LanguageSystem.SignLanguage ? delimiters.SignLanguageQuote : delimiters.Quote;
                text = $"{quoteDelimiter.Start}{text}{quoteDelimiter.End}";
                text = ChatHelper.LangColor(text, language);
                if(language == LanguageSystem.SignLanguage)
                {
                    text = ChatHelper.Italic(text);
                }
                builder.Append(text);
            }
        }
        
        context.Message = builder.ToString();
        return context;
    }

} 