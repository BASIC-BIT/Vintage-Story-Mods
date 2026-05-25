using System.Collections.Generic;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.ModSystems.ProximityChat;

internal static class ChatterMessagePlanner
{
    public static bool TryGetSpeechLength(MessageContext context, ModConfig config, out int speechLength)
    {
        speechLength = 0;
        if (IsSilentLanguage(context))
        {
            return false;
        }

        if (context.HasFlag(MessageContext.IS_SPEECH))
        {
            return TryGetSpeechMessageLength(context, config, out speechLength);
        }

        return context.HasFlag(MessageContext.IS_EMOTE)
            && TryGetEmoteSpeechLength(context.Message, out speechLength);
    }

    public static ChatterSoundMessage CreateBaseMessage(IServerPlayer player, MessageContext context, ModConfig config, int speechLength)
    {
        var mode = context.GetMetadata(MessageContext.CHAT_MODE, player.GetChatMode());
        return new ChatterSoundMessage
        {
            EntityId = player.Entity.EntityId,
            TalkType = GetTalkType(mode),
            NoteCount = GetNoteCount(speechLength),
            Volume = GetModeValue(config.ChatterModeVolume, mode, 0.8f),
            Pitch = GetModeValue(config.ChatterModePitch, mode, 1.0f),
        };
    }

    public static ChatterSoundMessage ForRecipient(ChatterSoundMessage message, bool isSelf, float selfVolumeMultiplier)
    {
        return isSelf
            ? new ChatterSoundMessage
            {
                EntityId = message.EntityId,
                TalkType = message.TalkType,
                NoteCount = message.NoteCount,
                Volume = message.Volume * selfVolumeMultiplier,
                Pitch = message.Pitch,
            }
            : message;
    }

    internal static int CountQuotedSpeechLength(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return 0;
        }

        var segments = message.Split('"');
        var speechLength = 0;
        for (var i = 1; i < segments.Length; i += 2)
        {
            speechLength += segments[i].Length;
        }

        return speechLength;
    }

    internal static int GetNoteCount(int speechLength)
    {
        return speechLength switch
        {
            <= 24 => 2,
            <= 80 => 3,
            _ => 4,
        };
    }

    private static bool IsSilentLanguage(MessageContext context)
    {
        return context.TryGetMetadata(MessageContext.LANGUAGE, out Language lang)
            && lang == LanguageSystem.SignLanguage;
    }

    private static bool TryGetSpeechMessageLength(MessageContext context, ModConfig config, out int speechLength)
    {
        speechLength = 0;
        if (!context.TryGetSpeechText(out var speechText) || string.IsNullOrWhiteSpace(speechText))
        {
            return false;
        }

        speechLength = UsesProsePresentation(config)
            ? CountQuotedSpeechLength(speechText)
            : speechText.Length;
        return speechLength > 0;
    }

    private static bool TryGetEmoteSpeechLength(string message, out int speechLength)
    {
        speechLength = CountQuotedSpeechLength(message);
        return speechLength > 0;
    }

    private static bool UsesProsePresentation(ModConfig config)
    {
        return ProximityChatPresentationModes.Normalize(config.ProximityChatPresentationMode) == ProximityChatPresentationModes.Prose;
    }

    private static int GetTalkType(ProximityChatMode mode)
    {
        return mode == ProximityChatMode.Whisper
            ? (int)EnumTalkType.IdleShort
            : (int)EnumTalkType.Idle;
    }

    private static float GetModeValue(IDictionary<ProximityChatMode, float> values, ProximityChatMode mode, float fallback)
    {
        return values.TryGetValue(mode, out var value) ? value : fallback;
    }
}
