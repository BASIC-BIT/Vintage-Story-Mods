using System.Collections.Generic;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class TransformerSystem
{
    private List<IMessageTransformer> _senderPhaseTransformers;
    private List<IMessageTransformer> _recipientPhaseTransformers;

    private RPProximityChatSystem _chatSystem;
    private LanguageSystem _languageSystem;
    private DistanceObfuscationSystem _distanceObfuscationSystem;
    private ProximityCheckUtils _proximityCheckUtils;

    public TransformerSystem(RPProximityChatSystem chatSystem, LanguageSystem languageSystem, DistanceObfuscationSystem distanceObfuscationSystem, ProximityCheckUtils proximityCheckUtils)
    {
        _chatSystem = chatSystem;
        _languageSystem = languageSystem;
        _distanceObfuscationSystem = distanceObfuscationSystem;
        _proximityCheckUtils = proximityCheckUtils;

        InitializeTransformers();
    }

    private void InitializeTransformers()
    {
        // Initialize transformers for the sender phase (validation and recipient determination)
        _senderPhaseTransformers = new List<IMessageTransformer>
        {
            // Validation transformers
            new RoleplayTransformer(_chatSystem), // Add roleplay metadata
            new NicknameRequirementTransformer(_chatSystem), // Require nickname if we're in RP chat
            new PlayerChatTransformer(_chatSystem), // If player chat, process special modifiers
            new BabbleWarningTransformer(_chatSystem, _languageSystem), // Warn if babbling

            new OOCTransformer(_chatSystem), // Handle formatting OOC messages
            new EnvironmentMessageTransformer(_chatSystem), // Handle formatting environmental messages
            new FormatTransformer(_chatSystem),
            new EmoteTransformer(_chatSystem),
            new ChatModeTransformer(_chatSystem),
            new ChatTypeTransformer(_chatSystem),

            // Recipient determination runs last in the sender phase
            new RecipientDeterminationTransformer(_chatSystem, _languageSystem, _proximityCheckUtils),
        };

        // Initialize transformers for the recipient phase (content transformation for each recipient)
        _recipientPhaseTransformers = new List<IMessageTransformer>
        {
            // Keep only transformers that need recipient-specific processing
            new LanguageTransformer(_languageSystem),
            new ObfuscationTransformer(_distanceObfuscationSystem)
        };
    }

    private MessageContext ExecuteTransformers(MessageContext context, List<IMessageTransformer> transformers)
    {
        foreach (var transformer in transformers)
        {
            context = transformer.Transform(context);
            if (context.State != MessageContextState.CONTINUE)
            {
                break;
            }
        }
        return context;
    }

    /// <summary>
    /// Processes a message through the two-phase pipeline: sender validation followed by per-recipient processing
    /// </summary>
    public void ProcessMessagePipeline(MessageContext initialContext, EnumChatType defaultChatType = EnumChatType.OthersMessage)
    {
        // ----- PHASE 1: Process sender context (validation and recipient determination) -----
        var context = ExecuteTransformers(initialContext, _senderPhaseTransformers);

        // If processing was stopped or no recipients were determined, we're done
        if (context.State != MessageContextState.CONTINUE || context.Recipients == null || context.Recipients.Count == 0)
        {
            return;
        }

        // ----- PHASE 2: Process for each recipient (content transformation) -----
        foreach (var recipient in context.Recipients)
        {
            // Create a fresh context for this recipient
            var recipientContext = new MessageContext
            {
                Message = context.Message,
                SendingPlayer = context.SendingPlayer,
                ReceivingPlayer = recipient,
                GroupId = context.GroupId,
                Metadata = new Dictionary<string, object>(context.Metadata)
            };

            // Process only the recipient-phase transformers
            recipientContext = ExecuteTransformers(recipientContext, _recipientPhaseTransformers);

            // Skip sending if processing was stopped for this recipient
            if (recipientContext.State != MessageContextState.CONTINUE)
            {
                continue;
            }

            // Get the chat type from the context metadata or use the provided default
            var chatType = recipientContext.Metadata.ContainsKey("chatType")
                ? (EnumChatType)recipientContext.Metadata["chatType"]
                : defaultChatType;

            // Get client data if available
            string data = null;
            if (recipientContext.Metadata.ContainsKey("clientData") && recipientContext.Metadata["clientData"] is string clientData)
            {
                data = clientData;
            }

            // Send the message to this recipient
            recipient.SendMessage(_chatSystem.ProximityChatId, recipientContext.Message, chatType, data);
        }
    }
}