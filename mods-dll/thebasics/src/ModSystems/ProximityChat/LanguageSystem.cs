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
            API.RegisterSingleStringCommand("addlang", "Add a new language to the system", AddLanguage);
            API.RegisterSingleStringCommand("removelang", "Add a new language to the system", RemoveLanguage);
            API.RegisterCommand("listlang", "Add a new language to the system", null, ListLanguages);
        }

        private void AddLanguage(IServerPlayer player, int groupId, string language)
        {
            var foundLang = Config.Languages.Any(lang => lang.Name.ToLower() == language.ToLower() || lang.Prefix.ToLower() == language.ToLower());

            if (!foundLang)
            {
                API.SendMessage(player, groupId, "Invalid language specified", EnumChatType.CommandError);
                return;
            }

            var lang = Config.Languages.Single(lang => lang.Name.ToLower() == language.ToLower() || lang.Prefix.ToLower() == language.ToLower());

            player.AddLanguage(lang);
            
            API.SendMessage(player, groupId, "Language added!", EnumChatType.CommandSuccess);
        }

        private void RemoveLanguage(IServerPlayer player, int groupId, string language)
        {
            var foundLang = player.GetLanguages().Any(lang => lang.Name.ToLower() == language.ToLower() || lang.Prefix.ToLower() == language.ToLower());

            if (!foundLang)
            {
                API.SendMessage(player, groupId, "You don't know that language!", EnumChatType.CommandError);
                return;
            }

            var lang = player.GetLanguages().Single(lang => lang.Name.ToLower() == language.ToLower() || lang.Prefix.ToLower() == language.ToLower());

            player.RemoveLanguage(lang);
            
            API.SendMessage(player, groupId, "Language removed!", EnumChatType.CommandSuccess);
        }

        private void ListLanguages(IServerPlayer player, int groupId, CmdArgs args)
        {
            var output = "You know: " + string.Join(", ", player.GetLanguages().Select(lang => lang.Name)) + "\n" +
                         "All languages: " + string.Join(", ", Config.Languages.Select(lang => lang.Name));
            API.SendMessage(player, groupId, output, EnumChatType.CommandSuccess);
        }

        private static Regex _languageSwapRegex = new(@"^\s*:(\w+)\s*$");
        private static Regex _languageTalkRegex = new(@"^\s*:(\w+)\s+(.*)$");
        public bool HandleSwapLanguage(IServerPlayer sendingPlayer, int groupId, string message)
        {
            if (_languageSwapRegex.IsMatch(ChatHelper.GetMessage(message)))
            {
                var match = _languageSwapRegex.Match(ChatHelper.GetMessage(message));
                var languageIdentifier = match.Groups[1].Value;

                if (Config.Languages.All(lang => lang.Prefix.ToLower() != languageIdentifier.ToLower() && lang.Name.ToLower() != languageIdentifier.ToLower()))
                {
                    API.SendMessage(sendingPlayer, groupId, "Invalid language specifier.  Valid prefixes include: " + string.Join(", ", Config.Languages.Select(lang => ":" + lang.Prefix + " (" + lang.Name + ")")), EnumChatType.CommandError);
                    return true;
                }

                var lang = Config.Languages.Single(lang => lang.Prefix.ToLower() == languageIdentifier.ToLower() || lang.Name.ToLower() == languageIdentifier.ToLower());

                if (sendingPlayer.GetLanguages().All(playerLang => playerLang.Name != lang.Name))
                {
                    API.SendMessage(sendingPlayer, groupId, "You don't know that language!", EnumChatType.CommandError);
                    return true;
                }

                sendingPlayer.SetDefaultLanguage(lang);
                API.SendMessage(sendingPlayer, groupId, "You are now speaking " + lang.Name + ".", EnumChatType.CommandSuccess);

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
                    API.SendMessage(sendingPlayer, groupId, "Invalid language specifier.  Valid prefixes include: " + string.Join(", ", Config.Languages.Select(lang => ":" + lang.Prefix + " (" + lang.Name + ")")), EnumChatType.CommandError);
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

            return TheStringSlingingScrambler.ScrambleMessage(message, lang);
        }
    }
}