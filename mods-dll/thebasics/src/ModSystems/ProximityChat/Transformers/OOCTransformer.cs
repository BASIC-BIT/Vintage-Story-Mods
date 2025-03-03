using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class OOCTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public OOCTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        if (context.Metadata["isOOC"] as bool? == true)
        {
            context.Message = ChatHelper.Color("#808080", $"(OOC) {context.Metadata["formattedName"]}: {context.Message}");
        }

        return context;
    }
} 