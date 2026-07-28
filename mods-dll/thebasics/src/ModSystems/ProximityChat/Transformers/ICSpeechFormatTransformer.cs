using System;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ICSpeechFormatTransformer : MessageTransformerBase
{
    private readonly LanguageSystem _languageSystem;
    private readonly DistanceObfuscationSystem _distanceObfuscationSystem;

    public ICSpeechFormatTransformer(
        RPProximityChatSystem chatSystem,
        LanguageSystem languageSystem = null,
        DistanceObfuscationSystem distanceObfuscationSystem = null) : base(chatSystem)
    {
        _languageSystem = languageSystem;
        _distanceObfuscationSystem = distanceObfuscationSystem;
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_SPEECH);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var lang = context.GetMetadata<Language>(MessageContext.LANGUAGE);
        var languageEnabled = _config.EnableLanguageSystem && !_config.DisableRPChat;
        var nickname = context.GetMetadata<string>(MessageContext.FORMATTED_NAME);
        var mode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());
        var presentationMode = ProximityChatPresentationModes.Normalize(_config.ProximityChatPresentationMode);

        // Note: content is escaped earlier in the pipeline for speech

        if (presentationMode == ProximityChatPresentationModes.Prose)
        {
            return FormatProseSpeech(context, lang, languageEnabled, nickname);
        }

        // Resolve the verb from the sender-phase speech body, before recipient formatting rewrites it.
        var verb = ChatHelper.GetProximityChatVerb(lang, mode, _config, GetSpeechTextForVerb(context));

        context.Message = FormatSpeechBody(context, lang, languageEnabled, presentationMode);

        context.Message = presentationMode switch
        {
            ProximityChatPresentationModes.SimpleSpeech => $"{nickname}: {context.Message}",
            ProximityChatPresentationModes.PlainProximity => $"{nickname}: {context.Message}",
            ProximityChatPresentationModes.Prose => context.Message,
            _ => $"{nickname} {verb} {context.Message}"
        };

        return context;
    }

    private MessageContext FormatProseSpeech(MessageContext context, Language lang, bool languageEnabled, string nickname)
    {
        var messageToFormat = context.Message;
        var hasDistanceFontSize = TryUnwrapDistanceFontSize(messageToFormat, out var unwrappedMessage, out var distanceFontSize);
        if (hasDistanceFontSize)
        {
            messageToFormat = unwrappedMessage;
        }

        context.Message = ChatHelper.FormatProseMessage(
            messageToFormat,
            lang,
            _config,
            languageEnabled,
            text => ProcessProseQuotedText(context, text, lang, languageEnabled),
            nickname,
            text => ChatVisualPreferenceResolver.FormatLanguageText(text, lang, context.ReceivingPlayer));

        context.Message = ChatHelper.ApplyFreeformAttribution(context.Message, context.SendingPlayer, _config);
        if (hasDistanceFontSize)
        {
            context.Message = $"<font size=\"{distanceFontSize}\">{context.Message}</font>";
        }

        return context;
    }

    private string FormatSpeechBody(MessageContext context, Language lang, bool languageEnabled, string presentationMode)
    {
        var message = context.Message;
        if (ProximityChatPresentationModes.UsesSpeechQuotes(presentationMode))
        {
            message = ChatHelper.WrapSpeechQuotes(message, lang, _config, languageEnabled);
        }

        if (!languageEnabled)
        {
            return message;
        }

        if (lang == LanguageSystem.SignLanguage)
        {
            message = ChatHelper.Italic(message);
        }

        return ChatVisualPreferenceResolver.FormatLanguageText(message, lang, context.ReceivingPlayer);
    }

    /// <summary>
    /// The speech text as it stood at the end of the sender phase. Recipient-phase transformers rewrite
    /// <see cref="MessageContext.Message"/> per recipient (language scrambling, obfuscation, font tags),
    /// so the raw speech body is the only source that yields the same verb for everyone.
    /// </summary>
    private static string GetSpeechTextForVerb(MessageContext context)
    {
        return context.TryGetSpeechText(out var speechText) ? speechText : context.Message;
    }

    private string ProcessProseQuotedText(MessageContext context, string text, Language lang, bool languageEnabled)
    {
        var processed = text;

        if (languageEnabled && _languageSystem != null)
        {
            _languageSystem.ProcessMessage(context.ReceivingPlayer, ref processed, lang);
        }

        if (_distanceObfuscationSystem != null && context.SendingPlayer != null && context.ReceivingPlayer != null)
        {
            _distanceObfuscationSystem.ObfuscateMessage(context.SendingPlayer, context.ReceivingPlayer, ref processed,
                occlusionPenalty: context.GetOcclusionPenalty(context.ReceivingPlayer));
        }

        return processed;
    }

    private static bool TryUnwrapDistanceFontSize(string message, out string innerMessage, out string fontSize)
    {
        innerMessage = message;
        fontSize = null;

        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var match = Regex.Match(message, "^<font size=\"([^\"]+)\">(.*)</font>$", RegexOptions.Singleline);
        if (!match.Success)
        {
            return false;
        }

        fontSize = match.Groups[1].Value;
        innerMessage = match.Groups[2].Value;
        return true;
    }
}
