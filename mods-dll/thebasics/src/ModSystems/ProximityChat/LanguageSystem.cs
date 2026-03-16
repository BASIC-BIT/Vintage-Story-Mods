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
        public HeritageLanguageSystem? HeritageLanguageSystem { get; }

        public LanguageSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config) : base(system, api,
            config)
        {
            // Language system is an RP feature. If RP chat or languages are disabled, do not register commands/events.
            if (!Config.EnableLanguageSystem || Config.DisableRPChat)
            {
                return;
            }

            // Player commands
            API.ChatCommands.GetOrCreate("addlang")
                .WithAlias("addlanguage")
                .WithDescription(Lang.Get("thebasics:lang-cmd-addlang-desc"))
                .RequiresPrivilege(Config.ChangeOwnLanguagePermission)
                .WithArgs(new WordArgParser("language", true))
                .RequiresPlayer()
                .HandleWith(HandleAddLanguageCommand);

            API.ChatCommands.GetOrCreate("removelang")
                .WithAlias("removelanguage", "remlang", "remlanguage")
                .WithDescription(Lang.Get("thebasics:lang-cmd-removelang-desc"))
                .RequiresPrivilege(Config.ChangeOwnLanguagePermission)
                .WithArgs(new WordArgParser("language", true))
                .RequiresPlayer()
                .HandleWith(HandleRemoveLanguageCommand);

            API.ChatCommands.GetOrCreate("listlang")
                .WithAlias("listlanguage", "listlanguages")
                .WithDescription(Lang.Get("thebasics:lang-cmd-listlang-desc"))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(HandleListLanguagesCommand);

            // Admin commands
            API.ChatCommands.GetOrCreate("adminaddlang")
                .WithAlias("adminaddlanguage")
                .WithDescription(Lang.Get("thebasics:lang-cmd-adminaddlang-desc"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("language", true))
                .HandleWith(HandleAdminAddLanguageCommand);

            API.ChatCommands.GetOrCreate("adminremovelang")
                .WithAlias("adminremovelanguage")
                .WithDescription(Lang.Get("thebasics:lang-cmd-adminremovelang-desc"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("language", true))
                .HandleWith(HandleAdminRemoveLanguageCommand);

            API.ChatCommands.GetOrCreate("adminlistlang")
                .WithAlias("adminlistlanguage")
                .WithDescription(Lang.Get("thebasics:lang-cmd-adminlistlang-desc"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true))
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
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid", languageIdentifier),
                };
            }

            // Check if player already knows this language
            if (player.KnowsLanguage(lang))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-already-known", ChatHelper.LangIdentifier(lang)),
                };
            }

            // Check language limit
            var currentLanguages = player.GetLanguages();
            if (Config.MaxLanguagesPerPlayer >= 0 && currentLanguages.Count >= Config.MaxLanguagesPerPlayer)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-max-languages", Config.MaxLanguagesPerPlayer),
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
                StatusMessage = Lang.Get("thebasics:lang-success-added", ChatHelper.LangIdentifier(lang)),
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
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid", languageIdentifier),
                };
            }

            if (!player.KnowsLanguage(language))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-not-known", ChatHelper.LangIdentifier(language)),
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
                    player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-no-languages", ChatHelper.LangColor("babble", BabbleLang)), EnumChatType.Notification);
                    player.SetDefaultLanguage(BabbleLang);
                }
                else
                {
                    var newDefaultIdentifier = newPlayerLanguages.First();
                    var newDefault = GetLangFromText(newDefaultIdentifier, false);
                    player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-new-default", ChatHelper.LangIdentifier(newDefault)), EnumChatType.Notification);
                    player.SetDefaultLanguage(newDefault);
                }
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:lang-success-removed", ChatHelper.LangIdentifier(language)),
            };
        }

        private TextCommandResult HandleListLanguagesCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var known = GetPlayerLanguages(player);
            var knownNames = new HashSet<string>(known.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
            var unknown = GetAllLanguages(false, includeHidden: false)
                .Where(l => !knownNames.Contains(l.Name))
                .ToList();

            var knownList = string.Join("\n  ", known.Select(ChatHelper.LangIdentifierWithDescription));
            var message = Lang.Get("thebasics:lang-list-known", known.Count > 0 ? "\n  " + knownList : knownList);

            if (unknown.Count > 0)
            {
                var unknownList = string.Join("\n  ", unknown.Select(ChatHelper.LangIdentifierWithDescription));
                message += "\n" + Lang.Get("thebasics:lang-list-unknown", "\n  " + unknownList);
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = message,
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
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid", languageIdentifier),
                };
            }

            // Check if player already knows this language
            if (targetPlayer.KnowsLanguage(lang))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-admin-already-known", targetPlayer.PlayerName, ChatHelper.LangIdentifier(lang)),
                };
            }

            // Check language limit
            var currentLanguages = targetPlayer.GetLanguages();
            if (Config.MaxLanguagesPerPlayer >= 0 && currentLanguages.Count >= Config.MaxLanguagesPerPlayer)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-admin-max-languages", targetPlayer.PlayerName, Config.MaxLanguagesPerPlayer),
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
                StatusMessage = Lang.Get("thebasics:lang-success-admin-added", ChatHelper.LangIdentifier(lang), targetPlayer.PlayerName),
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
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid", languageIdentifier),
                };
            }

            if (!targetPlayer.KnowsLanguage(language))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-admin-not-known", targetPlayer.PlayerName, ChatHelper.LangIdentifier(language)),
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
                    player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-admin-no-languages", targetPlayer.PlayerName, ChatHelper.LangColor("babble", BabbleLang)), EnumChatType.Notification);
                    targetPlayer.SetDefaultLanguage(BabbleLang);
                }
                else
                {
                    var newDefaultIdentifier = newPlayerLanguages.First();
                    var newDefault = GetLangFromText(newDefaultIdentifier, false);
                    if (newDefault == null)
                    {
                        player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-admin-no-valid-default", targetPlayer.PlayerName), EnumChatType.Notification);
                        targetPlayer.SetDefaultLanguage(BabbleLang);
                    }
                    else
                    {
                        player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-admin-new-default", targetPlayer.PlayerName, ChatHelper.LangColor(newDefault.Name, newDefault)), EnumChatType.Notification);
                        targetPlayer.SetDefaultLanguage(newDefault);
                    }
                }
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:lang-success-admin-removed", ChatHelper.LangIdentifier(language), targetPlayer.PlayerName),
            };
        }

        private TextCommandResult HandleAdminListLanguagesCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languages = GetPlayerLanguages(targetPlayer);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:lang-list-admin-known", targetPlayer.PlayerName, string.Join(", ", languages.Select(ChatHelper.LangIdentifier))) +
                                "\n" +
                                Lang.Get("thebasics:lang-list-all", string.Join(", ", GetAllLanguages(true, includeHidden: true).Select(ChatHelper.LangIdentifier))),
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

        [Obsolete("Use HeritageLanguageSystem watcher-driven class handling")]
        public void OnPlayerClassChanged(IServerPlayer player, string oldClass, string newClass)
        {
            _ = oldClass;
            _ = newClass;

            if (player == null)
            {
                return;
            }

            HeritageLanguageSystem?.ReconcilePlayerClassChange(player);
        }
    }
}
