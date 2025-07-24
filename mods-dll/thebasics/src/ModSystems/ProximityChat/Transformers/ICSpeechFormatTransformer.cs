using System;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ICSpeechFormatTransformer : MessageTransformerBase
{

    public ICSpeechFormatTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_SPEECH);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var lang = context.GetMetadata<Language>(MessageContext.LANGUAGE);
        var nickname = context.GetMetadata<string>(MessageContext.FORMATTED_NAME);
        var mode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());

        // Add Quotes based on language type
        var delimiters = _config.ChatDelimiters;
        var quoteDelimiter = lang == LanguageSystem.SignLanguage ? delimiters.SignLanguageQuote : delimiters.Quote;
        context.Message = $"{quoteDelimiter.Start}{context.Message}{quoteDelimiter.End}";
        // Add Italics if sign language
        if(lang == LanguageSystem.SignLanguage){
            _chatSystem.API.Logger.Debug("Adding italics to sign language");
            context.Message = ChatHelper.Italic(context.Message);
        }

        // Add Lang color
        context.Message = ChatHelper.LangColor(context.Message, lang);
        
        var verb = GetProximityChatVerb(lang, mode);

        context.Message = $"{nickname} {verb} {context.Message}";

        return context;
    }

    private string GetProximityChatVerb(Language lang, ProximityChatMode mode)
    {
        // Check for sign language first
        if (lang == LanguageSystem.SignLanguage)
        {
            return "signs";
        }

        // Use the verbs from config
        var verbs = _config.ProximityChatModeVerbs[mode];

        return verbs.GetRandomElement();
    }
}