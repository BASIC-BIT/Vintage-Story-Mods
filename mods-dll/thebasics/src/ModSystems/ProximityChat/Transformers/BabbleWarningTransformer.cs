using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

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
        var config = _chatSystem.GetModConfig();
        
        // Only process for the sending player
        if (context.SendingPlayer != context.ReceivingPlayer)
        {
            return context;
        }
        
        // Check if the player is speaking in babble
        if (context.SendingPlayer.GetDefaultLanguage(config) == LanguageSystem.BabbleLang)
        {
            // Add a warning flag to show a babble warning message
            context.Metadata["showBabbleWarning"] = true;
        }
        
        return context;
    }
    
    // Method to send the babble warning if needed
    public static void SendBabbleWarningIfNeeded(MessageContext context)
    {
        if (context.Metadata.TryGetValue("showBabbleWarning", out var showWarning) && (bool)showWarning)
        {
            context.SendingPlayer.SendMessage(
                GlobalConstants.CurrentChatGroup, 
                "You are speaking in babble. Add a language via /addlang or set your default lang with a language identifier, ex. \":c\". Use /listlang to see all available languages", 
                EnumChatType.CommandError
            );
        }
    }
} 