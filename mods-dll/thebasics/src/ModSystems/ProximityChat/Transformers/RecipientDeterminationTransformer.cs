using System.Linq;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Determines which players should receive a message based on proximity and other factors
/// </summary>
public class RecipientDeterminationTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    private readonly LanguageSystem _languageSystem;
    private readonly ProximityCheckUtils _proximityCheckUtils;

    public RecipientDeterminationTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem, ProximityCheckUtils proximityCheckUtils)
    {
        _chatSystem = chatSystem;
        _languageSystem = languageSystem;
        _proximityCheckUtils = proximityCheckUtils;
    }

    public MessageContext Transform(MessageContext context)
    {
        // Skip if this is a context for an individual recipient
        if (context.SendingPlayer != context.ReceivingPlayer)
        {
            return context;
        }

        // Skip if we already have recipients
        if (context.Recipients != null && context.Recipients.Count > 0)
        {
            return context;
        }

        // Determine communication range based on chat mode and language
        var range = GetCommunicationRange(context);

        // Find players within range
        var allPlayers = _chatSystem.GetAPI().World.AllOnlinePlayers;
        var nearbyPlayers = allPlayers.Where(player =>
        {
            var serverPlayer = player as IServerPlayer;
            if (serverPlayer == null) return false;

            bool inRange = player.Entity.Pos.AsBlockPos.ManhattenDistance(
                context.SendingPlayer.Entity.Pos.AsBlockPos) < range;

            var lang = context.Metadata["language"] as Language;
            // Special check for sign language - must be within line of sight
            if (inRange && lang == LanguageSystem.SignLanguage)
            {
                return _proximityCheckUtils.CanSeePlayer(context.SendingPlayer, serverPlayer);
            }

            return inRange;
        }).Cast<IServerPlayer>().ToList();

        // Add players to context
        context.Recipients = nearbyPlayers;

        return context;
    }

    private int GetCommunicationRange(MessageContext context)
    {
        if (context.Metadata["language"] is Language lang && lang == LanguageSystem.SignLanguage)
        {
            return _chatSystem.GetModConfig().SignLanguageRange;
        }

        var chatMode = (ProximityChatMode)context.Metadata["chatMode"];
        return _chatSystem.GetModConfig().ProximityChatModeDistances[chatMode];
    }
}