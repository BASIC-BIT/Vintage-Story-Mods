using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Interface for transformers that are aware of message processing stages
/// </summary>
public interface IStageAwareTransformer : IMessageTransformer
{
    /// <summary>
    /// Gets the stage(s) at which this transformer should be applied
    /// </summary>
    MessageStage[] ApplicableStages { get; }
} 