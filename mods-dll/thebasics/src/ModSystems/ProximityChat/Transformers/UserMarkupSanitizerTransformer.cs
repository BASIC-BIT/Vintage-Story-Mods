using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Prevents player-authored VTML/HTML-style tags from reaching chat or overhead-bubble renderers.
/// </summary>
public class UserMarkupSanitizerTransformer : MessageTransformerBase
{
    public UserMarkupSanitizerTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return !string.IsNullOrEmpty(context.Message) &&
            (context.HasFlag(MessageContext.IS_PLAYER_CHAT) || context.HasFlag(MessageContext.IS_FROM_COMMAND));
    }

    public override MessageContext Transform(MessageContext context)
    {
        context.UpdateMessage(VtmlUtils.StripUserVtmlTags(context.Message));
        return context;
    }
}
