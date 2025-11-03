#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
        public HeritageLanguageSystem HeritageLanguageSystem { get; }

        public LanguageSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config) : base(system, api,
            config)
        {
            // Player commands
            API.ChatCommands.GetOrCreate("addlang")
                .WithAlias("addlanguage")
                .WithDescription(Lang.Get("thebasics:command.addlang.description"))
                .RequiresPrivilege(Config.ChangeOwnLanguagePermission)
                .WithArgs(new WordArgParser(Lang.Get("thebasics:command.arg.language"), true))
                .RequiresPlayer()
                .HandleWith(HandleAddLanguageCommand);

            API.ChatCommands.GetOrCreate("removelang")
                .WithAlias("removelanguage", "remlang", "remlanguage")
                .WithDescription(Lang.Get("thebasics:command.removelang.description"))
                .RequiresPrivilege(Config.ChangeOwnLanguagePermission)
                .WithArgs(new WordArgParser(Lang.Get("thebasics:command.arg.language"), true))
                .RequiresPlayer()
                .HandleWith(HandleRemoveLanguageCommand);

            API.ChatCommands.GetOrCreate("listlang")
                .WithAlias("listlanguage", "listlanguages")
                .WithDescription(Lang.Get("thebasics:command.listlang.description"))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(HandleListLanguagesCommand);

            // Admin commands
            API.ChatCommands.GetOrCreate("adminaddlang")
                .WithAlias("adminaddlanguage")
                .WithDescription(Lang.Get("thebasics:command.adminaddlang.description"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser(Lang.Get("thebasics:command.arg.player"), API, true),
                    new WordArgParser(Lang.Get("thebasics:command.arg.language"), true))
                .HandleWith(HandleAdminAddLanguageCommand);

            API.ChatCommands.GetOrCreate("adminremovelang")
                .WithAlias("adminremovelanguage")
                .WithDescription(Lang.Get("thebasics:command.adminremovelang.description"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser(Lang.Get("thebasics:command.arg.player"), API, true),
                    new WordArgParser(Lang.Get("thebasics:command.arg.language"), true))
                .HandleWith(HandleAdminRemoveLanguageCommand);

            API.ChatCommands.GetOrCreate("adminlistlang")
                .WithAlias("adminlistlanguage")
                .WithDescription(Lang.Get("thebasics:command.adminlistlang.description"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser(Lang.Get("thebasics:command.arg.player"), API, true))
                .HandleWith(HandleAdminListLanguagesCommand);

            var playerModelLibEnabled = API.ModLoader.IsModEnabled("playermodellib");
            HeritageLanguageSystem = new HeritageLanguageSystem(System, API, Config, playerModelLibEnabled);

        }

        public static readonly Language BabbleLang = new Language("Babble", "Unintelligible", "babble", ["ba", "ble", "bla", "bal"], "#FF0000", false, true);
        public static readonly Language SignLanguage = new Language("Sign", "A visual language using hand gestures and movements", "sign", [], "#A0A0A0", false, false);

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
                    StatusMessage = Lang.Get("thebasics:language.error.invalidSpecifier", languageIdentifier),
                };
            }

            // Check if player already knows this language
            if (player.KnowsLanguage(lang))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:language.error.alreadyKnown", ChatHelper.LangIdentifier(lang)),
                };
            }

            // Check language limit
            var currentLanguages = player.GetLanguages();
            if (Config.MaxLanguagesPerPlayer >= 0 && currentLanguages.Count >= Config.MaxLanguagesPerPlayer)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:language.error.maxLanguages", Config.MaxLanguagesPerPlayer),
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
                StatusMessage = Lang.Get("thebasics:language.success.added", ChatHelper.LangIdentifier(lang)),
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
                    StatusMessage = Lang.Get("thebasics:language.error.invalidSpecifier", languageIdentifier),
                };
            }

            if (!player.KnowsLanguage(language))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:language.error.notKnown", ChatHelper.LangIdentifier(language)),
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
                    player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:language.notification.none", ChatHelper.LangColor("babble", BabbleLang)), EnumChatType.Notification);
                    player.SetDefaultLanguage(BabbleLang);
                }
                else
                {
                    var newDefaultIdentifier = newPlayerLanguages.First();
                    var newDefault = GetLangFromText(newDefaultIdentifier, false);
                    player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:language.notification.newDefault", ChatHelper.LangIdentifier(newDefault)), EnumChatType.Notification);
                    player.SetDefaultLanguage(newDefault);
                }
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:language.success.removed", ChatHelper.LangIdentifier(language)),
            };
        }

        private TextCommandResult HandleListLanguagesCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var languages = GetPlayerLanguages(player);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get(
                    "thebasics:language.status.listself",
                    string.Join(", ", languages.Select(ChatHelper.LangIdentifier)),
                    string.Join(", ", GetAllLanguages(false, includeHidden: false).Select(ChatHelper.LangIdentifier))),
            };
        }

        private TextCommandResult HandleAdminAddLanguageCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languageIdentifier = (string)args.Parsers[1].GetValue();
            var lang = GetLangFromText(languageIdentifier, true, allowHidden: true);

            if (lang == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:language.error.invalidSpecifier", languageIdentifier),
                };
            }

            // Check if player already knows this language
            if (targetPlayer.KnowsLanguage(lang))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:language.error.playerAlreadyKnows", targetPlayer.PlayerName, ChatHelper.LangIdentifier(lang)),
                };
            }

            // Check language limit
            var currentLanguages = targetPlayer.GetLanguages();
            if (Config.MaxLanguagesPerPlayer >= 0 && currentLanguages.Count >= Config.MaxLanguagesPerPlayer)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:language.error.playerMax", targetPlayer.PlayerName, Config.MaxLanguagesPerPlayer),
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
                StatusMessage = Lang.Get("thebasics:language.success.addedOther", ChatHelper.LangIdentifier(lang), targetPlayer.PlayerName),
            };
        }

        private TextCommandResult HandleAdminRemoveLanguageCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languageIdentifier = (string)args.Parsers[1].GetValue();
            var language = GetLangFromText(languageIdentifier, false, allowHidden: true);

            if (language == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:language.error.invalidSpecifier", languageIdentifier),
                };
            }

            if (!targetPlayer.KnowsLanguage(language))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:language.error.playerDoesNotKnow", targetPlayer.PlayerName, ChatHelper.LangIdentifier(language)),
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
                    player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:language.notification.playerNone", targetPlayer.PlayerName, ChatHelper.LangColor("babble", BabbleLang)), EnumChatType.Notification);
                    targetPlayer.SetDefaultLanguage(BabbleLang);
                }
                else
                {
                    var newDefaultIdentifier = newPlayerLanguages.First();
                    var newDefault = GetLangFromText(newDefaultIdentifier, false);
                    if (newDefault == null)
                    {
                        player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:language.notification.playerNoDefault", targetPlayer.PlayerName), EnumChatType.Notification);
                        targetPlayer.SetDefaultLanguage(BabbleLang);
                    }
                    else
                    {
                        player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:language.notification.playerNewDefault", targetPlayer.PlayerName, ChatHelper.LangColor(newDefault.Name, newDefault)), EnumChatType.Notification);
                        targetPlayer.SetDefaultLanguage(newDefault);
                    }
                }
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:language.success.removedOther", ChatHelper.LangIdentifier(language), targetPlayer.PlayerName),
            };
        }

        private TextCommandResult HandleAdminListLanguagesCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languages = GetPlayerLanguages(targetPlayer);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get(
                    "thebasics:language.status.listOther",
                    targetPlayer.PlayerName,
                    string.Join(", ", languages.Select(ChatHelper.LangIdentifier)),
                    string.Join(", ", GetAllLanguages(true, includeHidden: true).Select(ChatHelper.LangIdentifier))),
            };
        }

        private List<Language> GetPlayerLanguages(IServerPlayer player)
        {
            return player.GetLanguages()
                .Select(lang => GetLangFromText(lang, false, allowHidden: true))
                .Where(lang => lang != null)
                .Cast<Language>()  // Cast after filtering out nulls
                .ToList();
        }


        public Language? GetLangFromText(string text, bool allowBabble, bool allowHidden = false)
        {
            return GetAllLanguages(allowBabble).FirstOrDefault(lang =>
                (allowHidden || !lang.Hidden) &&
                (lang?.Prefix?.ToLower() == text.ToLower() ||
                lang?.Name?.ToLower() == text.ToLower()));
        }

        public List<Language> GetAllLanguages(bool allowBabble, bool includeHidden = true)
        {
            List<Language> languages = new();
            
            if (includeHidden)
            {
                languages.AddRange(Config.Languages);
            }
            else
            {
                languages.AddRange(Config.Languages.Where(lang => !lang.Hidden));
            }

            // Always include SignLanguage
            languages.Add(SignLanguage);

            if (allowBabble)
            {
                languages.Add(BabbleLang);
            }

            return languages;
        }

        public void ProcessMessage(IServerPlayer receivingPlayer,
            ref string message, Language lang)
        {
            if (Config.EnableLanguageSystem && !receivingPlayer.KnowsLanguage(lang))
            {
                var scrambledMessage = LanguageScrambler.ScrambleMessage(message, lang);
                message = ChatHelper.Italic(scrambledMessage);
            }
        }

    }
}
