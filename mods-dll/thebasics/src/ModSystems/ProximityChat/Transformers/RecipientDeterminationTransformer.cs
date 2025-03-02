using System.Linq;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Determines which players should receive a message based on proximity and other factors
/// </summary>
public class RecipientDeterminationTransformer : IStageAwareTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    private readonly LanguageSystem _languageSystem;
    private readonly ProximityCheckUtils _proximityCheckUtils;
    
    public MessageStage[] ApplicableStages => new[] { MessageStage.SENDER_ONLY };
    
    public RecipientDeterminationTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem, ProximityCheckUtils proximityCheckUtils)
    {
        _chatSystem = chatSystem;
        _languageSystem = languageSystem;
        _proximityCheckUtils = proximityCheckUtils;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        // Only process at the correct stage
        if (context.Stage != MessageStage.SENDER_ONLY || context.State != MessageContextState.CONTINUE)
        {
            return context;
        }
        
        // Get chat mode from context
        var chatMode = context.Metadata.ContainsKey("chatMode") 
            ? (ProximityChatMode)context.Metadata["chatMode"] 
            : context.SendingPlayer.GetChatMode();
            
        // Determine communication range based on chat mode and language
        var range = GetCommunicationRange(context.SendingPlayer, chatMode);
        
        // Find players within range
        var allPlayers = _chatSystem.GetAPI().World.AllOnlinePlayers;
        var nearbyPlayers = allPlayers.Where(player => 
        {
            var serverPlayer = player as IServerPlayer;
            if (serverPlayer == null) return false;
            
            bool inRange = player.Entity.Pos.AsBlockPos.ManhattenDistance(
                context.SendingPlayer.Entity.Pos.AsBlockPos) < range;
                
            // Special check for sign language - must be within line of sight
            if (inRange && IsUsingSignLanguage(context.SendingPlayer))
            {
                return _proximityCheckUtils.CanSeePlayer(context.SendingPlayer, serverPlayer);
            }
            
            return inRange;
        }).Cast<IServerPlayer>().ToList();
        
        // Add players to context
        context.Recipients = nearbyPlayers;
        
        // Update stage
        context.Stage = MessageStage.RECIPIENTS_DETERMINED;
        
        return context;
    }
    
    private int GetCommunicationRange(IServerPlayer player, ProximityChatMode chatMode)
    {
        var config = _chatSystem.GetModConfig();
        
        // Sign language has a special range
        if (IsUsingSignLanguage(player))
        {
            return config.SignLanguageRange;
        }
        
        // Otherwise use configured ranges for chat modes
        return chatMode switch
        {
            ProximityChatMode.Normal => config.ProximityChatModeDistances[ProximityChatMode.Normal],
            ProximityChatMode.Whisper => config.ProximityChatModeDistances[ProximityChatMode.Whisper],
            ProximityChatMode.Yell => config.ProximityChatModeDistances[ProximityChatMode.Yell],
            _ => config.ProximityChatModeDistances[ProximityChatMode.Normal]
        };
    }
    
    private bool IsUsingSignLanguage(IServerPlayer player)
    {
        var message = "";
        var lang = _languageSystem.GetSpeakingLanguage(player, _chatSystem.GetProximityChatGroupId(), ref message);
        return lang == LanguageSystem.SignLanguage;
    }
} 