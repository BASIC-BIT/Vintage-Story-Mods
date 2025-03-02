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
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    public MessageContextState State { get; set; } = MessageContextState.CONTINUE;
    
    /// <summary>
    /// The current stage in the message processing pipeline
    /// </summary>
    public MessageStage Stage { get; set; } = MessageStage.SENDER_ONLY;
    
    /// <summary>
    /// Players who should receive this message (only populated during RECIPIENTS_DETERMINED stage)
    /// </summary>
    public List<IServerPlayer> Recipients { get; set; } = new List<IServerPlayer>();
    public string ErrorMessage { get; set; }
} 