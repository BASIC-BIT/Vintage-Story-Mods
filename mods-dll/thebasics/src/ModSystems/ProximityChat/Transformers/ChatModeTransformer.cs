using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ChatModeTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public ChatModeTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        if (context.Metadata.ContainsKey("isEmote") || context.Metadata.ContainsKey("isEnvironmental"))
        {
            return context;
        }
        
        var content = context.Message;
        var nickname = _chatSystem.GetFormattedNickname(context.SendingPlayer);
        var mode = context.SendingPlayer.GetChatMode();
        
        var verb = GetProximityChatVerb(context.SendingPlayer, mode, context);
        var punctuation = GetProximityChatPunctuation(mode);
        
        context.Message = $"{nickname} {verb}: {content}{punctuation}";
        return context;
    }
    
    private string GetProximityChatVerb(IServerPlayer player, ProximityChatMode mode, MessageContext context)
    {
        if (context.Metadata.TryGetValue("language", out var langObj) && langObj is Language lang && lang.IsSignLanguage)
        {
            return "signs";
        }

        return mode switch
        {
            ProximityChatMode.Normal => "says",
            ProximityChatMode.Whisper => "whispers",
            ProximityChatMode.Yell => "yells",
            _ => "says"
        };
    }
    
    private string GetProximityChatPunctuation(ProximityChatMode mode)
    {
        return mode switch
        {
            ProximityChatMode.Normal => ".",
            ProximityChatMode.Whisper => "...",
            ProximityChatMode.Yell => "!",
            _ => "."
        };
    }
} 