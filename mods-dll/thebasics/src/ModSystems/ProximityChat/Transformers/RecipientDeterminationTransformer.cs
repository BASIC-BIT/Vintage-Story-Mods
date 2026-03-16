using System.Linq;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Determines which players should receive a message based on proximity and other factors
/// </summary>
public class RecipientDeterminationTransformer : MessageTransformerBase
{
    private readonly ProximityCheckUtils _proximityCheckUtils;

    public RecipientDeterminationTransformer(RPProximityChatSystem chatSystem, ProximityCheckUtils proximityCheckUtils) : base(chatSystem)
    {
        _proximityCheckUtils = proximityCheckUtils;
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return true;
    }

    public override MessageContext Transform(MessageContext context)
    {
        // Short circuit for global OOC and send to all players
        if (context.HasFlag(MessageContext.IS_GLOBAL_OOC))
        {
            context.Recipients = _chatSystem.API.World.AllOnlinePlayers.Cast<IServerPlayer>().ToList();
            return context;
        }

        // Determine communication range based on chat mode and language
        var range = GetCommunicationRange(context);

        // For placed environmental messages, use the hit position as the proximity origin
        // so recipients are determined by distance to the bubble, not the sender.
        BlockPos originPos;
        if (context.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL) &&
            context.TryGetMetadata(MessageContext.PLACED_POSITION, out Vec3d placedPos))
        {
            originPos = new BlockPos(
                (int)System.Math.Floor(placedPos.X),
                (int)System.Math.Floor(placedPos.Y),
                (int)System.Math.Floor(placedPos.Z));
        }
        else
        {
            originPos = context.SendingPlayer.Entity.Pos.AsBlockPos;
        }

        // Find players within range
        var allPlayers = _chatSystem.API.World.AllOnlinePlayers;
        var nearbyPlayers = allPlayers.Where(player =>
        {
            var serverPlayer = player as IServerPlayer;
            if (serverPlayer == null) return false;

            bool inRange = player.Entity.Pos.AsBlockPos.ManhattenDistance(originPos) < range;

            var lang = context.GetMetadata<Language>(MessageContext.LANGUAGE);
            // Special check for sign language - must be within line of sight
            if (inRange && lang == LanguageSystem.SignLanguage)
            {
                return _proximityCheckUtils.CanSeePlayer(context.SendingPlayer, serverPlayer);
            }

            return inRange;
        }).Cast<IServerPlayer>().ToList();

        // For placed environmental messages, always include the sender so they see their
        // own bubble even if they're farther from the placement point than the chat range.
        if (context.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL) &&
            !nearbyPlayers.Contains(context.SendingPlayer))
        {
            nearbyPlayers.Add(context.SendingPlayer);
        }

        // Add players to context
        context.Recipients = nearbyPlayers;

        return context;
    }

    private int GetCommunicationRange(MessageContext context)
    {
        if (context.TryGetMetadata<Language>(MessageContext.LANGUAGE, out var lang) && lang == LanguageSystem.SignLanguage)
        {
            return _config.SignLanguageRange;
        }

        var chatMode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());
        return _config.ProximityChatModeDistances[chatMode];
    }
}
