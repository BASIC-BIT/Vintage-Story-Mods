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
            return !_chatSystem.Config.DisableRPChat && context.SendingPlayer.GetRpTextEnabled();
        }

        public override MessageContext Transform(MessageContext context)
        {
            context.SetFlag(MessageContext.IS_ROLEPLAY);
            return context;
        }

    }
}