using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.src.ModSystems.ProximityChat.Transformers
{
    // Add the isRoleplay metadata for future transformers
    public class RoleplayTransformer : MessageTransformerBase
    {
        public RoleplayTransformer(RPProximityChatSystem chatSystem) : base(chatSystem) {

        }

        public override bool ShouldTransform(MessageContext context)
        {
            return true;
        }

        public override MessageContext Transform(MessageContext context)
        {
            context.Metadata["isRoleplay"] = !_chatSystem.GetModConfig().DisableRPChat && context.SendingPlayer.GetRpTextEnabled();
            return context;
        }

    }
}