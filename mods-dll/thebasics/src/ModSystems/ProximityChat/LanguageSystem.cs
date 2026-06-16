#nullable enable
#pragma warning disable S1133 // Deprecated bridge method is retained for compatibility with older call sites.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Semantics;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat
{
    public class LanguageSystem : BaseSubSystem
    {
        public HeritageLanguageSystem? HeritageLanguageSystem { get; }
        public SemanticLanguageService SemanticLanguageService { get; }

        public LanguageSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config) : base(system, api,
            config)
        {
            SemanticLanguageService = new SemanticLanguageService(
                this,
                API,
                Config.SemanticLanguageLearning,
                () => ProximityChatPresentationModes.Normalize(Config.ProximityChatPresentationMode) == ProximityChatPresentationModes.Prose);

            // Language system is an RP feature. If RP chat or languages are disabled, do not register commands/events.
            if (!Config.EnableLanguageSystem || Config.DisableRPChat)
            {
                return;
            }

            RegisterPlayerCommands();
            RegisterAdminCommands();

            var playerModelLibEnabled = API.ModLoader.IsModEnabled("playermodellib");
            HeritageLanguageSystem = new HeritageLanguageSystem(System, API, Config, playerModelLibEnabled);
        }

        private void RegisterPlayerCommands()
        {
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

            API.ChatCommands.GetOrCreate("langprogress")
                .WithAlias("languageprogress", "langsemanticprogress")
                .WithDescription(Lang.Get("thebasics:lang-cmd-langprogress-desc"))
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .WithArgs(new WordArgParser("language", true))
                .HandleWith(HandleLanguageProgressCommand);
        }

        private void RegisterAdminCommands()
        {
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

            API.ChatCommands.GetOrCreate("adminsetlangskill")
                .WithAlias("adminsetlanguageproficiency", "adminsetlangproficiency")
                .WithDescription(Lang.Get("thebasics:lang-cmd-adminsetlangskill-desc"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("language", true),
                    new WordArgParser("percent", true))
                .HandleWith(HandleAdminSetLanguageSkillCommand);

            API.ChatCommands.GetOrCreate("adminsetlangbucket")
                .WithAlias("adminsetlanguagebucket", "adminsetlangbucketexpertise")
                .WithDescription(Lang.Get("thebasics:lang-cmd-adminsetlangbucket-desc"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("language", true),
                    new StringArgParser("bucket percent", true))
                .HandleWith(HandleAdminSetLanguageBucketCommand);

            API.ChatCommands.GetOrCreate("adminlangsemantic")
                .WithAlias("adminlanguagesemantic", "adminsemanticlang", "adminlangsemantics")
                .WithDescription(Lang.Get("thebasics:lang-cmd-adminlangsemantic-desc"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .HandleWith(HandleAdminLanguageSemanticStatusCommand);

            API.ChatCommands.GetOrCreate("adminlangprogress")
                .WithAlias("adminlanguageprogress", "adminlangsemanticprogress")
                .WithDescription(Lang.Get("thebasics:lang-cmd-adminlangprogress-desc"))
                .RequiresPrivilege(Config.ChangeOtherLanguagePermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("language", true))
                .HandleWith(HandleAdminLanguageProgressCommand);
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
            var learning = GetPlayerLanguageSkills(player);
            var learningNames = new HashSet<string>(learning.Select(entry => entry.Language.Name), StringComparer.OrdinalIgnoreCase);
            var unknown = GetAllLanguages(false, includeHidden: false)
                .Where(l => !knownNames.Contains(l.Name) && !learningNames.Contains(l.Name))
                .ToList();

            var knownList = string.Join("\n  ", known.Select(lang => ChatHelper.LangIdentifierWithDescription(lang, player)));
            var message = Lang.Get("thebasics:lang-list-known", known.Count > 0 ? "\n  " + knownList : knownList);

            if (learning.Count > 0)
            {
                var learningList = string.Join("\n  ", learning.Select(entry => FormatLanguageSkill(entry.Language, entry.Skill, player)));
                message += "\n" + Lang.Get("thebasics:lang-list-learning", "\n  " + learningList);
            }

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
            var learning = GetPlayerLanguageSkills(targetPlayer);
            AnalyticsService.TrackCommandUsed("adminlistlang", true);
            AnalyticsService.TrackFeatureUsed("language", "admin_list");
            var message = Lang.Get("thebasics:lang-list-admin-known", targetPlayer.PlayerName, string.Join(", ", languages.Select(lang => ChatHelper.LangIdentifier(lang, player)))) +
                          "\n" +
                          Lang.Get("thebasics:lang-list-all", string.Join(", ", GetAllLanguages(true, includeHidden: true).Select(lang => ChatHelper.LangIdentifier(lang, player))));
            if (learning.Count > 0)
            {
                message += "\n" + Lang.Get("thebasics:lang-list-admin-learning", targetPlayer.PlayerName, string.Join(", ", learning.Select(entry => FormatLanguageSkill(entry.Language, entry.Skill, player))));
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = message,
            };
        }

        private TextCommandResult HandleAdminSetLanguageSkillCommand(TextCommandCallingArgs args)
        {
            var player = GetCallerPlayer(args);
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languageIdentifier = (string)args.Parsers[1].GetValue();
            var skillText = (string)args.Parsers[2].GetValue();
            var language = GetLangFromText(languageIdentifier, false, allowHidden: true);

            if (language == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid", languageIdentifier),
                };
            }

            if (!int.TryParse(skillText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skill) || skill < 0 || skill > 100)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid-skill"),
                };
            }

            if (targetPlayer.KnowsLanguage(language))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-admin-skill-known", targetPlayer.PlayerName, ChatHelper.LangIdentifier(language, player)),
                };
            }

            targetPlayer.SetLanguageSkill(language, skill);
            AnalyticsService.TrackCommandUsed("adminsetlangskill", true);
            AnalyticsService.TrackFeatureUsed("language", "admin_set_skill");

            var message = skill <= 0
                ? Lang.Get("thebasics:lang-success-admin-skill-cleared", ChatHelper.LangIdentifier(language, player), targetPlayer.PlayerName)
                : Lang.Get("thebasics:lang-success-admin-skill-set", targetPlayer.PlayerName, ChatHelper.LangIdentifier(language, player), skill);

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = message,
            };
        }

        private TextCommandResult HandleAdminSetLanguageBucketCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            var languageIdentifier = (string)args.Parsers[1].GetValue();
            var bucketAndSkill = (string)args.Parsers[2].GetValue();
            var language = GetLangFromText(languageIdentifier, false, allowHidden: true);

            if (language == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid", languageIdentifier),
                };
            }

            if (!TryParseAtlasBucketAndSkill(bucketAndSkill, out var bucketIdentifier, out var skill))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid-skill"),
                };
            }

            if (!SemanticLanguageService.TrySetAtlasBucketCoverage(targetPlayer, language, bucketIdentifier, skill, out var bucket, out var errorCode))
            {
                var message = errorCode == "unavailable"
                    ? Lang.Get("thebasics:lang-error-atlas-unavailable")
                    : Lang.Get("thebasics:lang-error-invalid-atlas-bucket", VtmlUtils.EscapeVtml(bucketIdentifier), SemanticLanguageService.Atlas.FormatSuggestions());
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = message,
                };
            }

            AnalyticsService.TrackCommandUsed("adminsetlangbucket", true);
            AnalyticsService.TrackFeatureUsed("language", "admin_set_bucket");

            var bucketName = SemanticLanguageAtlasCatalog.FormatBucket(bucket!);
            var languageName = FormatPlainLanguageIdentifier(language);
            var targetName = VtmlUtils.EscapeVtml(targetPlayer.PlayerName);
            var messageText = skill <= 0
                ? Lang.Get("thebasics:lang-success-admin-bucket-cleared", bucketName, languageName, targetName)
                : Lang.Get("thebasics:lang-success-admin-bucket-set", targetName, bucketName, languageName, skill);

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = messageText,
            };
        }

        internal static bool TryParseAtlasBucketAndSkill(string? value, out string bucketIdentifier, out int skill)
        {
            bucketIdentifier = string.Empty;
            skill = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            var splitIndex = trimmed.LastIndexOf(' ');
            if (splitIndex <= 0 || splitIndex >= trimmed.Length - 1)
            {
                return false;
            }

            var skillText = trimmed[(splitIndex + 1)..].Trim();
            if (!int.TryParse(skillText, NumberStyles.Integer, CultureInfo.InvariantCulture, out skill) || skill < 0 || skill > 100)
            {
                skill = 0;
                return false;
            }

            bucketIdentifier = trimmed[..splitIndex].Trim();
            return !string.IsNullOrWhiteSpace(bucketIdentifier);
        }

        private TextCommandResult HandleAdminLanguageSemanticStatusCommand(TextCommandCallingArgs args)
        {
            _ = args;

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:lang-semantic-status", VtmlUtils.EscapeVtml(SemanticEmbeddingProviderStatus)),
            };
        }

        private TextCommandResult HandleLanguageProgressCommand(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var languageIdentifier = (string)args.Parsers[0].GetValue();
            var language = GetLangFromText(languageIdentifier, false, allowHidden: true);
            if (language == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:lang-error-invalid", languageIdentifier),
                };
            }

            AnalyticsService.TrackCommandUsed("langprogress", true);
            AnalyticsService.TrackFeatureUsed("language", "semantic_progress");

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = FormatSemanticProgress(player, language),
            };
        }

        private TextCommandResult HandleAdminLanguageProgressCommand(TextCommandCallingArgs args)
        {
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

            AnalyticsService.TrackCommandUsed("adminlangprogress", true);
            AnalyticsService.TrackFeatureUsed("language", "admin_semantic_progress");

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = FormatSemanticProgress(targetPlayer, language, targetPlayer.PlayerName),
            };
        }

        private string FormatSemanticProgress(IServerPlayer subject, Language language, string? subjectName = null)
        {
            var progress = SemanticLanguageService.BuildProgress(subject, language);
            var learnedBuckets = progress.LearnedAtlasBuckets.Count == 0
                ? Lang.Get("thebasics:lang-semantic-progress-top-none")
                : string.Join(", ", progress.LearnedAtlasBuckets.Select(VtmlUtils.EscapeVtml));
            var inProgressBuckets = progress.InProgressAtlasBuckets.Count == 0
                ? Lang.Get("thebasics:lang-semantic-progress-top-none")
                : string.Join(", ", progress.InProgressAtlasBuckets.Select(VtmlUtils.EscapeVtml));

            return Lang.Get(
                "thebasics:lang-semantic-progress",
                VtmlUtils.EscapeVtml(subjectName ?? Lang.Get("thebasics:lang-semantic-progress-you")),
                FormatPlainLanguageIdentifier(language),
                progress.AtlasCoveredBucketCount,
                progress.AtlasBucketCount,
                progress.AtlasCoveragePercent,
                progress.AtlasLearnedBucketCount,
                learnedBuckets,
                inProgressBuckets);
        }

        private static string FormatPlainLanguageIdentifier(Language language)
        {
            if (language == null)
            {
                return string.Empty;
            }

            var hiddenMarker = language.Hidden ? " [hidden]" : string.Empty;
            return VtmlUtils.EscapeVtml($"{language.Name} (:{language.Prefix}){hiddenMarker}");
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

        private List<(Language Language, int Skill)> GetPlayerLanguageSkills(IServerPlayer player)
        {
            var knownNames = new HashSet<string>(player.GetLanguages(), StringComparer.OrdinalIgnoreCase);
            var learning = new List<(Language Language, int Skill)>();
            foreach (var entry in player.GetLanguageSkills())
            {
                var lang = GetLangFromText(entry.Key, false, allowHidden: true);
                if (lang == null || knownNames.Contains(lang.Name) || entry.Value <= 0)
                {
                    continue;
                }

                learning.Add((lang, Math.Max(0, Math.Min(100, entry.Value))));
            }

            return learning;
        }

        private static string FormatLanguageSkill(Language lang, int skill, IServerPlayer? recipient)
        {
            return $"{ChatHelper.LangIdentifierWithDescription(lang, recipient)} ({skill}%)";
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
            if (!TryGetUnknownLanguageComprehension(receivingPlayer, lang, out var comprehensionPercent))
            {
                return;
            }

            message = ScrambleUnknownLanguageMessage(receivingPlayer, lang, message, comprehensionPercent);
        }

        private bool TryGetUnknownLanguageComprehension(IServerPlayer receivingPlayer, Language lang, out int comprehensionPercent)
        {
            comprehensionPercent = 0;
            if (!Config.EnableLanguageSystem || lang == null || receivingPlayer.KnowsLanguage(lang))
            {
                return false;
            }

            comprehensionPercent = receivingPlayer.GetLanguageSkill(lang);
            return comprehensionPercent < 100;
        }

        private string ScrambleUnknownLanguageMessage(IServerPlayer receivingPlayer, Language lang, string message, int comprehensionPercent)
        {
            var sourceMessage = message ?? string.Empty;
            var preservedWords = string.Equals(lang.Name, BabbleLang.Name, StringComparison.OrdinalIgnoreCase)
                ? null
                : GetRecognizableNameWords(receivingPlayer);
            var semanticPlan = SemanticLanguageService.BuildComprehensionPlan(receivingPlayer, lang, sourceMessage);
            System.Func<string, int, bool>? shouldPreserveWord = comprehensionPercent > 0 || semanticPlan?.HasScores == true
                ? (word, wordIndex) => ShouldUnderstandWord(receivingPlayer, lang, sourceMessage, word, wordIndex, comprehensionPercent, semanticPlan)
                : null;

            if (lang.Syllables == null || lang.Syllables.Length == 0)
            {
                return ChatHelper.Italic(ScrambleAsGesturesWithWordPredicate(sourceMessage, preservedWords, shouldPreserveWord));
            }

            var scrambledMessage = LanguageScrambler.ScrambleMessage(sourceMessage, lang, preservedWords, shouldPreserveWord);
            return ChatHelper.Italic(scrambledMessage);
        }

        public bool RegisterSemanticEmbeddingProvider(ITheBasicsSemanticEmbeddingProvider provider)
        {
            return SemanticLanguageService.RegisterProvider(provider);
        }

        public string SemanticEmbeddingProviderStatus => SemanticLanguageService.ProviderStatus;

        public void ObserveMessageForRecipient(MessageContext context)
        {
            if (Config.EnableLanguageSystem && !Config.DisableRPChat)
            {
                SemanticLanguageService.ObserveMessageForRecipient(context);
            }
        }

        public void DisposeLanguageServices()
        {
            SemanticLanguageService.Dispose();
        }

        private static bool ShouldUnderstandWord(
            IServerPlayer receivingPlayer,
            Language lang,
            string message,
            string word,
            int wordIndex,
            int comprehensionPercent,
            SemanticLanguageComprehensionPlan? semanticPlan)
        {
            var effectiveComprehensionPercent = Math.Max(comprehensionPercent, semanticPlan?.GetPercent(wordIndex) ?? 0);
            if (effectiveComprehensionPercent <= 0)
            {
                return false;
            }

            if (effectiveComprehensionPercent >= 100)
            {
                return true;
            }

            return GetStableComprehensionRoll(receivingPlayer.PlayerUID, lang.Name, message, word, wordIndex) < effectiveComprehensionPercent;
        }

        private static int GetStableComprehensionRoll(string playerUid, string languageName, string message, string word, int wordIndex)
        {
            unchecked
            {
                var hash = 17;
                hash = AddStableHash(hash, playerUid);
                hash = AddStableHash(hash, languageName);
                hash = AddStableHash(hash, message);
                hash = AddStableHash(hash, word);
                hash = (hash * 31) + wordIndex;
                return (hash & int.MaxValue) % 100;
            }
        }

        private static int AddStableHash(int hash, string value)
        {
            foreach (var character in value ?? string.Empty)
            {
                hash = (hash * 31) + character;
            }

            return hash;
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
        private static string ScrambleAsGesturesWithWordPredicate(string message, ISet<string>? preservedWords, System.Func<string, int, bool>? shouldPreserveWord)
        {
            var wordRegex = new Regex(@"\w+");
            var wordIndex = 0;
            return wordRegex.Replace(message, match =>
            {
                var word = match.Groups[0].Value;
                var currentWordIndex = wordIndex++;
                if (preservedWords?.Contains(word) == true || shouldPreserveWord?.Invoke(word, currentWordIndex) == true)
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
