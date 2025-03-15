using System;
using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Models;

public enum MessageContextState {
    CONTINUE,
    STOP,
    ERROR,
}

public interface IMessageTransformer
{
    /// <summary>
    /// Transform a message context and return the updated context
    /// </summary>
    MessageContext Transform(MessageContext context);
}

public class MessageContext
{
    public string Message { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    public MessageContextState State { get; set; } = MessageContextState.CONTINUE;
}
public class MessageContext<T> : MessageContext
{
    public T Payload { get; set; }
}

public static class MessageTransformerExtensions
{
    public static Func<MessageContext, MessageContext> Chain(this Func<MessageContext, MessageContext> first, Func<MessageContext, MessageContext> second)
    {
        return (messageContext) =>
        {
            var result = first(messageContext);
            if (result.State == MessageContextState.CONTINUE)
            {
                return second(result);
            }

            return result;
        };
    }
    public static Func<MessageContext<T>, MessageContext<V>> Chain<T, U, V>(this Func<MessageContext<T>, MessageContext<U>> first, Func<MessageContext<U>, MessageContext<V>> second)
    {
        return (messageContext) =>
        {
            var result = first(messageContext);
            return second(result);
        };
    }
}
