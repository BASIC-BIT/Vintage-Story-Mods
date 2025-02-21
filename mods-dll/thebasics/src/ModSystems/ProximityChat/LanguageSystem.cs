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
            // Player commands
            API.ChatCommands.GetOrCreate("addlang")
                .WithAlias("addlanguage")
                .WithDescription("Add one of your known languages")
                .RequiresPrivilege(Config.ChangeOwnLanguagePermission)
                .WithArgs(new WordArgParser("language", true))
                .RequiresPlayer()
                .HandleWith(HandleAddLanguageCommand);

            API.ChatCommands.GetOrCreate("removelang")
                .WithAlias("removelanguage", "remlang", "remlanguage")
                .WithDescription("Remove one of your known languages")
                .RequiresPrivilege(Config.ChangeOwnLanguagePermission)
                .WithArgs(new WordArgParser("language", true))
                .RequiresPlayer()
                .HandleWith(HandleRemoveLanguageCommand);

            API.ChatCommands.GetOrCreate("listlang")
                .WithAlias("listlanguage", "listlanguages")
                .WithDescription("List your known languages, and all available languages.")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(HandleListLanguagesCommand);
            
            // Admin commands
            API.ChatCommands.GetOrCreate("adminaddlang")
                .WithAlias("adminaddlanguage")
                .WithDescription("Add other player's known languages")
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("language", true))
                .HandleWith(HandleAdminAddLanguageCommand);

            API.ChatCommands.GetOrCreate("adminremovelang")
                .WithAlias("adminremovelanguage")
                .WithDescription("Remove other player's known languages")
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("language", true))
                .HandleWith(HandleAdminRemoveLanguageCommand);
            
            API.ChatCommands.GetOrCreate("adminlistlang")
                .WithAlias("adminlistlanguage")
                .WithDescription("List other player's languages")
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true))
                .HandleWith(HandleAdminListLanguagesCommand);

            api.Event.PlayerJoin += player =>
            {
                player.InstantiateLanguagesIfNotExist(config);
            };
        }
        
        public static readonly Language BabbleLang = new Language("Babble", "Unintelligible", "babble", new[] { "ba", "ble", "bla", "bal" }, "#FF0000", false, true);
        
        private TextCommandResult HandleAddLanguageCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var languageIdentifier = (string)args.Parsers[0].GetValue();
            var lang = GetLangFromText(languageIdentifier, false);

            if (lang == null || lang.Hidden)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Invalid language specifier \":{languageIdentifier}\"",
                };
            }

            // Check if player already knows this language
            if (player.KnowsLanguage(lang))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"You already know the language {ChatHelper.LangIdentifier(lang)}!",
                };
            }

            // Check language limit
            var currentLanguages = player.GetLanguages();
            if (Config.MaxLanguagesPerPlayer >= 0 && currentLanguages.Count >= Config.MaxLanguagesPerPlayer)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"You cannot learn more than {Config.MaxLanguagesPerPlayer} languages! Remove one first.",
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
                StatusMessage = $"Language {ChatHelper.LangIdentifier(lang)} added!",
            };
        }

        private TextCommandResult HandleRemoveLanguageCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var languageIdentifier = (string)args.Parsers[0].GetValue();
            var language = GetLangFromText(languageIdentifier, false);

            if (language == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Invalid language specifier \":{languageIdentifier}\"",
                };
            }
            
            if (!player.KnowsLanguage(language))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"You don't know the language {ChatHelper.LangIdentifier(language)}!",
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
                    player.SendMessage(GlobalConstants.CurrentChatGroup, $"You unlearned your default language, your new default is {ChatHelper.LangIdentifier(newDefault)}", EnumChatType.Notification);
                    player.SetDefaultLanguage(newDefault);
                }
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Language {ChatHelper.LangIdentifier(language)} removed!",
            };
        }

        private TextCommandResult HandleListLanguagesCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var languages = GetPlayerLanguages(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "You know: " + string.Join(", ", languages.Select(ChatHelper.LangIdentifier)) +
                                "\n" +
                                "All languages: " + string.Join(", ", GetAllLanguages(false).Where(l => !l.Hidden).Select(ChatHelper.LangIdentifier)),
            };
        }
        
        private TextCommandResult HandleAdminAddLanguageCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languageIdentifier = (string)args.Parsers[1].GetValue();
            var lang = GetLangFromText(languageIdentifier, true);

            if (lang == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Invalid language specifier \":{languageIdentifier}\"",
                };
            }

            // Check if player already knows this language
            if (targetPlayer.KnowsLanguage(lang))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"{targetPlayer.PlayerName} already knows the language {ChatHelper.LangIdentifier(lang)}!",
                };
            }

            // Check language limit
            var currentLanguages = targetPlayer.GetLanguages();
            if (Config.MaxLanguagesPerPlayer >= 0 && currentLanguages.Count >= Config.MaxLanguagesPerPlayer)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"{targetPlayer.PlayerName} cannot learn more than {Config.MaxLanguagesPerPlayer} languages! Remove one first.",
                };
            }

            targetPlayer.AddLanguage(lang);
            
            // Set players default language if their current language is babble
            var defaultLang = targetPlayer.GetDefaultLanguage(Config);
            if(defaultLang == null || defaultLang.Name == BabbleLang.Name)
            {
                targetPlayer.SetDefaultLanguage(lang);
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Language {ChatHelper.LangIdentifier(lang)} added to player {targetPlayer.PlayerName}!",
            };
        }

        private TextCommandResult HandleAdminRemoveLanguageCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languageIdentifier = (string)args.Parsers[1].GetValue();
            var language = GetLangFromText(languageIdentifier, false);

            if (language == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Invalid language specifier \":{languageIdentifier}\"",
                };
            }
            
            if (!targetPlayer.KnowsLanguage(language))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"{targetPlayer.PlayerName} doesn't know the language {ChatHelper.LangIdentifier(language)}!",
                };
            }

            targetPlayer.RemoveLanguage(language);

            // If they just removed their default language, set it to the first language they know
            var defaultLang = targetPlayer.GetDefaultLanguage(Config);
            if (defaultLang == null || defaultLang.Name == language.Name)
            {
                var newPlayerLanguages = targetPlayer.GetLanguages();
                
                // If the player now knows no languages, set their default to babble
                if (newPlayerLanguages.Count == 0)
                {
                    player.SendMessage(GlobalConstants.CurrentChatGroup, $"{targetPlayer.PlayerName} now knows no languages! If they try to speak, they will only {ChatHelper.LangColor("babble", BabbleLang)} incoherently!", EnumChatType.Notification);
                    targetPlayer.SetDefaultLanguage(BabbleLang);
                }
                else
                {
                    var newDefaultIdentifier = newPlayerLanguages.First();
                    var newDefault = GetLangFromText(newDefaultIdentifier, false);
                    if (newDefault == null)
                    {
                        player.SendMessage(GlobalConstants.CurrentChatGroup, $"{targetPlayer.PlayerName} unlearned their default language, but no valid default language was found!", EnumChatType.Notification);
                        targetPlayer.SetDefaultLanguage(BabbleLang);
                    }
                    else 
                    {
                        player.SendMessage(GlobalConstants.CurrentChatGroup, $"{targetPlayer.PlayerName} unlearned their default language, their new default is {ChatHelper.LangColor(newDefault.Name, newDefault)}", EnumChatType.Notification);
                        targetPlayer.SetDefaultLanguage(newDefault);
                    }
                }
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Language {ChatHelper.LangIdentifier(language)} removed from {targetPlayer.PlayerName}!",
            };
        }

        private TextCommandResult HandleAdminListLanguagesCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languages = GetPlayerLanguages(targetPlayer);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"{targetPlayer.PlayerName} knows: " + string.Join(", ", languages.Select(ChatHelper.LangIdentifier)) +
                                "\n" +
                                "All languages: " + string.Join(", ", GetAllLanguages(true).Select(ChatHelper.LangIdentifier)),
            };
        }

        private List<Language> GetPlayerLanguages(IServerPlayer player)
        {
            return player.GetLanguages()
                .Select(lang => GetLangFromText(lang, false))
                .Where(lang => lang != null)
                .Cast<Language>()  // Cast after filtering out nulls
                .ToList();
        }

        private static readonly Regex LanguageSwapRegex = new(@"^\s*:(\w+)\s*$");
        private static readonly Regex LanguageTalkRegex = new(@"^\s*:(\w+)\s+(.*)$");

        private Language? GetLangFromText(string text, bool allowBabble)
        {
            return GetAllLanguages(allowBabble).FirstOrDefault(lang =>
                lang?.Prefix?.ToLower() == text.ToLower() ||
                lang?.Name?.ToLower() == text.ToLower());
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
                            GetAllLanguages(true).Select(listLang => ChatHelper.LangColor($":{listLang.Prefix} ({listLang.Name})", listLang))),
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
                            GetAllLanguages(true).Select(listLang => ChatHelper.LangColor(":" + listLang.Prefix + " (" + listLang.Name + ")", listLang))),
                        EnumChatType.CommandError);
                    throw new Exception($"Invalid language specifier \":{languageIdentifier}\"");
                }

                if (lang.Name != BabbleLang.Name && !sendingPlayer.KnowsLanguage(lang))
                {
                    API.SendMessage(sendingPlayer, groupId, "You don't know that language!", EnumChatType.CommandError);
                    throw new Exception($"Character doesn't know language {ChatHelper.LangIdentifier(lang)}!");
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