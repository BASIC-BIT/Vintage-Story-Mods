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
        // TODO: Is this condition accurate? Should we check for roleplay/player chat flags?
        return true;
    }

    public override MessageContext Transform(MessageContext context)
    {
        if (LanguageTalkRegex.IsMatch(context.Message))
        {
            var match = LanguageTalkRegex.Match(context.Message);
            var languageIdentifier = match.Groups[1].Value;
            
            // First try to get the language with allowHidden=true to check if it exists at all
            var lang = _languageSystem.GetLangFromText(languageIdentifier, true, allowHidden: true);
            
            // Determine if we should show hidden languages in the error message
            bool showHidden = context.SendingPlayer.HasPrivilege(_config.ChangeOtherLanguagePermission);
            
            // If the language doesn't exist, or it's hidden and player can't use it, show error
            if (lang == null || (lang.Hidden && !context.SendingPlayer.KnowsLanguage(lang) && !showHidden))
            {
                context.SendingPlayer.SendMessage(
                    _chatSystem.ProximityChatId,
                    $"Invalid language specifier \":{languageIdentifier}\".  Valid prefixes include: " + string.Join(", ",
                        _languageSystem.GetAllLanguages(true, includeHidden: showHidden).Select(listLang => ChatHelper.LangColor(":" + listLang.Prefix + " (" + listLang.Name + ")", listLang))),
                    EnumChatType.CommandError);
                context.State = MessageContextState.STOP;
                return context;
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
                context.State = MessageContextState.STOP;
            } else {
                // Remove the language identifier and continue processing
                context.UpdateMessage(match.Groups[2].Value);
                context.SetMetadata(MessageContext.LANGUAGE, lang);
            }
        } else {
            context.SetMetadata(MessageContext.LANGUAGE, context.SendingPlayer.GetDefaultLanguage(_config));
        }
        
        return context;
    }
}
