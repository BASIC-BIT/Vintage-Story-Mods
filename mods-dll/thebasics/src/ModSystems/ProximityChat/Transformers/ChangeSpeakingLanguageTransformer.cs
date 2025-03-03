using System;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;

namespace thebasics.src.ModSystems.ProximityChat.Transformers;

public class ChangeSpeakingLanguageTransformer : IMessageTransformer
{
    private readonly LanguageSystem _languageSystem;
    private readonly RPProximityChatSystem _chatSystem;

    public ChangeSpeakingLanguageTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem)
    {
        _chatSystem = chatSystem;
        _languageSystem = languageSystem;
    }
    private static readonly Regex LanguageTalkRegex = new(@"^\s*:(\w+)\s*(.*)$");

    public MessageContext Transform(MessageContext context)
    {
        if (LanguageTalkRegex.IsMatch(context.Message))
        {
            var match = LanguageTalkRegex.Match(context.Message);
            var languageIdentifier = match.Groups[1].Value;
            var lang = _languageSystem.GetLangFromText(languageIdentifier, true, context.SendingPlayer.HasPrivilege(_chatSystem.GetModConfig().ChangeOtherLanguagePermission));

            if (lang == null)
            {
                context.SendingPlayer.SendMessage(
                    _chatSystem.GetProximityChatGroupId(),
                    $"Invalid language specifier \":{languageIdentifier}\".  Valid prefixes include: " + string.Join(", ",
                        _languageSystem.GetAllLanguages(true).Select(listLang => ChatHelper.LangColor(":" + listLang.Prefix + " (" + listLang.Name + ")", listLang))),
                    EnumChatType.CommandError);
                throw new Exception($"Invalid language specifier \":{languageIdentifier}\"");
            }

            if (lang.Name != LanguageSystem.BabbleLang.Name && !context.SendingPlayer.KnowsLanguage(lang))
            {
                context.SendingPlayer.SendMessage(
                    _chatSystem.GetProximityChatGroupId(),
                    "You don't know that language!",
                    EnumChatType.CommandError);
                throw new Exception($"Character doesn't know language {ChatHelper.LangIdentifier(lang)}!");
            }

            // If the message is empty, set the default language and stop processing
            if(!match.Groups[2].Success || string.IsNullOrWhiteSpace(match.Groups[2].Value))
            {
                context.SendingPlayer.SetDefaultLanguage(lang);
                context.SendingPlayer.SendMessage(
                    _chatSystem.GetProximityChatGroupId(),
                    "You are now speaking " + lang.Name + ".",
                    EnumChatType.CommandSuccess);
                context.State = MessageContextState.STOP;
            } else {
                // Remove the language identifier and continue processing
                context.Message = match.Groups[2].Value;
                context.Metadata["language"] = lang;
            }
        }
        
        return context;
    }
}