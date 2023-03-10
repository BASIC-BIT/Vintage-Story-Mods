using System;
using System.Linq;
using System.Text;
using thebasics.Extensions;
using thebasics.ModSystems.TPA.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.TPA
{
    public class TpaSystem : BaseBasicModSystem
    {
        private SimpleParticleProperties GetTpaRequestParticles(IServerPlayer player)
        {
            var rand = new NormalRandom();

            var pos = player.Entity.LocalEyePos;
            return new SimpleParticleProperties()
            {
                LifeLength = 0.8f,
                Color = ColorUtil.ToRgba(180, 200, 220, 250),
                Bounciness = 1,
                GravityEffect = 0,
                ParticleModel = EnumParticleModel.Cube,
                MinPos = pos,
                SelfPropelled = true,
                MinVelocity = new Vec3f((float) (rand.NextDouble() - 0.5), (float) (rand.NextDouble() - 0.5),
                    (float) (rand.NextDouble() - 0.5)),
                ShouldDieInAir = false,
                ShouldSwimOnLiquid = false,
                ShouldDieInLiquid = false,
                WithTerrainCollision = true,
                MinSize = 0.6f,
                MaxSize = 0.6f,
                WindAffected = false,
                MinQuantity = 10,
                DieOnRainHeightmap = false,
            };
        }

        private bool IsPlayerHoldingTemporalGear(IServerPlayer player)
        {
            return ItemSlotContainsTemporalGear(player.Entity.LeftHandItemSlot) ||
                   ItemSlotContainsTemporalGear(player.Entity.RightHandItemSlot);
        }

        private bool ItemSlotContainsTemporalGear(ItemSlot itemSlot)
        {
            return itemSlot != null &&
                   itemSlot.Itemstack != null &&
                   itemSlot.Itemstack.Item is ItemTemporalGear;
        }

        private bool RemoveTemporalGear(IServerPlayer player)
        {
            var leftHand = ItemSlotContainsTemporalGear(player.Entity.LeftHandItemSlot);
            var rightHand = ItemSlotContainsTemporalGear(player.Entity.RightHandItemSlot);

            if (!leftHand && !rightHand)
            {
                return false;
            }

            var itemSlot = leftHand ? player.Entity.LeftHandItemSlot : player.Entity.RightHandItemSlot;

            itemSlot.TakeOut(1);

            return true;
        }

        protected override void BasicStartServerSide()
        {
            if (Config.AllowPlayerTpa)
            {
                if (Config.AllowTpaPrivilegeByDefault)
                {
                    API.RegisterPlayerTargetCommand("tpa", "Request a teleport to another player", HandleTpa,
                        optional: false);
                    API.RegisterPlayerTargetCommand("tpahere", "Request to teleport another player to you", HandleTpaHere,
                        optional: false);
                }
                else
                {
                    API.RegisterPlayerTargetCommand("tpa", "Request a teleport to another player", HandleTpa,
                        optional: false, requiredPrivilege: "tpa");
                    API.RegisterPlayerTargetCommand("tpahere", "Request to teleport another player to you", HandleTpaHere,
                        optional: false, requiredPrivilege: "tpa");
                }
                API.RegisterCommand("tpaccept", "Accept last teleport request", "/tpaccept", HandleTpAccept);
                API.RegisterCommand("tpdeny", "Deny last teleport request", "/tpdeny", HandleTpDeny);
                API.RegisterOnOffCommand("tpallow", "Allow or deny all teleport requests from other players",
                    HandleTpAllow);
                API.RegisterOnOffCommand("cleartpa", "Clear all outstanding TPA requests", HandleTpaClear);
                
                API.Permissions.RegisterPrivilege("tpa", "Ability to use the /tpa and /tpahere commands");
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

        private void HandleTpaRequest(IServerPlayer player, int groupId, IServerPlayer targetPlayer,
            TpaRequestType type)
        {
            if (Config.TpaRequireTemporalGear && !IsPlayerHoldingTemporalGear(player))
            {
                player.SendMessage(groupId, "You must hold a temporal gear to initiate a teleport request. (This will consume it)",
                    EnumChatType.CommandError);
                return;
            }

            if (!player.CanTpa(API.World.Calendar, Config)) // TODO: Dynamic error message
            {
                var hoursString = Config.TpaCooldownInGameHours.ToString("0.##");
                player.SendMessage(groupId, "Please wait " + hoursString + " hours between teleport requests.",
                    EnumChatType.CommandError);
                return;
            }

            if (targetPlayer.PlayerUID == player.PlayerUID)
            {
                player.SendMessage(groupId, "You cannot /tpa to yourself!", EnumChatType.CommandError);
                return;
            }

            if (!targetPlayer.GetTpAllowed())
            {
                player.SendMessage(groupId, "Player has teleport requests from other players disabled.",
                    EnumChatType.CommandError);
                return;
            }

            API.World.SpawnParticles(GetTpaRequestParticles(player));

            
            var requestMessage = new StringBuilder();

            requestMessage.Append(player.PlayerName);
            if (type == TpaRequestType.Bring)
            {
                requestMessage.Append(" has requested to teleport to you.");
            }
            else if (type == TpaRequestType.Goto)
            {
                requestMessage.Append(" has requested to bring you to them.");
            }

            requestMessage.Append(" Type `/tpaccept` to accept, or `/tpdeny` to deny.");

            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, requestMessage.ToString(),
                EnumChatType.Notification);

            targetPlayer.AddTpaRequest(new TpaRequest
            {
                Type = type,
                RequestTimeHours = API.World.Calendar.TotalHours,
                RequestPlayerUID = player.PlayerUID,
                TargetPlayerUID = targetPlayer.PlayerUID,
            });
            player.SetTpaTime(API.World.Calendar);
            player.SendMessage(groupId, "Teleport request has been sent to " + targetPlayer.PlayerName + ".",
                EnumChatType.CommandSuccess);
        }

        private void HandleTpAccept(IServerPlayer player, int groupId, CmdArgs args)
        {
            var requests = player.GetTpaRequests().ToList();

            if (requests.Count == 0)
            {
                player.SendMessage(groupId, "No recent teleport request to accept!", EnumChatType.CommandError);
                return;
            }

            var request = requests[0];

            var targetPlayer = API.GetPlayerByUID(request.RequestPlayerUID);

            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Your teleport request has been accepted!",
                EnumChatType.CommandSuccess);

            if (request.Type == TpaRequestType.Goto)
            {
                var pos = player.Entity.Pos;
                targetPlayer.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
                API.World.SpawnParticles(GetTpaRequestParticles(player), player);
                API.World.SpawnParticles(GetTpaRequestParticles(player), targetPlayer);
            }

            else if (request.Type == TpaRequestType.Bring)
            {
                var pos = targetPlayer.Entity.Pos;
                player.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
                API.World.SpawnParticles(GetTpaRequestParticles(targetPlayer), player);
                API.World.SpawnParticles(GetTpaRequestParticles(targetPlayer), targetPlayer);
            }

            player.RemoveTpaRequest(request);
        }

        private void HandleTpDeny(IServerPlayer player, int groupId, CmdArgs args)
        {
            var requests = player.GetTpaRequests().ToList();

            if (requests.Count == 0)
            {
                player.SendMessage(groupId, "No recent teleport request to deny!", EnumChatType.CommandError);
                return;
            }

            var request = requests[0];

            var targetPlayer = API.GetPlayerByUID(request.RequestPlayerUID);
            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Your teleport request has been denied!",
                EnumChatType.CommandError);
            player.RemoveTpaRequest(request);
        }


        private void HandleTpAllow(IServerPlayer player, int groupId, bool value)
        {
            player.SetTpAllowed(value);

            player.SendMessage(groupId, "Teleport requests are now " + (value ? "allowed" : "disallowed") + ".",
                EnumChatType.CommandSuccess);
        }

        private void HandleTpaClear(IServerPlayer player, int groupId, bool value)
        {
            player.ClearTpaRequests();
            player.SendMessage(groupId, "Teleport requests have been cleared.", EnumChatType.CommandSuccess);
        }
    }
}