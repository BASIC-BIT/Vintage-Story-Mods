using System;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ChangeSpeakingLanguageTransformer : MessageTransformerBase
{
    private readonly LanguageSystem _languageSystem;

    public ChangeSpeakingLanguageTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem) : base(chatSystem)
    {
        _languageSystem = languageSystem;
    }
    private static readonly Regex LanguageTalkRegex = new(@"^\s*:(\w+)\s*(.*)$");

    public override bool ShouldTransform(MessageContext context)
    {
        return true;
    }

    public override MessageContext Transform(MessageContext context)
    {
        if (LanguageTalkRegex.IsMatch(context.Message))
        {
            var match = LanguageTalkRegex.Match(context.Message);
            var languageIdentifier = match.Groups[1].Value;
            var lang = _languageSystem.GetLangFromText(languageIdentifier, true, context.SendingPlayer.HasPrivilege(_chatSystem.Config.ChangeOtherLanguagePermission));

            if (lang == null)
            {
                context.SendingPlayer.SendMessage(
                    _chatSystem.ProximityChatId,
                    $"Invalid language specifier \":{languageIdentifier}\".  Valid prefixes include: " + string.Join(", ",
                        _languageSystem.GetAllLanguages(true).Select(listLang => ChatHelper.LangColor(":" + listLang.Prefix + " (" + listLang.Name + ")", listLang))),
                    EnumChatType.CommandError);
                context.State = MessageContextState.STOP;
            }

            if (lang.Name != LanguageSystem.BabbleLang.Name && !context.SendingPlayer.KnowsLanguage(lang))
            {
                context.SendingPlayer.SendMessage(
                    _chatSystem.ProximityChatId,
                    "You don't know that language!",
                    EnumChatType.CommandError);
                context.State = MessageContextState.STOP;
            }

            // If the message is empty, set the default language and stop processing
            if(!match.Groups[2].Success || string.IsNullOrWhiteSpace(match.Groups[2].Value))
            {
                context.SendingPlayer.SetDefaultLanguage(lang);
                context.SendingPlayer.SendMessage(
                    _chatSystem.ProximityChatId,
                    "You are now speaking " + lang.Name + ".",
                    EnumChatType.CommandSuccess);
            } else {
                // Remove the language identifier and continue processing
                context.Message = match.Groups[2].Value;
                context.SetMetadata(MessageContext.LANGUAGE, lang);
            }
        } else {
            context.SetMetadata(MessageContext.LANGUAGE, context.SendingPlayer.GetDefaultLanguage(_chatSystem.Config));
        }
        
        return context;
    }
}