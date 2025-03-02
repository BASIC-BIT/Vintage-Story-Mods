namespace thebasics.ModSystems.ProximityChat.Models;

/// <summary>
/// Represents different stages in the message processing pipeline.
/// This is used primarily for documentation since transformers are now
/// organized into separate lists for the sender phase and recipient phase.
/// </summary>
public enum MessageStage
{
    /// <summary>
    /// Initial stage - only the sender's context is available.
    /// Transformers in this stage perform validation, formatting, content transformation, 
    /// and recipient determination.
    /// Most transformers are registered in the _senderPhaseTransformers list, including:
    /// - NicknameRequirementTransformer
    /// - PlayerChatTransformer
    /// - BabbleWarningTransformer
    /// - SimpleMessageTransformer
    /// - OOCTransformer
    /// - EnvironmentMessageTransformer
    /// - FormatTransformer
    /// - EmoteTransformer
    /// - ChatModeTransformer
    /// - ChatTypeTransformer
    /// - RecipientDeterminationTransformer (runs last)
    /// </summary>
    SENDER_ONLY,
    
    /// <summary>
    /// Recipients have been determined but message hasn't been sent yet.
    /// This is a transition stage handled by the ProcessMessagePipeline method.
    /// </summary>
    RECIPIENTS_DETERMINED,
    
    /// <summary>
    /// Final stage - message is being sent to individual recipients.
    /// Only transformers that need recipient-specific processing are in this phase.
    /// Currently only two transformers are registered in the _recipientPhaseTransformers list:
    /// - LanguageTransformer (translates message based on recipient's known languages)
    /// - ObfuscationTransformer (modifies message based on distance between sender and recipient)
    /// </summary>
    SENDING_TO_RECIPIENT
} 