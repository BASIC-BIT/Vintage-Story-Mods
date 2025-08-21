using System.Collections.Generic;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class TransformerSystem
{
    private List<MessageTransformerBase> _senderPhaseTransformers;
    private List<MessageTransformerBase> _recipientPhaseTransformers;

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
        _senderPhaseTransformers = new List<MessageTransformerBase>
        {
            // Validation transformers
            new PlayerChatTransformer(_chatSystem), // If player chat, process special modifiers
            new CommandMessageEscapeTransformer(_chatSystem), // Escape XML special characters in command messages
            new RoleplayTransformer(_chatSystem), // Add roleplay metadata
            new NicknameRequirementTransformer(_chatSystem), // Require nickname if we're in RP chat
            new ChangeSpeakingLanguageTransformer(_chatSystem, _languageSystem), // Handle changing speaking language
            new BabbleWarningTransformer(_chatSystem), // Warn if babbling
            
            new ChatTypeTransformer(_chatSystem),
            new NameTransformer(_chatSystem), // Use nickname if RP chat, add color/bold
            new OOCTransformer(_chatSystem), // Format OOC Messages
            new GlobalOOCTransformer(_chatSystem), // Format Global OOC Messages
            new AutoCapitalizationTransformer(_chatSystem), // Add capitalization if needed
            new AutoPunctuationTransformer(_chatSystem), // Add punctuation if needed
            new AccentTransformer(_chatSystem), // Process accents
            new EnvironmentMessageTransformer(_chatSystem), // Add italics to environment messages

            // Recipient determination runs last in the sender phase
            new RecipientDeterminationTransformer(_chatSystem, _proximityCheckUtils),
        };

        // Initialize transformers for the recipient phase (content transformation for each recipient)
        _recipientPhaseTransformers = new List<MessageTransformerBase>
        {
            new EmoteTransformer(_chatSystem, _languageSystem), // Format emotes correctly
            // Keep only transformers that need recipient-specific processing
            new LanguageTransformer(_languageSystem, _chatSystem),
            new ObfuscationTransformer(_distanceObfuscationSystem, _chatSystem),
            new DistanceFontSizeTransformer(_chatSystem), // Apply distance-based font sizing

            // Finally, format speech for the recipient
            new ICSpeechFormatTransformer(_chatSystem)
        };
    }

    private MessageContext ExecuteTransformers(MessageContext context, List<MessageTransformerBase> transformers)
    {
        foreach (var transformer in transformers)
        {
            if(transformer.ShouldTransform(context))
            {
                context = transformer.Transform(context);
                if (context.State != MessageContextState.CONTINUE)
                {
                    break;
                }
            }
        }
        return context;
    }

    // TODO: Refactor common usage
    private string GetProximityChatVerb(Language lang, ProximityChatMode mode)
    {
        // Check for sign language first
        if (lang == LanguageSystem.SignLanguage)
        {
            return "signs";
        }

        // Use the verbs from config
        var verbs = _chatSystem.Config.ProximityChatModeVerbs[mode];

        return verbs.GetRandomElement();
    }

    // TODO: Refactor common usage with ICSpeechFormatTransformer
    private void LogChatMessage(MessageContext context) {
        var lang = context.GetMetadata<Language>(MessageContext.LANGUAGE);
        var nickname = context.GetMetadata<string>(MessageContext.FORMATTED_NAME);
        var mode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());
        
        // Add quotes based on language type
        var delimiters = _chatSystem.Config.ChatDelimiters;
        var quoteDelimiter = lang == LanguageSystem.SignLanguage ? delimiters.SignLanguageQuote : delimiters.Quote;
        var outputMessage = context.Message;
        outputMessage = $"{quoteDelimiter.Start}{outputMessage}{quoteDelimiter.End}";

        var verb = GetProximityChatVerb(lang, mode);

        outputMessage = $"{nickname} {verb} {outputMessage}";

        // log message
        _chatSystem.API.Logger.Chat(outputMessage);
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

        LogChatMessage(context);

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
                Metadata = new Dictionary<string, object>(context.Metadata),
                Flags = new Dictionary<string, bool>(context.Flags)
            };

            // Process only the recipient-phase transformers
            recipientContext = ExecuteTransformers(recipientContext, _recipientPhaseTransformers);

            // Skip sending if processing was stopped for this recipient
            if (recipientContext.State != MessageContextState.CONTINUE)
            {
                continue;
            }

            // Get the chat type from the context metadata or use the provided default
            var chatType = recipientContext.GetMetadata(MessageContext.CHAT_TYPE, defaultChatType);

            // Determine if we should show floating text for this message type
            bool shouldShowFloating = false;
            if (context.SendingPlayer?.Entity != null && !_chatSystem.Config.DisableFloatingText)
            {
                shouldShowFloating = true;
            }
            
            // Build the data field for floating text if needed
            string data = null;
            if (shouldShowFloating && context.SendingPlayer?.Entity != null)
            {
                // Format: "from: {entityId},rptext:"
                // The rptext: marker tells our client-side patch to handle this specially
                // Empty after colon so nothing shows if vanilla processes it
                data = $"from: {context.SendingPlayer.Entity.EntityId},rptext:";
            }
            
            // Send the message to this recipient with the proper data field
            recipient.SendMessage(_chatSystem.ProximityChatId, recipientContext.Message, chatType, data);
        }
    }
}