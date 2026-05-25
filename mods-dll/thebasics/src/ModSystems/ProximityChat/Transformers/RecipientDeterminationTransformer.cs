using System.Linq;
using System.Collections.Generic;
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

        var originPos = GetRecipientOrigin(context);

        // Find players within range
        var allPlayers = _chatSystem.API.World.AllOnlinePlayers;
        context.TryGetMetadata<Language>(MessageContext.LANGUAGE, out var lang);
        var requiresSignLineOfSight = lang == LanguageSystem.SignLanguage && _config.RequireLineOfSightForSignLanguage;
        var pendingSignLanguageRecipients = new List<IServerPlayer>();

        var nearbyPlayers = allPlayers
            .OfType<IServerPlayer>()
            .Where(player => CanReceive(context, player, originPos, range, requiresSignLineOfSight, pendingSignLanguageRecipients))
            .ToList();

        if (pendingSignLanguageRecipients.Count > 0)
        {
            context.SetMetadata(MessageContext.PENDING_SIGN_LANGUAGE_RECIPIENTS, pendingSignLanguageRecipients);
        }

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

    private static BlockPos GetRecipientOrigin(MessageContext context)
    {
        if (!context.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL) ||
            !context.TryGetMetadata(MessageContext.PLACED_POSITION, out Vec3d placedPos))
        {
            return context.SendingPlayer.Entity.Pos.AsBlockPos;
        }

        return new BlockPos(
            (int)System.Math.Floor(placedPos.X),
            (int)System.Math.Floor(placedPos.Y),
            (int)System.Math.Floor(placedPos.Z));
    }

    private bool CanReceive(
        MessageContext context,
        IServerPlayer player,
        BlockPos originPos,
        int range,
        bool requiresSignLineOfSight,
        List<IServerPlayer> pendingSignLanguageRecipients)
    {
        var inRange = player.Entity.Pos.AsBlockPos.ManhattanDistance(originPos) < range;
        if (!inRange)
        {
            return false;
        }

        if (!requiresSignLineOfSight)
        {
            return true;
        }

        var canSee = _proximityCheckUtils.CanSeePlayer(
            context.SendingPlayer,
            player,
            useMultiPointTargets: true);
        if (!canSee)
        {
            pendingSignLanguageRecipients.Add(player);
        }

        return canSee;
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
