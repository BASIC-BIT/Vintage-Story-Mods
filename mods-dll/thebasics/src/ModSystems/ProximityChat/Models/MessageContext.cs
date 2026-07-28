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
    public Dictionary<string, object> Metadata { get; set; } = [];
    public Dictionary<string, bool> Flags { get; set; } = [];
    public MessageContextState State { get; set; } = MessageContextState.CONTINUE;

    /// <summary>
    /// The players who should receive this message (populated during recipient determination)
    /// </summary>
    public List<IServerPlayer> Recipients { get; set; }
    public string ErrorMessage { get; set; }

    public bool HasFlag(string flag)
    {
        return Flags.ContainsKey(flag) && Flags[flag];
    }

    public void SetFlag(string flag, bool value = true)
    {
        Flags[flag] = value;
    }

    public T GetMetadata<T>(string key)
    {
        return (T)Metadata[key];
    }

    public T GetMetadata<T>(string key, T defaultValue)
    {
        if (Metadata.ContainsKey(key))
        {
            return (T)Metadata[key];
        }
        return defaultValue;
    }

    public bool HasMetadata(string key)
    {
        return Metadata.ContainsKey(key);
    }

    public bool TryGetMetadata<T>(string key, out T value)
    {
        if (Metadata.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public void SetMetadata<T>(string key, T value)
    {
        Metadata[key] = value;
    }

    public static readonly string IS_OOC = "isOOC";
    public static readonly string IS_ENVIRONMENTAL = "isEnvironmental";
    public static readonly string IS_PLAYER_CHAT = "isPlayerChat";
    public static readonly string IS_EMOTE = "isEmote";
    public static readonly string IS_ROLEPLAY = "isRoleplay";
    public static readonly string IS_GLOBAL_OOC = "isGlobalOOC";
    public static readonly string IS_FROM_COMMAND = "isFromCommand";
    public static readonly string LANGUAGE = "language";
    public static readonly string CHAT_MODE = "chatMode";
    public static readonly string CHAT_TYPE = "chatType";
    public static readonly string FORMATTED_NAME = "formattedName";
    public static readonly string IS_SPEECH = "isSpeech";
    public static readonly string SPEECH_COLOR = "speechColor";
    public static readonly string SPEECH_TEXT = "speechText";
    public static readonly string PENDING_SIGN_LANGUAGE_RECIPIENTS = "pendingSignLanguageRecipients";

    // The speech verb, resolved once in the sender phase. Verb lists are random-pick, so resolving
    // per recipient would show two players standing together different verbs for the same line.
    public static readonly string SPEECH_VERB = "speechVerb";

    // Player UID -> extra effective distance in blocks from sound-occluding geometry between the
    // speaker and that recipient. Populated during recipient determination so obfuscation and font
    // size fade with the same wall penalty the range check applied. Note the base distance differs:
    // the range check is Manhattan, both consumers are Euclidean. Only the penalty is shared.
    public static readonly string OCCLUSION_PENALTY_BY_RECIPIENT = "occlusionPenaltyByRecipient";

    // Stores the pre-recipient-phase bubble text for speech messages.
    // Used when we want to keep overhead bubbles closer to vanilla behavior.
    public static readonly string BUBBLE_TEXT_BASE = "bubbleTextBase";

    public static readonly string IS_PLACED_ENVIRONMENTAL = "isPlacedEnvironmental";
    public static readonly string PLACED_POSITION = "placedPosition";

    public void UpdateMessage(string message, bool updateSpeech = true)
    {
        Message = message;
        if (updateSpeech && HasFlag(IS_SPEECH))
        {
            SetSpeechText(message);
        }
    }

    public void SetSpeechText(string text)
    {
        if (text == null)
        {
            Metadata.Remove(SPEECH_TEXT);
            return;
        }

        Metadata[SPEECH_TEXT] = text;
    }

    /// <summary>
    /// Extra effective distance in blocks for this recipient from sound-occluding geometry.
    /// Zero when wall muffling is disabled or nothing stands between the two players.
    /// </summary>
    public int GetOcclusionPenalty(IServerPlayer recipient)
    {
        if (recipient == null ||
            !TryGetMetadata(OCCLUSION_PENALTY_BY_RECIPIENT, out IDictionary<string, int> penalties))
        {
            return 0;
        }

        return penalties.TryGetValue(recipient.PlayerUID, out var penalty) ? penalty : 0;
    }

    public bool TryGetSpeechText(out string text)
    {
        if (TryGetMetadata(SPEECH_TEXT, out string value))
        {
            text = value;
            return true;
        }

        text = null;
        return false;
    }
}
