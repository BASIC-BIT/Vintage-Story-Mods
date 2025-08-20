using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

// Escapes XML/HTML special characters in messages that come from chat commands
// This prevents VTML injection and parsing errors when players use < > & " ' in commands
public class CommandMessageEscapeTransformer : MessageTransformerBase
{
    public CommandMessageEscapeTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        // Only escape messages that came from commands
        // Normal chat messages are already escaped by Vintage Story
        return context.HasFlag(MessageContext.IS_FROM_COMMAND);
    }

    public override MessageContext Transform(MessageContext context)
    {
        // Use the minimal escaping that VS uses for normal chat
        // This only escapes < and > which are the critical characters
        context.Message = VtmlUtils.EscapeVtml(context.Message);
        return context;
    }
}