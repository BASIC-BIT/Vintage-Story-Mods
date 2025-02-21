using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Models;

public enum MessageContextState
{
    CONTINUE,
    STOP
}

public class MessageContext
{
    public string Message { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    public MessageContextState State { get; set; } = MessageContextState.CONTINUE;
} 