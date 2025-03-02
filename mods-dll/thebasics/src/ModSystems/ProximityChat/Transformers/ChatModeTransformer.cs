using System;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
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
        // Skip emotes and environmental messages
        if (context.Metadata.ContainsKey("isEmote") || context.Metadata.ContainsKey("isEnvironmental"))
        {
            return context;
        }
        
        var content = context.Message;
        var nickname = _chatSystem.GetFormattedNickname(context.SendingPlayer);
        var mode = context.Metadata.TryGetValue("chatMode", out var chatModeObj) && chatModeObj is ProximityChatMode chatMode 
            ? chatMode 
            : context.SendingPlayer.GetChatMode();
        
        var verb = GetProximityChatVerb(context.SendingPlayer, mode, context);
        var punctuation = GetProximityChatPunctuation(mode);
        
        // Format sign language in italics (this is already done in LanguageSystem.ProcessMessage)
        // We only need to assemble the final message format here
        
        context.Message = $"{nickname} {verb}: {content}{punctuation}";
        return context;
    }
    
    private string GetProximityChatVerb(IServerPlayer player, ProximityChatMode mode, MessageContext context)
    {
        // Check for sign language first
        if (context.Metadata.TryGetValue("language", out var langObj) && langObj is Language lang && lang == LanguageSystem.SignLanguage)
        {
            return "signs";
        }
        
        // Use the verbs from config
        var verbs = _chatSystem.GetModConfig().ProximityChatModeVerbs[mode];

        var random = new Random();
        return verbs[random.Next(verbs.Length)];
    }
    
    private string GetProximityChatPunctuation(ProximityChatMode mode)
    {
        return _chatSystem.GetModConfig().ProximityChatModePunctuation[mode];
    }
} 