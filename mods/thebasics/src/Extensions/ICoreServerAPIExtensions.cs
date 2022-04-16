using System;
using System.Linq;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.Extensions
{
    public static class ICoreServerAPIExtensions
    {
        public static bool RegisterCommand(this ICoreServerAPI api,
            string[] commands,
            string descriptionMsg,
            string syntaxMsg,
            ServerChatCommandDelegate handler,
            string requiredPrivilege = null)
        {
            var results = commands.Select(command =>
                api.RegisterCommand(command, descriptionMsg, syntaxMsg, handler, requiredPrivilege));

            return results.All(result => result);
        }
        public static bool RegisterOnOffCommand(this ICoreServerAPI api,
            string command,
            string descriptionMsg,
            ChatHelper.OnOffChatCommandDelegate handler,
            string requiredPrivilege = null)
        {
            ServerChatCommandDelegate del = (player, groupId, args) =>
            {
                if (args.Length != 1)
                {
                    player.SendMessage(groupId, "Usage: /" + command + " [on|off]", EnumChatType.CommandError);
                    return;
                }

                var value = args[0].ToLower();

                if (value != "on" && value != "off")
                {
                    player.SendMessage(groupId, "Usage: /" + command + " [on|off]", EnumChatType.CommandError);
                    return;
                }

                var boolValue = value == "on";

                handler(player, groupId, boolValue);
            };

            var syntaxMsg = "/" + command + " [on|off]";
            
            return api.RegisterCommand(command, descriptionMsg, syntaxMsg, del, requiredPrivilege);
        }

        public static IServerPlayer GetPlayerByName(this ICoreServerAPI api, string name)
        {
            return api.Server.Players.ToList()
                .Find(findPlayer => String.Equals(findPlayer.PlayerName, name, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}