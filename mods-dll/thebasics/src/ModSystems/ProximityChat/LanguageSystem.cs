#nullable enable
using System;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
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
        }

        private TextCommandResult AddLanguage(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            var language = (string)args.Parsers[0].GetValue();
            var foundLang = Config.Languages.Any(lang =>
                lang.Name.ToLower() == language.ToLower() || lang.Prefix.ToLower() == language.ToLower());

            if (!foundLang)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Invalid language specified",
                };
            }

            var lang = Config.Languages.Single(lang =>
                lang.Name.ToLower() == language.ToLower() || lang.Prefix.ToLower() == language.ToLower());

            player.AddLanguage(lang);

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Language added!",
            };
        }

        private TextCommandResult RemoveLanguage(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            var language = (string)args.Parsers[0].GetValue();
            var foundLang = player.GetLanguages().Any(lang =>
                lang.Name.ToLower() == language.ToLower() || lang.Prefix.ToLower() == language.ToLower());

            if (!foundLang)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "You don't know that language!",
                };
            }

            var lang = player.GetLanguages().Single(lang =>
                lang.Name.ToLower() == language.ToLower() || lang.Prefix.ToLower() == language.ToLower());

            player.RemoveLanguage(lang);

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Language removed!",
            };
        }

        private TextCommandResult ListLanguages(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "You know: " + string.Join(", ", player.GetLanguages().Select(lang => ChatHelper.LangColor(lang.Name, lang))) +
                                "\n" +
                                "All languages: " + string.Join(", ", Config.Languages.Select(lang => ChatHelper.LangColor(lang.Name, lang))),
            };
        }

        private static Regex _languageSwapRegex = new(@"^\s*:(\w+)\s*$");
        private static Regex _languageTalkRegex = new(@"^\s*:(\w+)\s+(.*)$");

        public bool HandleSwapLanguage(IServerPlayer sendingPlayer, int groupId, string message)
        {
            if (_languageSwapRegex.IsMatch(ChatHelper.GetMessage(message)))
            {
                var match = _languageSwapRegex.Match(ChatHelper.GetMessage(message));
                var languageIdentifier = match.Groups[1].Value;

                if (Config.Languages.All(lang =>
                        lang.Prefix.ToLower() != languageIdentifier.ToLower() &&
                        lang.Name.ToLower() != languageIdentifier.ToLower()))
                {
                    API.SendMessage(sendingPlayer, groupId,
                        "Invalid language specifier.  Valid prefixes include: " + string.Join(", ",
                            Config.Languages.Select(lang => ChatHelper.LangColor($":{lang.Prefix} ({lang.Name})", lang))),
                        EnumChatType.CommandError);
                    return true;
                }

                var lang = Config.Languages.Single(lang =>
                    lang.Prefix.ToLower() == languageIdentifier.ToLower() ||
                    lang.Name.ToLower() == languageIdentifier.ToLower());

                if (sendingPlayer.GetLanguages().All(playerLang => playerLang.Name != lang.Name))
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
            if (_languageTalkRegex.IsMatch(message))
            {
                var match = _languageTalkRegex.Match(message);
                var languageIdentifier = match.Groups[1].Value;

                if (Config.Languages.All(lang => lang.Prefix.ToLower() != languageIdentifier.ToLower()))
                {
                    API.SendMessage(sendingPlayer, groupId,
                        "Invalid language specifier.  Valid prefixes include: " + string.Join(", ",
                            Config.Languages.Select(lang => ":" + lang.Prefix + " (" + lang.Name + ")")),
                        EnumChatType.CommandError);
                    throw new Exception("Invalid language specifier");
                }

                var lang = Config.Languages.Single(lang => lang.Prefix.ToLower() == languageIdentifier.ToLower());

                if (sendingPlayer.GetLanguages().All(playerLang => playerLang.Name != lang.Name))
                {
                    API.SendMessage(sendingPlayer, groupId, "You don't know that language!", EnumChatType.CommandError);
                    throw new Exception("Character doesn't know language");
                }

                message = match.Groups[2].Value;
                return lang;
            }

            return sendingPlayer.GetDefaultLanguage(Config);
        }

        public string ProcessMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, int groupId,
            string message, Language lang)
        {
            if (!Config.EnableLanguageSystem)
            {
                return message;
            }

            if (receivingPlayer.GetLanguages().Any(curLang => curLang.Name == lang.Name))
            {
                return message;
            }

            return LanguageScrambler.ScrambleMessage(message, lang);
        }
    }
}