using System.Text;
using thebasics.Extensions;
using thebasics.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.TPA
{
    public class TpaSystem : BaseBasicModSystem
    {
        protected override void BasicStartServerSide()
        {
            if (Config.AllowPlayerTpa)
            {
                API.RegisterPlayerTargetCommand("tpa", "Request a teleport to another player", HandleTpa, optional: false);
                API.RegisterPlayerTargetCommand("tpahere", "Request to teleport another player to you", HandleTpaHere, optional: false);
                API.RegisterCommand("tpaccept", "Accept last teleport request", "/tpaccept", HandleTpAccept);
                API.RegisterCommand("tpdeny", "Deny last teleport request", "/tpdeny", HandleTpDeny);
                API.RegisterOnOffCommand("tpallow", "Allow or deny all teleport requests from other players", HandleTpAllow);
            }
        }

        private void HandleTpa(IServerPlayer player, int groupId, IServerPlayer targetPlayer)
        {
            HandleTpaRequest(player, groupId, targetPlayer, TpaRequestType.Goto);
        }

        private void HandleTpaHere(IServerPlayer player, int groupId, IServerPlayer targetPlayer)
        {
            HandleTpaRequest(player, groupId, targetPlayer, TpaRequestType.Bring);
        }

        private void HandleTpaRequest(IServerPlayer player, int groupId, IServerPlayer targetPlayer, TpaRequestType type)
        {
            if (!player.CanTpa(API.World.Calendar, Config))
            {
                var hoursString = Config.TpaCooldownInGameHours.ToString("0.##");
                player.SendMessage(groupId, "Please wait " + hoursString + " hours between teleport requests.", EnumChatType.CommandError);
                return;
            }
            
            if (targetPlayer.PlayerUID == player.PlayerUID)
            {
                player.SendMessage(groupId, "You cannot /tpa to yourself!", EnumChatType.CommandError);
                return;
            }

            if (!targetPlayer.GetTpAllowed())
            {
                player.SendMessage(groupId, "Player has teleport requests from other players disabled.", EnumChatType.CommandError);
                return;
            }

            var requestMessage = new StringBuilder();

            requestMessage.Append(player.PlayerName);
            if (type == TpaRequestType.Bring)
            {
                requestMessage.Append(" has requested to teleport to you.");
            } else if (type == TpaRequestType.Goto)
            {
                requestMessage.Append(" has requested to bring you to them.");
            }

            requestMessage.Append(" Type `/tpaccept` to accept, or `/tpdeny` to deny.");
            
            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, requestMessage.ToString(), EnumChatType.Notification);
            
            targetPlayer.AddTpaRequest(new TpaRequest
            {
                Type = type,
                RequestTimeHours = API.World.Calendar.TotalHours,
                RequestPlayerUID = player.PlayerUID,
                TargetPlayerUID = targetPlayer.PlayerUID,
            });
            player.SetTpaTime(API.World.Calendar);
            player.SendMessage(groupId, "Teleport request has been sent to " + targetPlayer.PlayerName + ".", EnumChatType.CommandSuccess);
        }

        private void HandleTpAccept(IServerPlayer player, int groupId, CmdArgs args)
        {
            var requests = player.GetTpaRequests();

            if (requests.Count == 0)
            {
                player.SendMessage(groupId, "No recent teleport request to accept!", EnumChatType.CommandError);
                return;
            }

            var request = requests[0];

            var targetPlayer = API.GetPlayerByUID(request.RequestPlayerUID);
            
            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Your teleport request has been accepted!", EnumChatType.CommandSuccess);

            if (request.Type == TpaRequestType.Goto)
            {
                var pos = player.Entity.Pos;
                targetPlayer.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
            } else if (request.Type == TpaRequestType.Bring)
            {
                var pos = targetPlayer.Entity.Pos;
                player.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
            }
            player.RemoveTpaRequest(request);
        }

        private void HandleTpDeny(IServerPlayer player, int groupId, CmdArgs args)
        {
            var requests = player.GetTpaRequests();

            if (requests.Count == 0)
            {
                player.SendMessage(groupId, "No recent teleport request to deny!", EnumChatType.CommandError);
                return;
            }
            var request = requests[0];

            var targetPlayer = API.GetPlayerByUID(request.RequestPlayerUID);
            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Your teleport request has been denied!", EnumChatType.CommandError);
            player.RemoveTpaRequest(request);
        }


        private void HandleTpAllow(IServerPlayer player, int groupId, bool value)
        {
            player.SetTpAllowed(value);

            player.SendMessage(groupId, "Teleport requests are now " + (value ? "allowed" : "disallowed") + ".",
                EnumChatType.CommandSuccess);
        }
    }
}