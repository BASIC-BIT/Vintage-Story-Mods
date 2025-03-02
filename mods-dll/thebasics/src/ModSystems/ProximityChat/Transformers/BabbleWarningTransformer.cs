using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class BabbleWarningTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    private readonly LanguageSystem _languageSystem;
    
    public BabbleWarningTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem)
    {
        _chatSystem = chatSystem;
        _languageSystem = languageSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        // Skip if this is not a player chat message
        if (!context.Metadata.ContainsKey("isPlayerChat"))
        {
            return context;
        }
        
        // Only check for babble warning when we're in the initial context with the sending player
        if (context.SendingPlayer != context.ReceivingPlayer)
        {
            return context;
        }
        
        // Get the message text
        var message = context.Message;
        
        // Check if player is using babble language
        var speakingLanguage = _languageSystem.GetSpeakingLanguage(
            context.SendingPlayer, 
            _chatSystem.GetProximityChatGroupId(), 
            ref message
        );
        
        if (speakingLanguage == null)
        {
            return context;
        }
        
        // Get the player's default language
        var defaultLanguage = context.SendingPlayer.GetDefaultLanguage(_chatSystem.GetModConfig());
        
        // Check if player is speaking a different language than their default
        if (speakingLanguage != defaultLanguage)
        {
            // Send babble warning directly to the player
            context.SendingPlayer.SendMessage(
                _chatSystem.GetProximityChatGroupId(), 
                $"Warning: You are speaking in the {speakingLanguage.Name} language. Other players may not understand you.", 
                EnumChatType.Notification
            );
        }
        
        return context;
    }
} 