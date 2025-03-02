namespace thebasics.ModSystems.ProximityChat.Models;

/// <summary>
/// Represents different stages in the message processing pipeline
/// </summary>
public enum MessageStage
{
    /// <summary>
    /// Initial stage - only the sender's context is available
    /// </summary>
    SENDER_ONLY,
    
    /// <summary>
    /// Recipients have been determined but message hasn't been sent yet
    /// </summary>
    RECIPIENTS_DETERMINED,
    
    /// <summary>
    /// Final stage - message is being sent to individual recipients
    /// </summary>
    SENDING_TO_RECIPIENT
} 