using System;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
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
        var content = ChatHelper.LangColor(context.Message, lang);    
        if(lang == LanguageSystem.SignLanguage){
            content = ChatHelper.Italic(content);
        }
        // TODO: Make this configurable (and less hacky for sign language, maybe another transformer?)
        var quote = lang == LanguageSystem.SignLanguage ? "'" : "\"";
        var nickname = context.GetMetadata<string>(MessageContext.FORMATTED_NAME);
        var mode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());

        var verb = GetProximityChatVerb(context.SendingPlayer, mode, context);

        context.Message = $"{nickname} {verb} {quote}{content}{quote}";
        return context;
    }

    private string GetProximityChatVerb(IServerPlayer player, ProximityChatMode mode, MessageContext context)
    {
        // Check for sign language first
        if (context.TryGetMetadata<Language>(MessageContext.LANGUAGE, out var lang) && lang == LanguageSystem.SignLanguage)
        {
            return "signs";
        }

        // Use the verbs from config
        var verbs = _config.ProximityChatModeVerbs[mode];

        var random = new Random();
        return verbs[random.Next(verbs.Length)];
    }
}