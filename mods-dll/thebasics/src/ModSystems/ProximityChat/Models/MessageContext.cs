using System.Collections.Generic;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Models;

public enum MessageContextState
{
    CONTINUE,
    STOP
}

public class MessageContext
{
    public string Message { get; set; }
    public IServerPlayer SendingPlayer { get; set; }
    public IServerPlayer ReceivingPlayer { get; set; }
    public int GroupId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public MessageStage Stage { get; set; } = MessageStage.SENDER_ONLY;
    public MessageContextState State { get; set; } = MessageContextState.CONTINUE;
    
    /// <summary>
    /// The players who should receive this message (populated during recipient determination)
    /// </summary>
    public List<IServerPlayer> Recipients { get; set; }
    public string ErrorMessage { get; set; }
} 