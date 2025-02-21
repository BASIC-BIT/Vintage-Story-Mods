using System.Collections.Generic;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Models;

public enum MessageContextState
{
    CONTINUE,
    STOP,
    ERROR
}

public class MessageContext
{
    public string Message { get; set; }
    public IServerPlayer SendingPlayer { get; set; }
    public IServerPlayer ReceivingPlayer { get; set; }
    public int GroupId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public MessageContextState State { get; set; } = MessageContextState.CONTINUE;
    public string ErrorMessage { get; set; }
} 