using System.Linq;
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
    }
}