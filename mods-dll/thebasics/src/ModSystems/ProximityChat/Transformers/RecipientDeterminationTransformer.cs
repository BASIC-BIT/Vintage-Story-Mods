using System.Linq;
using System.Collections.Generic;
using thebasics.Configs;
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
    private const int DefaultSignLanguageRange = 15;

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
        var rules = BuildDeliveryRules(context, lang, range);
        var pendingSignLanguageRecipients = new List<IServerPlayer>();
        var occlusionPenalties = new Dictionary<string, int>();

        var nearbyPlayers = allPlayers
            .OfType<IServerPlayer>()
            .Where(player => CanReceive(context, player, originPos, rules, pendingSignLanguageRecipients, occlusionPenalties))
            .ToList();

        if (occlusionPenalties.Count > 0)
        {
            context.SetMetadata(MessageContext.OCCLUSION_PENALTY_BY_RECIPIENT, occlusionPenalties);
        }

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

    /// <summary>
    /// Delivery constraints for one message, resolved once instead of per candidate recipient.
    /// </summary>
    private readonly record struct DeliveryRules(
        int Range,
        bool RequiresSignLineOfSight,
        bool RequiresSpeechLineOfSight,
        int WallPenaltyBlocks)
    {
        public bool UsesWallMuffling => WallPenaltyBlocks > 0;
    }

    private DeliveryRules BuildDeliveryRules(MessageContext context, Language lang, int range)
    {
        var isSignLanguage = lang == LanguageSystem.SignLanguage;

        // Occlusion models sound, so it applies to audible speech only. Sign language is visual and
        // already gated by the sight check; emotes, environmental text, and OOC are not sound at all.
        var isAudibleSpeech = context.HasFlag(MessageContext.IS_SPEECH) && !isSignLanguage;

        var chatMode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());

        // An unlimited range means everyone hears you regardless of geometry, so occlusion has
        // nothing to decide. Skipping it also keeps raycasts and segment walks off a distance that
        // is bounded only by the size of the map.
        var occludes = isAudibleSpeech && !ModConfig.IsUnlimitedRange(range);

        return new DeliveryRules(
            Range: range,
            RequiresSignLineOfSight: isSignLanguage && _config.RequireLineOfSightForSignLanguage,
            RequiresSpeechLineOfSight: occludes && RequiresSpeechLineOfSight(chatMode),
            WallPenaltyBlocks: occludes ? System.Math.Max(0, _config.SpeechOcclusionWallPenaltyBlocks) : 0);
    }

    private bool RequiresSpeechLineOfSight(ProximityChatMode chatMode)
    {
        return _config.RequireLineOfSightForSpeech != null &&
               _config.RequireLineOfSightForSpeech.TryGetValue(chatMode, out var required) &&
               required;
    }

    /// <summary>
    /// Applies the sound occlusion experiments. Returns false when the recipient is muffled or
    /// walled off entirely; otherwise reports the effective-distance penalty to record for them.
    /// Occlusion is already disabled at unlimited range, so no distance re-test is needed there.
    /// </summary>
    private bool TryApplySoundOcclusion(
        MessageContext context,
        IServerPlayer player,
        DeliveryRules rules,
        int distance,
        bool isSelf,
        out int penalty)
    {
        penalty = 0;

        if (isSelf)
        {
            return true;
        }

        if (rules.UsesWallMuffling)
        {
            penalty = _proximityCheckUtils.CountSoundOccluders(context.SendingPlayer, player) * rules.WallPenaltyBlocks;

            // The unlimited check is redundant today because BuildDeliveryRules disables muffling at
            // unlimited range, but without it a future change there would make this compare against
            // a negative range and silently reject every recipient.
            if (!ModConfig.IsUnlimitedRange(rules.Range) && distance + penalty >= rules.Range)
            {
                return false;
            }
        }

        return !rules.RequiresSpeechLineOfSight ||
               _proximityCheckUtils.CanHearPlayer(context.SendingPlayer, player);
    }

    private bool CanReceive(
        MessageContext context,
        IServerPlayer player,
        BlockPos originPos,
        DeliveryRules rules,
        List<IServerPlayer> pendingSignLanguageRecipients,
        IDictionary<string, int> occlusionPenalties)
    {
        // A player still in the connect/spawn window can appear in AllOnlinePlayers with no world
        // entity yet. They are not in proximity of anything, and admitting them would hand a null
        // entity to every distance-reading transformer in the recipient phase, where an exception
        // aborts delivery for everyone after them.
        if (player.Entity?.Pos == null)
        {
            return false;
        }

        // An unlimited range skips the distance filter entirely; everyone online is in range.
        var unlimited = ModConfig.IsUnlimitedRange(rules.Range);
        var distance = unlimited ? 0 : player.Entity.Pos.AsBlockPos.ManhattanDistance(originPos);

        // Prefilter on raw distance before any raycasting. Occlusion only ever adds distance, so
        // anyone already out of range stays out, and the expensive checks never run for them.
        if (!unlimited && distance >= rules.Range)
        {
            return false;
        }

        var isSelf = player.PlayerUID == context.SendingPlayer.PlayerUID;

        if (!TryApplySoundOcclusion(context, player, rules, distance, isSelf, out var penalty))
        {
            return false;
        }

        if (rules.RequiresSignLineOfSight)
        {
            var canSee = _proximityCheckUtils.CanSeePlayer(
                context.SendingPlayer,
                player,
                useMultiPointTargets: true);
            if (!canSee)
            {
                pendingSignLanguageRecipients.Add(player);
                return false;
            }
        }

        if (penalty > 0)
        {
            // Recipient-phase transformers reuse this penalty so obfuscation and font size fade
            // with the same wall cost. Their base distance is Euclidean, not the Manhattan one
            // used above, so the two are not numerically identical.
            occlusionPenalties[player.PlayerUID] = penalty;
        }

        return true;
    }

    private int GetCommunicationRange(MessageContext context)
    {
        if (context.TryGetMetadata<Language>(MessageContext.LANGUAGE, out var lang) && lang == LanguageSystem.SignLanguage)
        {
            // Sign language has no unlimited sentinel: the admin panel bounds it to 0..512 and the
            // docs say so. A hand-edited negative would otherwise make signing server-wide and run
            // the default-on LOS raycast against every online player.
            return _config.SignLanguageRange < 0 ? DefaultSignLanguageRange : _config.SignLanguageRange;
        }

        var chatMode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());
        return _config.GetModeDistance(chatMode);
    }
}
