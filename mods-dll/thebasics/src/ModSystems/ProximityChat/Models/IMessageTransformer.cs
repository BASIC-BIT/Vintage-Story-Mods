using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Models;

public interface IMessageTransformer
{
    /// <summary>
    /// Transform a message context and return the updated context
    /// </summary>
    MessageContext Transform(MessageContext context);
} 