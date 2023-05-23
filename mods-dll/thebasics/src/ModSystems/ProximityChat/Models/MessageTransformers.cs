using System;
using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Models;

public enum MessageContextState {
    CONTINUE,
    STOP,
    ERROR,
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

public static class MessageTransformers
{
    public static MessageContext AddTimestamp(MessageContext messageContext)
    {
        messageContext.Metadata["timestamp"] = DateTime.UtcNow;
        return messageContext;
    }

    public static MessageContext ConvertToUpperCase(MessageContext messageContext)
    {
        messageContext.Message = messageContext.Message.ToUpperInvariant();
        return messageContext;
    }

    public static MessageContext HandleLanguageSwitch(MessageContext messageContext)
    {
        messageContext.Message = messageContext.Message.ToUpperInvariant();
        return messageContext;
    }

    public static MessageContext HandleEmote(MessageContext messageContext)
    {
        // if(messageContext.M)
        messageContext.Message = messageContext.Message.ToUpperInvariant();
        return messageContext;
    }

    public static MessageContext HandleEnvironmentMessage(MessageContext messageContext)
    {
        messageContext.Message = messageContext.Message.ToUpperInvariant();
        return messageContext;
    }
}