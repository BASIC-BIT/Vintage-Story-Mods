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

    public bool HasFlag(string flag) {
        return Flags.ContainsKey(flag) && Flags[flag];
    }

    public void SetFlag(string flag, bool value = true) {
        Flags[flag] = value;
    }

    public T GetMetadata<T>(string key) {
        return (T)Metadata[key];
    }

    public T GetMetadata<T>(string key, T defaultValue) {
        if(Metadata.ContainsKey(key)) {
            return (T)Metadata[key];
        }
        return defaultValue;
    }
    
    public bool HasMetadata(string key) {
        return Metadata.ContainsKey(key);
    }

    public bool TryGetMetadata<T>(string key, out T value) {
        if(Metadata.TryGetValue(key, out var obj) && obj is T typedValue) {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public void SetMetadata<T>(string key, T value) {
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
}   