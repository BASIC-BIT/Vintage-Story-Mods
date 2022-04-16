using System;
using System.Linq;
using System.Text;
using thebasics.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public class TpaSystem : BaseBasicModSystem
    {
        protected override void BasicStartServerSide()
        {
            if (Config.AllowPlayerTpa)
            {
                API.RegisterCommand("tpa", "Request a teleport to another player", "/tpa [name]", HandleTpa);
                API.RegisterCommand("tpaccept", "Accept last teleport request", "/tpaccept", HandleTpAccept);
                API.RegisterCommand("tpdeny", "Deny last teleport request", "/tpdeny", HandleTpDeny);
                API.RegisterCommand("tpallow", "Globally allow or deny all teleport requests", "/tpallow [on|off]", HandleTpAllow);
                API.RegisterCommand("tpallow", "Globally allow or deny all teleport requests", "/tpallow [on|off]", HandleTpAllow);
            }
        }

        private void HandleTpa(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length == 0)
            {
                API.SendMessage(player, groupId, "Usage: /tpa [name]", EnumChatType.CommandError);
                return;
            }
            
            var targetPlayer = API.Server.Players.ToList()
                .Find(findPlayer => String.Equals(findPlayer.PlayerName, args[0], StringComparison.InvariantCultureIgnoreCase));

            if (targetPlayer == player)
            {
                API.SendMessage(player, groupId, "You cannot /tpa to yourself!", EnumChatType.CommandError);
                return;
            }
            
            if (targetPlayer == null)
            {
                API.SendMessage(player, groupId, "Target player not found!", EnumChatType.CommandError);
                return;
            }

            var requestMessage = new StringBuilder();

            requestMessage.Append(player.PlayerName);
            requestMessage.Append(
                " has requested to teleport to you.  Type `/tpaccept` to accept, or `/tpdeny` to deny.");
            API.SendMessage(targetPlayer, GlobalConstants.GeneralChatGroup, requestMessage.ToString(), EnumChatType.Notification);
            
            player.SetLastTpa(targetPlayer);
        }


        private void HandleTpAccept(IServerPlayer player, int groupId, CmdArgs args)
        {
            var targetPlayer = player.GetLastTpa();

            if (targetPlayer == null)
            {
                API.SendMessage(player, groupId, "No recent teleport request to accept!", EnumChatType.CommandError);
                return;
            }
            
            API.SendMessage(targetPlayer, GlobalConstants.GeneralChatGroup, "Your teleport request has been accepted!", EnumChatType.CommandSuccess);
            var pos = player.Entity.Pos;
            
            targetPlayer.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
            player.ClearLastTpa();
        }

        private void HandleTpDeny(IServerPlayer player, int groupId, CmdArgs args)
        {
            var targetPlayer = player.GetLastTpa();
            
            if (targetPlayer == null)
            {
                API.SendMessage(player, groupId, "No recent teleport request to deny!", EnumChatType.CommandError);
                return;
            }
            
            API.SendMessage(targetPlayer, GlobalConstants.GeneralChatGroup, "Your teleport request has been denied!", EnumChatType.CommandError);
            player.ClearLastTpa();
        }
    }
}