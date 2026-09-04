using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Keeps an invisible spectator's deliberate communication separate from embodied roleplay.
/// Plain or explicit speech, signing, and name-led emotes are refused so the administrator must
/// deliberately choose an allowed chat type.
/// Environmental narration and placed casting use their existing message types and pass through.
/// </summary>
public class SpectatorMessageTypeTransformer : MessageTransformerBase
{
    public SpectatorMessageTypeTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return SpectatorChatPolicy.ShouldProtectRoleplayChat(context.SendingPlayer, _config) &&
            (context.HasFlag(MessageContext.IS_SPEECH) || context.HasFlag(MessageContext.IS_EMOTE));
    }

    public override MessageContext Transform(MessageContext context)
    {
        context.SendingPlayer?.SendMessage(
            _chatSystem.ProximityChatId,
            Lang.Get("thebasics:chat-spectator-embodied-message-disabled"),
            EnumChatType.CommandError);
        context.State = MessageContextState.STOP;
        return context;
    }
}
