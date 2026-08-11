#pragma warning disable S3267 // Transformer execution mutates context and short-circuits, so a LINQ pipeline would obscure control flow.
using System.Collections.Generic;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class TransformerSystem
{
    private const string ChatMessageLoggedMetadataKey = "chatMessageLogged";
    private const int SignLanguageLineOfSightRetryIntervalMs = 250;
    private const int SignLanguageLineOfSightRetryWindowMs = 3000;

    private List<MessageTransformerBase> _senderPhaseTransformers;
    private List<MessageTransformerBase> _recipientPhaseTransformers;

    private readonly RPProximityChatSystem _chatSystem;
    private readonly LanguageSystem _languageSystem;
    private readonly DistanceObfuscationSystem _distanceObfuscationSystem;
    private readonly ProximityCheckUtils _proximityCheckUtils;

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
            new PlacedEnvironmentTransformer(_chatSystem), // Raycast for !! / /envhere, falls back to standard env on miss
            new UserMarkupSanitizerTransformer(_chatSystem), // Strip player-authored VTML before trusted formatting is added
            new CommandMessageEscapeTransformer(_chatSystem), // Escape XML special characters in command messages
            new RoleplayTransformer(_chatSystem), // Add roleplay metadata
            new NicknameRequirementTransformer(_chatSystem), // Require nickname if we're in RP chat
            new ChangeSpeakingLanguageTransformer(_chatSystem, _languageSystem), // Handle changing speaking language
            new BabbleWarningTransformer(_chatSystem), // Warn if babbling
            
            new ChatTypeTransformer(_chatSystem),
            new NameTransformer(_chatSystem), // Canonical name for logging; recipient phase reapplies preferences.
            new AutoCapitalizationTransformer(_chatSystem), // Add capitalization if needed
            new AutoPunctuationTransformer(_chatSystem), // Add punctuation if needed
            new AccentTransformer(_chatSystem), // Process accents
            new EnvironmentMessageTransformer(_chatSystem), // Add italics to environment messages

            // Snapshot baseline bubble text for speech (before recipient-specific language/obfuscation).
            new SpeechBubbleBaseTextTransformer(_chatSystem),

            // Recipient determination runs last in the sender phase
            new RecipientDeterminationTransformer(_chatSystem, _proximityCheckUtils),
        };

        // Initialize transformers for the recipient phase (content transformation for each recipient)
        _recipientPhaseTransformers = new List<MessageTransformerBase>
        {
            new NameTransformer(_chatSystem), // Use nickname if RP chat, add recipient-specific color/bold
            new OOCTransformer(_chatSystem), // Format OOC Messages with recipient-specific colors
            new GlobalOOCTransformer(_chatSystem), // Format Global OOC Messages with recipient-specific colors
            new EmoteTransformer(_chatSystem, _languageSystem), // Format emotes correctly
            // Keep only transformers that need recipient-specific processing
            new LanguageTransformer(_languageSystem, _chatSystem),
            new ObfuscationTransformer(_distanceObfuscationSystem, _chatSystem),

            // Optional: override vanilla overhead bubble (clientData) with RP-processed text.
            new SpeechBubbleClientDataTransformer(_chatSystem, _languageSystem, _distanceObfuscationSystem),

            new DistanceFontSizeTransformer(_chatSystem), // Apply distance-based font sizing

            // Finally, format speech for the recipient
            new ICSpeechFormatTransformer(_chatSystem, _languageSystem, _distanceObfuscationSystem)
        };
    }

    private static MessageContext ExecuteTransformers(MessageContext context, List<MessageTransformerBase> transformers)
    {
        foreach (var transformer in transformers)
        {
            if (transformer.ShouldTransform(context))
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

    private string BuildChatLogMessage(MessageContext context)
    {
        return FormatChatLogMessage(context);
    }

    private string FormatChatLogMessage(MessageContext context)
    {
        var nickname = context.GetMetadata(MessageContext.FORMATTED_NAME, context.SendingPlayer?.PlayerName ?? "unknown");

        if (context.HasFlag(MessageContext.IS_OOC))
        {
            return $"(OOC) {nickname}: {context.Message}";
        }

        if (context.HasFlag(MessageContext.IS_GLOBAL_OOC))
        {
            return $"(GOOC) {nickname}: {context.Message}";
        }

        return FormatInCharacterLogMessage(context, nickname);
    }

    private string FormatInCharacterLogMessage(MessageContext context, string nickname)
    {
        var lang = context.GetMetadata<Language>(MessageContext.LANGUAGE);
        var mode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());
        var presentationMode = ProximityChatPresentationModes.Normalize(_chatSystem.Config.ProximityChatPresentationMode);
        var outputMessage = FormatLoggedSpeechBody(context, lang, presentationMode, nickname);
        var verb = GetResolvedSpeechVerb(context, lang, mode, _chatSystem.Config);

        return presentationMode switch
        {
            ProximityChatPresentationModes.SimpleSpeech => $"{nickname}: {outputMessage}",
            ProximityChatPresentationModes.PlainProximity => $"{nickname}: {outputMessage}",
            ProximityChatPresentationModes.Prose => outputMessage,
            _ => $"{nickname} {verb} {outputMessage}"
        };
    }

    private string FormatLoggedSpeechBody(MessageContext context, Language lang, string presentationMode, string nickname)
    {
        var languageEnabled = _chatSystem.Config.EnableLanguageSystem && !_chatSystem.Config.DisableRPChat;
        if (presentationMode == ProximityChatPresentationModes.Prose)
        {
            var prose = ChatHelper.FormatProseMessage(context.Message, lang, _chatSystem.Config, languageEnabled, nicknameReplacement: nickname);
            return ChatHelper.ApplyFreeformAttribution(prose, context.SendingPlayer, _chatSystem.Config);
        }

        return ProximityChatPresentationModes.UsesSpeechQuotes(presentationMode)
            ? ChatHelper.WrapSpeechQuotes(context.Message, lang, _chatSystem.Config, languageEnabled)
            : context.Message;
    }

    /// <summary>
    /// Processes a message through the two-phase pipeline: sender validation followed by per-recipient processing
    /// </summary>
    public void ProcessMessagePipeline(MessageContext initialContext, EnumChatType defaultChatType = EnumChatType.OthersMessage)
    {
        // ----- PHASE 1: Process sender context (validation and recipient determination) -----
        var context = ExecuteTransformers(initialContext, _senderPhaseTransformers);

        var hasImmediateRecipients = context.Recipients != null && context.Recipients.Count > 0;
        var hasPendingSignLanguageRecipients = HasPendingSignLanguageRecipients(context);

        if (context.State != MessageContextState.CONTINUE)
        {
            return;
        }

        ResolveSpeechVerbOnce(context);
        LogChatMessageOnce(context);

        // History/logging happens even when no one can hear the message; delivery still stops here.
        if (!hasImmediateRecipients && !hasPendingSignLanguageRecipients)
        {
            return;
        }

        if (hasImmediateRecipients)
        {
            _chatSystem.DispatchSpeechForContext(context);
            _chatSystem.DispatchChatterForContext(context);

            // ----- PHASE 2: Process for each recipient (content transformation) -----
            foreach (var recipient in context.Recipients)
            {
                _ = SendToRecipient(context, recipient, defaultChatType);
            }
        }

        SchedulePendingSignLanguageDeliveries(context, defaultChatType);
    }

    private static bool HasPendingSignLanguageRecipients(MessageContext context)
    {
        return context.TryGetMetadata(MessageContext.PENDING_SIGN_LANGUAGE_RECIPIENTS, out List<IServerPlayer> pendingRecipients) &&
               pendingRecipients.Count > 0;
    }

    internal static bool IsWithinSignLanguageRetryWindow(int elapsedMs)
    {
        return elapsedMs <= SignLanguageLineOfSightRetryWindowMs;
    }

    /// <summary>
    /// Reads the verb resolved in the sender phase, falling back to resolving one when a transformer
    /// is driven directly without the pipeline having run.
    /// </summary>
    internal static string GetResolvedSpeechVerb(MessageContext context, Language lang, ProximityChatMode mode, ModConfig config)
    {
        if (context.TryGetMetadata(MessageContext.SPEECH_VERB, out string verb) && !string.IsNullOrEmpty(verb))
        {
            return verb;
        }

        var speechText = context.TryGetSpeechText(out var raw) ? raw : context.Message;
        return ChatHelper.GetProximityChatVerb(lang, mode, config, speechText);
    }

    /// <summary>
    /// Picks the speech verb once, in the sender phase, and stores it for everyone downstream.
    /// Verb lists are a random pick, so resolving in the recipient phase would show two players
    /// standing side by side different verbs for the same line, and a third one in the chat log.
    /// </summary>
    private void ResolveSpeechVerbOnce(MessageContext context)
    {
        if (!context.HasFlag(MessageContext.IS_SPEECH) || context.HasMetadata(MessageContext.SPEECH_VERB))
        {
            return;
        }

        context.TryGetMetadata<Language>(MessageContext.LANGUAGE, out var lang);
        var mode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());
        var speechText = context.TryGetSpeechText(out var raw) ? raw : context.Message;

        context.SetMetadata(
            MessageContext.SPEECH_VERB,
            ChatHelper.GetProximityChatVerb(lang, mode, _chatSystem.Config, speechText));
    }

    private void LogChatMessageOnce(MessageContext context)
    {
        if (context.GetMetadata(ChatMessageLoggedMetadataKey, false))
        {
            return;
        }

        var logMessage = BuildChatLogMessage(context);
        _chatSystem.API.Logger.Chat(logMessage);
        _chatSystem.PublishProximityChatMessageProcessed(context, logMessage);
        _chatSystem.RecordChatHistory(context, logMessage);
        context.SetMetadata(ChatMessageLoggedMetadataKey, true);
    }

    private bool SendToRecipient(MessageContext context, IServerPlayer recipient, EnumChatType defaultChatType)
    {
        var recipientContext = new MessageContext
        {
            Message = context.Message,
            SendingPlayer = context.SendingPlayer,
            ReceivingPlayer = recipient,
            GroupId = context.GroupId,
            Metadata = new Dictionary<string, object>(context.Metadata),
            Flags = new Dictionary<string, bool>(context.Flags)
        };

        _languageSystem.ObserveMessageForRecipient(recipientContext);
        recipientContext = ExecuteTransformers(recipientContext, _recipientPhaseTransformers);
        if (recipientContext.State != MessageContextState.CONTINUE)
        {
            return false;
        }

        var chatType = recipientContext.GetMetadata(MessageContext.CHAT_TYPE, defaultChatType);
        recipientContext.TryGetMetadata("clientData", out string clientData);
        recipient.SendMessage(_chatSystem.ProximityChatId, recipientContext.Message, chatType, clientData);

        SendPlacedEnvironmentPacketIfNeeded(recipientContext, recipient);
        return true;
    }

    private void SendPlacedEnvironmentPacketIfNeeded(MessageContext recipientContext, IServerPlayer recipient)
    {
        if (!recipientContext.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL) ||
            !recipientContext.TryGetMetadata(MessageContext.PLACED_POSITION, out Vec3d placedPos))
        {
            return;
        }

        var bubbleText = recipientContext.TryGetMetadata(MessageContext.BUBBLE_TEXT_BASE, out string baseText)
            && !string.IsNullOrEmpty(baseText)
            ? baseText
            : recipientContext.Message ?? "";

        _chatSystem.SendPlacedEnvironmentPacket(recipient, placedPos, bubbleText);
    }

    private void SchedulePendingSignLanguageDeliveries(MessageContext context, EnumChatType defaultChatType)
    {
        if (!context.TryGetMetadata(MessageContext.PENDING_SIGN_LANGUAGE_RECIPIENTS, out List<IServerPlayer> pendingRecipients))
        {
            return;
        }

        SchedulePendingSignLanguageDeliveries(
            context,
            pendingRecipients,
            defaultChatType,
            SignLanguageLineOfSightRetryIntervalMs);
    }

    private void SchedulePendingSignLanguageDeliveries(
        MessageContext context,
        List<IServerPlayer> pendingRecipients,
        EnumChatType defaultChatType,
        int nextCheckElapsedMs)
    {
        if (pendingRecipients.Count == 0 || !IsWithinSignLanguageRetryWindow(nextCheckElapsedMs))
        {
            return;
        }

        _chatSystem.API.Event.RegisterCallback(_ =>
        {
            for (var i = pendingRecipients.Count - 1; i >= 0; i--)
            {
                var recipient = pendingRecipients[i];
                if (CanReceivePendingSignLanguage(context, recipient) &&
                    SendToRecipient(context, recipient, defaultChatType))
                {
                    LogChatMessageOnce(context);
                    pendingRecipients.RemoveAt(i);
                }
            }

            SchedulePendingSignLanguageDeliveries(
                context,
                pendingRecipients,
                defaultChatType,
                nextCheckElapsedMs + SignLanguageLineOfSightRetryIntervalMs);
        }, SignLanguageLineOfSightRetryIntervalMs);
    }

    private bool CanReceivePendingSignLanguage(MessageContext context, IServerPlayer recipient)
    {
        if (context.SendingPlayer?.Entity == null || recipient?.Entity == null)
        {
            return false;
        }

        var distance = recipient.Entity.Pos.AsBlockPos.ManhattanDistance(GetPendingSignLanguageOrigin(context));
        return distance < _chatSystem.Config.GetSignLanguageRange() &&
               _proximityCheckUtils.CanSeePlayer(context.SendingPlayer, recipient, useMultiPointTargets: true);
    }

    private static BlockPos GetPendingSignLanguageOrigin(MessageContext context)
    {
        if (context.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL) &&
            context.TryGetMetadata(MessageContext.PLACED_POSITION, out Vec3d placedPos))
        {
            return new BlockPos(
                (int)System.Math.Floor(placedPos.X),
                (int)System.Math.Floor(placedPos.Y),
                (int)System.Math.Floor(placedPos.Z));
        }

        return context.SendingPlayer.Entity.Pos.AsBlockPos;
    }
}
