#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat
{
    public class LanguageSystem : BaseSubSystem
    {
        public LanguageSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config) : base(system, api,
            config)
        {
            API.ChatCommands.GetOrCreate("addlang")
                .WithAlias("addlanguage")
                .WithDescription("Add one of your known languages")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(new StringArgParser("language", true))
                .HandleWith(AddLanguage);

            API.ChatCommands.GetOrCreate("removelang")
                .WithAlias("removelanguage", "remlang", "remlanguage")
                .WithDescription("Remove one of your known languages")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(new StringArgParser("language", true))
                .HandleWith(RemoveLanguage);

            API.ChatCommands.GetOrCreate("listlang")
                .WithAlias("listlanguage", "listlanguages")
                .WithDescription("List your known languages, and all available languages.")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(ListLanguages);
            
            api.Event.PlayerJoin += player =>
            {
                player.InstantiateLanguagesIfNotExist(config);
            };
        }
        
        public static readonly Language BabbleLang = new("Babble", "Unintelligible", "babble", new[] { "ba", "ble", "bla", "bal" }, "#FF0000");
        
        private TextCommandResult AddLanguage(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var languageIdentifier = (string)args.Parsers[0].GetValue();
            var lang = GetLangFromText(languageIdentifier, false);

            if (lang == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Invalid language specifier \":{languageIdentifier}\"",
                };
            }

            player.AddLanguage(lang);
            
            // Set players default language if their current language is babble
            if(player.GetDefaultLanguage(Config).Name == BabbleLang.Name)
            {
                player.SetDefaultLanguage(lang);
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Language {ChatHelper.LangColor(lang.Name, lang)} added!",
            };
        }

        private TextCommandResult RemoveLanguage(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var languageIdentifier = (string)args.Parsers[0].GetValue();
            var language = GetLangFromText(languageIdentifier, false);

            if (!player.KnowsLanguage(language))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"You don't know the language {ChatHelper.LangColor(language.Name, language)}!",
                };
            }

            player.RemoveLanguage(language);

            // If they just removed their default language, set it to the first language they know
            if (player.GetDefaultLanguage(Config).Name == language.Name)
            {
                var newPlayerLanguages = player.GetLanguages();
                
                // If the player now knows no languages, set their default to babble
                if (newPlayerLanguages.Count == 0)
                {
                    player.SendMessage(GlobalConstants.CurrentChatGroup, $"You now know no languages! If you try to speak, you will only {ChatHelper.LangColor("babble", BabbleLang)} incoherently!", EnumChatType.Notification);
                    player.SetDefaultLanguage(BabbleLang);
                }
                else
                {
                    var newDefaultIdentifier = newPlayerLanguages.First();
                    var newDefault = GetLangFromText(newDefaultIdentifier, false);
                    player.SendMessage(GlobalConstants.CurrentChatGroup, $"You unlearned your default language, your new default is {ChatHelper.LangColor(newDefault.Name, newDefault)}", EnumChatType.Notification);
                    player.SetDefaultLanguage(newDefault);
                }
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Language {ChatHelper.LangColor(language.Name, language)} removed!",
            };
        }

        private TextCommandResult ListLanguages(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var languages = GetPlayerLanguages(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "You know: " + string.Join(", ", languages.Select(lang => ChatHelper.LangColor(lang.Name, lang))) +
                                "\n" +
                                "All languages: " + string.Join(", ", GetAllLanguages(false).Select(lang => ChatHelper.LangColor(lang.Name, lang))),
            };
        }

        private List<Language> GetPlayerLanguages(IServerPlayer player)
        {
            return player.GetLanguages()
                .Select((lang) => GetLangFromText(lang, false))
                .Cast<Language>()
                .ToList();
        }

        private static readonly Regex LanguageSwapRegex = new(@"^\s*:(\w+)\s*$");
        private static readonly Regex LanguageTalkRegex = new(@"^\s*:(\w+)\s+(.*)$");

        private Language? GetLangFromText(string text, bool allowBabble)
        {
            return GetAllLanguages(allowBabble).FirstOrDefault(lang =>
                lang.Prefix.ToLower() == text.ToLower() ||
                lang.Name.ToLower() == text.ToLower());
        }

        private List<Language> GetAllLanguages(bool allowBabble)
        {
            List<Language> languages = new();
            languages.AddRange(Config.Languages);
            if (allowBabble)
            {
                languages.Add(BabbleLang);
            }
            return languages;
        }

        public bool HandleSwapLanguage(IServerPlayer sendingPlayer, int groupId, string message)
        {
            if (LanguageSwapRegex.IsMatch(ChatHelper.GetMessage(message)))
            {
                var match = LanguageSwapRegex.Match(ChatHelper.GetMessage(message));
                var languageIdentifier = match.Groups[1].Value;

                var lang = GetLangFromText(languageIdentifier, true);
                
                if (lang == null)
                {
                    API.SendMessage(sendingPlayer, groupId,
                        $"Invalid language specifier \":{languageIdentifier}\".  Valid prefixes include: " + string.Join(", ",
                            GetAllLanguages(true).Select(lang => ChatHelper.LangColor($":{lang.Prefix} ({lang.Name})", lang))),
                        EnumChatType.CommandError);
                    return true;
                }

                if (lang.Name != BabbleLang.Name && !sendingPlayer.KnowsLanguage(lang))
                {
                    API.SendMessage(sendingPlayer, groupId, "You don't know that language!", EnumChatType.CommandError);
                    return true;
                }

                sendingPlayer.SetDefaultLanguage(lang);
                API.SendMessage(sendingPlayer, groupId, "You are now speaking " + lang.Name + ".",
                    EnumChatType.CommandSuccess);

                return true;
            }

            return false;
        }

        public Language GetSpeakingLanguage(IServerPlayer sendingPlayer, int groupId, ref string message)
        {
            if (LanguageTalkRegex.IsMatch(message))
            {
                var match = LanguageTalkRegex.Match(message);
                var languageIdentifier = match.Groups[1].Value;
                var lang = GetLangFromText(languageIdentifier, true);

                if (lang == null)
                {
                    API.SendMessage(sendingPlayer, groupId,
                        $"Invalid language specifier \":{languageIdentifier}\".  Valid prefixes include: " + string.Join(", ",
                            GetAllLanguages(true).Select(lang => ":" + lang.Prefix + " (" + lang.Name + ")")),
                        EnumChatType.CommandError);
                    throw new Exception($"Invalid language specifier \":{languageIdentifier}\"");
                }

                if (lang.Name != BabbleLang.Name && !sendingPlayer.KnowsLanguage(lang))
                {
                    API.SendMessage(sendingPlayer, groupId, "You don't know that language!", EnumChatType.CommandError);
                    throw new Exception($"Character doesn't know language {ChatHelper.LangColor(lang.Name, lang)}!");
                }

                message = match.Groups[2].Value;
                return lang;
            }

            return sendingPlayer.GetDefaultLanguage(Config);
        }

        public void ProcessMessage(IServerPlayer receivingPlayer,
            ref string message, Language lang)
        {
            if (Config.EnableLanguageSystem && !receivingPlayer.KnowsLanguage(lang))
            {
                message = LanguageScrambler.ScrambleMessage(message, lang);
            }
        }
    }
}