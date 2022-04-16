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
                API.RegisterOnOffCommand("tpallow", "Allow or deny all teleport requests from other players", HandleTpAllow);
            }
        }

        private void HandleTpa(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (!player.CanTpa(API.World.Calendar, Config))
            {
                var hoursString = Config.TpaCooldownInGameHours.ToString("0.##");
                player.SendMessage(groupId, "Please wait " + hoursString + " hours between teleport requests.", EnumChatType.CommandError);
                return;
            }
            
            if (args.Length == 0)
            {
                player.SendMessage(groupId, "Usage: /tpa [name]", EnumChatType.CommandError);
                return;
            }

            var targetPlayer = API.GetPlayerByName(args[0]);

            if (targetPlayer == player)
            {
                player.SendMessage(groupId, "You cannot /tpa to yourself!", EnumChatType.CommandError);
                return;
            }
            
            if (targetPlayer == null)
            {
                player.SendMessage(groupId, "Target player not found!", EnumChatType.CommandError);
                return;
            }

            if (!targetPlayer.GetTpAllowed())
            {
                player.SendMessage(groupId, "Player has teleport requests from other players disabled.", EnumChatType.CommandError);
                return;
            }

            var requestMessage = new StringBuilder();

            requestMessage.Append(player.PlayerName);
            requestMessage.Append(
                " has requested to teleport to you.  Type `/tpaccept` to accept, or `/tpdeny` to deny.");
            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, requestMessage.ToString(), EnumChatType.Notification);
            
            targetPlayer.SetLastTpa(player);
            player.SetTpaTime(API.World.Calendar);
            player.SendMessage(groupId, "Teleport request to " + targetPlayer.PlayerName + " has been sent.", EnumChatType.CommandSuccess);
        }


        private void HandleTpAccept(IServerPlayer player, int groupId, CmdArgs args)
        {
            var targetPlayer = player.GetLastTpa();

            if (targetPlayer == null)
            {
                player.SendMessage(groupId, "No recent teleport request to accept!", EnumChatType.CommandError);
                return;
            }
            
            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Your teleport request has been accepted!", EnumChatType.CommandSuccess);
            var pos = player.Entity.Pos;
            
            targetPlayer.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
            player.ClearLastTpa();
        }

        private void HandleTpDeny(IServerPlayer player, int groupId, CmdArgs args)
        {
            var targetPlayer = player.GetLastTpa();
            
            if (targetPlayer == null)
            {
                player.SendMessage(groupId, "No recent teleport request to deny!", EnumChatType.CommandError);
                return;
            }
            
            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Your teleport request has been denied!", EnumChatType.CommandError);
            player.ClearLastTpa();
        }


        private void HandleTpAllow(IServerPlayer player, int groupId, bool value)
        {
            player.SetTpAllowed(value);

            player.SendMessage(groupId, "Teleport requests are now " + (value ? "allowed" : "disallowed") + ".",
                EnumChatType.CommandSuccess);
        }
    }
}