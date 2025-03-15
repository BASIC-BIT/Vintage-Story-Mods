using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat.Transformers
{
    // Set the isRoleplay flag for future transformers
    public class RoleplayTransformer : MessageTransformerBase
    {
        public RoleplayTransformer(RPProximityChatSystem chatSystem) : base(chatSystem) {

        }

        public override bool ShouldTransform(MessageContext context)
        {
            return !_config.DisableRPChat &&
            context.SendingPlayer.GetRpTextEnabled() &&
            !context.HasFlag(MessageContext.IS_OOC) &&
            !context.HasFlag(MessageContext.IS_GLOBAL_OOC);
        }

        public override MessageContext Transform(MessageContext context)
        {
            context.SetFlag(MessageContext.IS_ROLEPLAY);
            return context;
        }
    }
}