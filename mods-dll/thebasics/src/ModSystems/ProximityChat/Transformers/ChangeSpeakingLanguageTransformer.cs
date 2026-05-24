using System.Linq;
using System.Text;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ChangeSpeakingLanguageTransformer : MessageTransformerBase
{
    private readonly LanguageSystem _languageSystem;

    public ChangeSpeakingLanguageTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem) : base(chatSystem)
    {
        _languageSystem = languageSystem;
    }

    private static bool TryParseLanguageSpecifier(string message, out string languageIdentifier, out string remainder)
    {
        languageIdentifier = null;
        remainder = null;

        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var index = 0;
        SkipWhitespaceAndDecorators(message, ref index);

        if (index >= message.Length || message[index] != ':')
        {
            return false;
        }

        index++;
        // Skip decorators right after ':' (e.g. zalgo).
        SkipDecorators(message, ref index);

        if (!TryReadLanguageIdentifier(message, ref index, out languageIdentifier))
        {
            return false;
        }

        // Skip whitespace and decorators between identifier and content.
        SkipWhitespaceAndDecorators(message, ref index);

        remainder = index < message.Length ? message[index..] : string.Empty;
        return true;
    }

    private static void SkipWhitespaceAndDecorators(string message, ref int index)
    {
        while (index < message.Length && (char.IsWhiteSpace(message[index]) || ChatHelper.IsDecoratorChar(message[index])))
        {
            index++;
        }
    }

    private static void SkipDecorators(string message, ref int index)
    {
        while (index < message.Length && ChatHelper.IsDecoratorChar(message[index]))
        {
            index++;
        }
    }

    private static bool TryReadLanguageIdentifier(string message, ref int index, out string languageIdentifier)
    {
        var sb = new StringBuilder();
        while (index < message.Length)
        {
            var c = message[index];
            if (ChatHelper.IsDecoratorChar(c))
            {
                index++;
                continue;
            }

            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                break;
            }

            sb.Append(c);
            index++;
        }

        languageIdentifier = sb.ToString();
        return languageIdentifier.Length > 0;
    }

    private static bool StartsWithLanguagePrefixSyntax(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var i = 0;
        SkipWhitespaceAndDecorators(message, ref i);

        return i < message.Length && message[i] == ':';
    }

    private string BuildValidLanguageList(bool includeHidden, Vintagestory.API.Server.IServerPlayer recipient)
    {
        var languages = _languageSystem.GetAllLanguages(true, includeHidden: includeHidden)
            .Where(lang => includeHidden || !lang.Hidden)
            .Select(lang => ChatHelper.LangIdentifierWithDescription(lang, recipient));

        return "\n  " + string.Join("\n  ", languages);
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return true;
    }

    public override MessageContext Transform(MessageContext context)
    {
        // Always populate a default language for downstream formatting.
        // If the language system is disabled, we intentionally do NOT parse :lang prefixes.
        var defaultLang = GetConfiguredDefaultLanguage();

        var languageEnabled = _config.EnableLanguageSystem && !_config.DisableRPChat;
        if (!languageEnabled)
        {
            return HandleDisabledLanguageSystem(context, defaultLang);
        }

        if (TryParseLanguageSpecifier(context.Message, out var languageIdentifier, out var remainder))
        {
            return HandleLanguageSpecifier(context, languageIdentifier, remainder);
        }

        if (StartsWithLanguagePrefixSyntax(context.Message))
        {
            return RejectInvalidPrefix(context);
        }

        SetDefaultLanguageMetadata(context, defaultLang);
        return context;
    }

    private Language GetConfiguredDefaultLanguage()
    {
        return _config.Languages?.FirstOrDefault(l => l.Default) ??
               _config.Languages?.FirstOrDefault() ??
               LanguageSystem.BabbleLang;
    }

    private MessageContext HandleDisabledLanguageSystem(MessageContext context, Language defaultLang)
    {
        context.SetMetadata(MessageContext.LANGUAGE, defaultLang);
        if (TryParseLanguageSpecifier(context.Message, out _, out _))
        {
            context.SendingPlayer.SendMessage(
                _chatSystem.ProximityChatId,
                Lang.Get("thebasics:lang-error-system-disabled"),
                EnumChatType.CommandError);
            context.State = MessageContextState.STOP;
        }

        return context;
    }

    private MessageContext HandleLanguageSpecifier(MessageContext context, string languageIdentifier, string remainder)
    {
        var showHidden = context.SendingPlayer.HasPrivilege(_config.ChangeOtherLanguagePermission);
        var lang = _languageSystem.GetLangFromText(languageIdentifier, true, allowHidden: true);

        if (!CanUseLanguage(context, lang, showHidden))
        {
            context.SendingPlayer.SendMessage(
                _chatSystem.ProximityChatId,
                Lang.Get("thebasics:lang-error-invalid-with-list", languageIdentifier, BuildValidLanguageList(showHidden, context.SendingPlayer)),
                EnumChatType.CommandError);
            context.State = MessageContextState.STOP;
            return context;
        }

        if (lang.Name != LanguageSystem.BabbleLang.Name && !context.SendingPlayer.KnowsLanguage(lang))
        {
            context.SendingPlayer.SendMessage(
                _chatSystem.ProximityChatId,
                Lang.Get("thebasics:lang-error-unknown-language"),
                EnumChatType.CommandError);
            context.State = MessageContextState.STOP;
            return context;
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            context.SendingPlayer.SetDefaultLanguage(lang);
            context.SendingPlayer.SendMessage(
                _chatSystem.ProximityChatId,
                Lang.Get("thebasics:lang-success-now-speaking", lang.Name),
                EnumChatType.CommandSuccess);
            context.State = MessageContextState.STOP;
            return context;
        }

        context.UpdateMessage(remainder.Trim());
        context.SetMetadata(MessageContext.LANGUAGE, lang);
        return context;
    }

    private static bool CanUseLanguage(MessageContext context, Language lang, bool showHidden)
    {
        return lang != null && (!lang.Hidden || context.SendingPlayer.KnowsLanguage(lang) || showHidden);
    }

    private MessageContext RejectInvalidPrefix(MessageContext context)
    {
        var showHidden = context.SendingPlayer.HasPrivilege(_config.ChangeOtherLanguagePermission);
        context.SendingPlayer.SendMessage(
            _chatSystem.ProximityChatId,
            Lang.Get("thebasics:lang-error-invalid-prefix-with-list", BuildValidLanguageList(showHidden, context.SendingPlayer)),
            EnumChatType.CommandError);
        context.State = MessageContextState.STOP;
        return context;
    }

    private void SetDefaultLanguageMetadata(MessageContext context, Language defaultLang)
    {
        try
        {
            context.SetMetadata(MessageContext.LANGUAGE, context.SendingPlayer.GetDefaultLanguage(_config));
        }
        catch
        {
            context.SetMetadata(MessageContext.LANGUAGE, defaultLang);
        }
    }
}
