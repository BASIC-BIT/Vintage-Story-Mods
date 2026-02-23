using System.Linq;
using System.Globalization;
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

    private static bool IsDecoratorChar(char c)
    {
        // Handle zalgo-like effects (temporal storm/drunk) that add combining marks.
        var cat = CharUnicodeInfo.GetUnicodeCategory(c);
        return cat == UnicodeCategory.NonSpacingMark ||
            cat == UnicodeCategory.SpacingCombiningMark ||
            cat == UnicodeCategory.EnclosingMark ||
            cat == UnicodeCategory.Format;
    }

    private static bool TryParseLanguageSpecifier(string message, out string languageIdentifier, out string remainder)
    {
        languageIdentifier = null;
        remainder = null;

        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var i = 0;
        // Skip whitespace and any stray combining/format characters.
        while (i < message.Length && (char.IsWhiteSpace(message[i]) || IsDecoratorChar(message[i])))
        {
            i++;
        }

        if (i >= message.Length || message[i] != ':')
        {
            return false;
        }

        i++;
        // Skip decorators right after ':' (e.g. zalgo).
        while (i < message.Length && IsDecoratorChar(message[i]))
        {
            i++;
        }

        // Collect identifier chars, ignoring decorators in-between.
        var sb = new StringBuilder();
        while (i < message.Length)
        {
            var c = message[i];
            if (IsDecoratorChar(c))
            {
                i++;
                continue;
            }

            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
                i++;
                continue;
            }

            break;
        }

        if (sb.Length == 0)
        {
            return false;
        }

        languageIdentifier = sb.ToString();

        // Skip whitespace and decorators between identifier and content.
        while (i < message.Length && (char.IsWhiteSpace(message[i]) || IsDecoratorChar(message[i])))
        {
            i++;
        }

        remainder = i < message.Length ? message[i..] : string.Empty;
        return true;
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return true;
    }

    public override MessageContext Transform(MessageContext context)
    {
        // Always populate a default language for downstream formatting.
        // If the language system is disabled, we intentionally do NOT parse :lang prefixes.
        var defaultLang = _config.Languages?.FirstOrDefault(l => l.Default) ?? _config.Languages?.FirstOrDefault();
        if (defaultLang == null)
        {
            defaultLang = LanguageSystem.BabbleLang;
        }

        var languageEnabled = _config.EnableLanguageSystem && !_config.DisableRPChat;
        if (!languageEnabled)
        {
            context.SetMetadata(MessageContext.LANGUAGE, defaultLang);
            return context;
        }

        if (TryParseLanguageSpecifier(context.Message, out var languageIdentifier, out var remainder))
        {
            // First try to get the language with allowHidden=true to check if it exists at all
            var lang = _languageSystem.GetLangFromText(languageIdentifier, true, allowHidden: true);
            
            // Determine if we should show hidden languages in the error message
            bool showHidden = context.SendingPlayer.HasPrivilege(_config.ChangeOtherLanguagePermission);
            
            // If the language doesn't exist, or it's hidden and player can't use it, show error
            if (lang == null || (lang.Hidden && !context.SendingPlayer.KnowsLanguage(lang) && !showHidden))
            {
                context.SendingPlayer.SendMessage(
                    _chatSystem.ProximityChatId,
                    Lang.Get("thebasics:lang-error-invalid-with-list", languageIdentifier, string.Join(", ",
                        _languageSystem.GetAllLanguages(true, includeHidden: showHidden).Select(listLang => ChatHelper.LangColor(":" + listLang.Prefix + " (" + listLang.Name + ")", listLang)))),
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

            // If the message is empty, set the default language and stop processing
            if (string.IsNullOrWhiteSpace(remainder))
            {
                context.SendingPlayer.SetDefaultLanguage(lang);
                context.SendingPlayer.SendMessage(
                    _chatSystem.ProximityChatId,
                    Lang.Get("thebasics:lang-success-now-speaking", lang.Name),
                    EnumChatType.CommandSuccess);
                context.State = MessageContextState.STOP;
            } else {
                // Remove the language identifier and continue processing
                context.UpdateMessage(remainder.Trim());
                context.SetMetadata(MessageContext.LANGUAGE, lang);
            }
        } else {
            try
            {
                context.SetMetadata(MessageContext.LANGUAGE, context.SendingPlayer.GetDefaultLanguage(_config));
            }
            catch
            {
                context.SetMetadata(MessageContext.LANGUAGE, defaultLang);
            }
        }
        
        return context;
    }
}
