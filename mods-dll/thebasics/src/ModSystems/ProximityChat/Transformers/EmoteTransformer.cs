using System.Text;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class EmoteTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public EmoteTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        if (!context.Metadata.ContainsKey("isEmote"))
        {
            return context;
        }
        
        var content = context.Message;
        var builder = new StringBuilder();
        
        builder.Append(context.Metadata["formattedName"]);
        builder.Append(" ");
        
        // Process the emote content
        var trimmedMessage = content.Trim();
        var splitMessage = trimmedMessage.Split('"');
        
        for (var i = 0; i < splitMessage.Length; i++)
        {
            if (i % 2 == 0)
            {
                builder.Append(splitMessage[i]);
            }
            else
            {
                // Handle quoted text in emotes
                if (context.Metadata.TryGetValue("language", out var langObj) && langObj is Language lang)
                {
                    builder.Append('"');
                    builder.Append(splitMessage[i]);
                    builder.Append('"');
                }
            }
        }
        
        context.Message = builder.ToString();
        return context;
    }
} 