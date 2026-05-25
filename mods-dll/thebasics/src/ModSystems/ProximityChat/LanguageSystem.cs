#nullable enable
#pragma warning disable S1133 // Deprecated bridge method is retained for compatibility with older call sites.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.Analytics;
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
        private const int MinimumRecognizableNameLength = 3;

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
                    StatusMessage = Lang.Get("thebasics:lang-error-already-known", ChatHelper.LangIdentifier(lang, player)),
                };
            }

            // Check language limit
            var currentLanguages = GetPlayerLanguages(player);
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
            if (player.GetDefaultLanguage(Config).Name == BabbleLang.Name)
            {
                player.SetDefaultLanguage(lang);
            }

            AnalyticsService.TrackCommandUsed("addlang", true);
            AnalyticsService.TrackFeatureUsed("language", "add");

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:lang-success-added", ChatHelper.LangIdentifier(lang, player)),
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
                    StatusMessage = Lang.Get("thebasics:lang-error-not-known", ChatHelper.LangIdentifier(language, player)),
                };
            }

            player.RemoveLanguage(language);

            // If they just removed their default language, set it to the first language they know
            if (player.GetDefaultLanguage(Config).Name == language.Name)
            {
                var newPlayerLanguages = GetPlayerLanguages(player);

                // If the player now knows no languages, set their default to babble
                if (newPlayerLanguages.Count == 0)
                {
                    player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-no-languages", ChatVisualPreferenceResolver.FormatLanguageText("babble", BabbleLang, player)), EnumChatType.Notification);
                    player.SetDefaultLanguage(BabbleLang);
                }
                else
                {
                    var newDefault = newPlayerLanguages[0];
                    player.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-new-default", ChatHelper.LangIdentifier(language, player), ChatHelper.LangIdentifier(newDefault, player)), EnumChatType.Notification);
                    player.SetDefaultLanguage(newDefault);
                }
            }

            AnalyticsService.TrackCommandUsed("removelang", true);
            AnalyticsService.TrackFeatureUsed("language", "remove");

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:lang-success-removed", ChatHelper.LangIdentifier(language, player)),
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

            var knownList = string.Join("\n  ", known.Select(lang => ChatHelper.LangIdentifierWithDescription(lang, player)));
            var message = Lang.Get("thebasics:lang-list-known", known.Count > 0 ? "\n  " + knownList : knownList);

            if (unknown.Count > 0)
            {
                var unknownList = string.Join("\n  ", unknown.Select(lang => ChatHelper.LangIdentifierWithDescription(lang, player)));
                message += "\n" + Lang.Get("thebasics:lang-list-unknown", "\n  " + unknownList);
            }

            AnalyticsService.TrackCommandUsed("listlang", true);
            AnalyticsService.TrackFeatureUsed("language", "list");

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = message,
            };
        }

        private TextCommandResult HandleAdminAddLanguageCommand(TextCommandCallingArgs args)
        {
            var player = GetCallerPlayer(args);
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
                    StatusMessage = Lang.Get("thebasics:lang-error-admin-already-known", targetPlayer.PlayerName, ChatHelper.LangIdentifier(lang, player)),
                };
            }

            // Player self-add respects the configured language limit; admin add can bypass it.
            var currentLanguages = GetPlayerLanguages(targetPlayer);
            var exceedsConfiguredLimit = Config.MaxLanguagesPerPlayer >= 0 && currentLanguages.Count >= Config.MaxLanguagesPerPlayer;

            targetPlayer.AddLanguage(lang);

            // Set players default language if their current language is babble
            var defaultLang = targetPlayer.GetDefaultLanguage(Config);
            if (defaultLang == null || defaultLang.Name == BabbleLang.Name)
            {
                targetPlayer.SetDefaultLanguage(lang);
            }

            var statusMessage = Lang.Get("thebasics:lang-success-admin-added", ChatHelper.LangIdentifier(lang, player), targetPlayer.PlayerName);
            if (exceedsConfiguredLimit)
            {
                statusMessage += "\n" + Lang.Get("thebasics:lang-warning-admin-over-max-languages", targetPlayer.PlayerName, targetPlayer.GetLanguages().Count, Config.MaxLanguagesPerPlayer);
            }

            AnalyticsService.TrackCommandUsed("adminaddlang", true);
            AnalyticsService.TrackFeatureUsed("language", "admin_add");

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = statusMessage,
            };
        }

        private TextCommandResult HandleAdminRemoveLanguageCommand(TextCommandCallingArgs args)
        {
            var player = GetCallerPlayer(args);
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
                    StatusMessage = Lang.Get("thebasics:lang-error-admin-not-known", targetPlayer.PlayerName, ChatHelper.LangIdentifier(language, player)),
                };
            }

            targetPlayer.RemoveLanguage(language);

            // If they just removed their default language, set it to the first language they know
            var defaultLang = targetPlayer.GetDefaultLanguage(Config);
            if (defaultLang == null || defaultLang.Name == language.Name)
            {
                var newPlayerLanguages = GetPlayerLanguages(targetPlayer);

                // If the player now knows no languages, set their default to babble
                if (newPlayerLanguages.Count == 0)
                {
                    player?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-admin-no-languages", targetPlayer.PlayerName, ChatVisualPreferenceResolver.FormatLanguageText("babble", BabbleLang, player)), EnumChatType.Notification);
                    targetPlayer.SetDefaultLanguage(BabbleLang);
                }
                else
                {
                    var newDefault = newPlayerLanguages[0];
                    player?.SendMessage(GlobalConstants.CurrentChatGroup, Lang.Get("thebasics:lang-notify-admin-new-default", targetPlayer.PlayerName, ChatHelper.LangIdentifier(language, player), ChatVisualPreferenceResolver.FormatLanguageText(newDefault.Name, newDefault, player)), EnumChatType.Notification);
                    targetPlayer.SetDefaultLanguage(newDefault);
                }
            }

            AnalyticsService.TrackCommandUsed("adminremovelang", true);
            AnalyticsService.TrackFeatureUsed("language", "admin_remove");

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:lang-success-admin-removed", ChatHelper.LangIdentifier(language, player), targetPlayer.PlayerName),
            };
        }

        private TextCommandResult HandleAdminListLanguagesCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var player = GetCallerPlayer(args);
            var languages = GetPlayerLanguages(targetPlayer);
            AnalyticsService.TrackCommandUsed("adminlistlang", true);
            AnalyticsService.TrackFeatureUsed("language", "admin_list");
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:lang-list-admin-known", targetPlayer.PlayerName, string.Join(", ", languages.Select(lang => ChatHelper.LangIdentifier(lang, player)))) +
                                "\n" +
                                Lang.Get("thebasics:lang-list-all", string.Join(", ", GetAllLanguages(true, includeHidden: true).Select(lang => ChatHelper.LangIdentifier(lang, player)))),
            };
        }

        private IServerPlayer? GetCallerPlayer(TextCommandCallingArgs args)
        {
            var playerUid = args.Caller.Player?.PlayerUID;
            return string.IsNullOrWhiteSpace(playerUid) ? null : API.GetPlayerByUID(playerUid);
        }

        private List<Language> GetPlayerLanguages(IServerPlayer player)
        {
            PruneUnknownPlayerLanguages(player);

            return player.GetLanguages()
                .Select(lang => GetLangFromText(lang, false, allowHidden: true))
                .Where(lang => lang != null)
                .Cast<Language>()  // Cast after filtering out nulls
                .ToList();
        }

        private void PruneUnknownPlayerLanguages(IServerPlayer player)
        {
            var currentNames = player.GetLanguages();
            var validNames = new HashSet<string>(
                GetAllLanguages(false, includeHidden: true).Select(lang => lang.Name),
                StringComparer.OrdinalIgnoreCase);

            var prunedNames = currentNames
                .Where(validNames.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!currentNames.SequenceEqual(prunedNames, StringComparer.OrdinalIgnoreCase))
            {
                player.SetLanguages(prunedNames);
            }

            var defaultLanguageName = player.GetDefaultLanguageName();
            var defaultIsKnown = string.Equals(defaultLanguageName, BabbleLang.Name, StringComparison.OrdinalIgnoreCase) ||
                prunedNames.Contains(defaultLanguageName, StringComparer.OrdinalIgnoreCase);
            if (defaultIsKnown)
            {
                return;
            }

            var newDefaultName = prunedNames.FirstOrDefault();
            var newDefault = newDefaultName == null
                ? BabbleLang
                : GetLangFromText(newDefaultName, false, allowHidden: true) ?? BabbleLang;
            player.SetDefaultLanguage(newDefault);
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
                var preservedWords = string.Equals(lang.Name, BabbleLang.Name, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : GetRecognizableNameWords(receivingPlayer);

                // Visual/gestural languages (like sign language) have no syllables to
                // scramble into. Replace each word with random ASCII gesture symbols,
                // mirroring the syllable-based scrambling for spoken languages.
                if (lang.Syllables == null || lang.Syllables.Length == 0)
                {
                    message = ChatHelper.Italic(ScrambleAsGestures(message, preservedWords));
                    return;
                }

                var scrambledMessage = LanguageScrambler.ScrambleMessage(message, lang, preservedWords);
                message = ChatHelper.Italic(scrambledMessage);
            }
        }

        private ISet<string> GetRecognizableNameWords(IServerPlayer receivingPlayer)
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddRecognizableNameWords(words, receivingPlayer.PlayerName);
            AddRecognizableNameWords(words, receivingPlayer.GetNickname(Config));
            return words;
        }

        private static void AddRecognizableNameWords(ISet<string> words, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            foreach (var word in Regex.Matches(name, @"\w+").Cast<Match>().Select(match => match.Value))
            {
                if (word.Length >= MinimumRecognizableNameLength)
                {
                    words.Add(word);
                }
            }
        }

        /// <summary>
        /// Replaces each word with random ASCII gesture symbols, deterministically seeded
        /// by the word content (same word always produces the same symbols, matching the
        /// spoken-language scrambler's consistency). Shorter and more playful than a
        /// verbose description like "makes unintelligible gestures".
        /// </summary>
        private static readonly char[] GestureSymbols = { '*', '-', '.', '~', '#', '+', '?' };
        private static string ScrambleAsGestures(string message, ISet<string>? preservedWords)
        {
            var wordRegex = new Regex(@"\w+");
            return wordRegex.Replace(message, match =>
            {
                var word = match.Groups[0].Value;
                if (preservedWords?.Contains(word) == true)
                {
                    return word;
                }

                var hash = word.Select((c, i) => (int)c * (i + 1)).Aggregate((a, b) => a + b);
                var rng = new Random(hash);
                var len = Math.Max(1, (word.Length + 1) / 2); // roughly half the word length
                var symbols = new char[len];
                for (var i = 0; i < len; i++)
                {
                    symbols[i] = GestureSymbols[rng.Next(GestureSymbols.Length)];
                }
                return new string(symbols);
            });
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
