using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat
{
    public class LanguageSystem : BaseSubSystem
    {
        private TheStringSlingingScrambler _languageScrambler = new();
        public LanguageSystem(BaseBasicModSystem system, ICoreServerAPI api, ModConfig config) : base(system, api, config)
        {
            API.RegisterSingleStringCommand("addlanguage", "Add a new language to the system", AddLanguage);
            API.RegisterSingleStringCommand("removelanguage", "Add a new language to the system", RemoveLanguage);
            API.RegisterCommand("listlanguage", "Add a new language to the system", null, ListLanguages);
        }

        private void AddLanguage(IServerPlayer player, int groupId, string language)
        {
            player.AddLanguage(Config.Languages.Single(lang => lang.Name == language));
        }

        private void RemoveLanguage(IServerPlayer player, int groupId, string language)
        {
            player.RemoveLanguage(Config.Languages.Single(lang => lang.Name == language));
        }

        private void ListLanguages(IServerPlayer player, int groupId, CmdArgs args)
        {
            var output = string.Join(", ", player.GetLanguages());
            API.SendMessage(player, groupId, output, EnumChatType.CommandSuccess);
        }

        public string ProcessMessage(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer, Language lang, string message)
        {
            if (receivingPlayer.GetLanguages().Contains(lang))
            {
                return message;
            }

            return _languageScrambler.ScrambleMessage(message, lang);
        }
    }
}