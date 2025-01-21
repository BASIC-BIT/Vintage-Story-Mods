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
                MinVelocity = new Vec3f((float)(rand.NextDouble() - 0.5), (float)(rand.NextDouble() - 0.5),
                    (float)(rand.NextDouble() - 0.5)),
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
                API.Permissions.RegisterPrivilege("tpa", "Ability to use the /tpa and /tpahere commands");

                API.ChatCommands.GetOrCreate("tpa")
                    .WithDescription("Request a teleport to another player")
                    .RequiresPrivilege(Config.AllowTpaPrivilegeByDefault ? Privilege.chat : "tpa")
                    .WithArgs(new PlayersArgParser("player", API, true))
                    .RequiresPlayer()
                    .HandleWith(HandleTpa);

                API.ChatCommands.GetOrCreate("tpahere")
                    .WithDescription("Request to teleport another player to you")
                    .RequiresPrivilege(Config.AllowTpaPrivilegeByDefault ? Privilege.chat : "tpa")
                    .WithArgs(new PlayersArgParser("player", API, true))
                    .RequiresPlayer()
                    .HandleWith(HandleTpaHere);

                API.ChatCommands.GetOrCreate("tpaccept")
                    .WithDescription("Accept last teleport request")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpAccept);

                API.ChatCommands.GetOrCreate("tpdeny")
                    .WithDescription("Deny last teleport request")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpDeny);

                API.ChatCommands.GetOrCreate("tpallow")
                    .WithDescription("Allow or deny all teleport requests from other players")
                    .WithArgs(new BoolArgParser("allow", "on", true))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpAllow);

                API.ChatCommands.GetOrCreate("cleartpa")
                    .WithDescription("Clear all outstanding TPA requests")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpaClear);
            }
        }

        private TextCommandResult HandleTpa(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var attemptTarget = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            if (attemptTarget == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot find player.",
                };
            }
            return HandleTpaRequest(player, attemptTarget, TpaRequestType.Goto);
        }

        private TextCommandResult HandleTpaHere(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);

            var attemptTarget = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);
            if (attemptTarget == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot find player.",
                };
            }
            return HandleTpaRequest(player, attemptTarget, TpaRequestType.Bring);
        }

        private TextCommandResult HandleTpaRequest(IServerPlayer player, IServerPlayer targetPlayer,
            TpaRequestType type)
        {
            if (Config.TpaRequireTemporalGear)
            {
                if(!IsPlayerHoldingTemporalGear(player))
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage =
                            "You must hold a temporal gear to initiate a teleport request. (This will consume it)",
                    };
                }
                else
                {
                    // TODO: Remove temporal gear from inventory (temporarily if request times out or is declined?)
                }
                
            }

            if (!player.CanTpa(API.World.Calendar, Config)) // TODO: Dynamic error message
            {
                var hoursString = Config.TpaCooldownInGameHours.ToString("0.##");

                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Please wait {hoursString} hours between teleport requests.",
                };
            }

            if (targetPlayer.PlayerUID == player.PlayerUID)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "You cannot /tpa to yourself!",
                };
            }

            if (!targetPlayer.GetTpAllowed())
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Player has teleport requests from other players disabled.",
                };
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

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Teleport request has been sent to {targetPlayer.PlayerName}.",
            };
        }

        private TextCommandResult HandleTpAccept(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var requests = player.GetTpaRequests().ToList();

            if (requests.Count == 0)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "No recent teleport request to accept!",
                };
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

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Teleport successful!",
            };
            
        }

        private TextCommandResult HandleTpDeny(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var requests = player.GetTpaRequests().ToList();

            if (requests.Count == 0)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "No recent teleport request to deny!",
                };
            }

            var request = requests[0];

            var targetPlayer = API.GetPlayerByUID(request.RequestPlayerUID);
            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Your teleport request has been denied!",
                EnumChatType.CommandError);
            player.RemoveTpaRequest(request);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = $"You have denied the teleport request from {targetPlayer.PlayerName}.",
            };
        }

        private TextCommandResult HandleTpAllow(TextCommandCallingArgs args)
        {
            var value = (bool)args.Parsers[0].GetValue();
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);

            player.SetTpAllowed(value);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Teleport requests are now {(value ? "allowed" : "disallowed")}).",
            };
        }

        private TextCommandResult HandleTpaClear(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            player.ClearTpaRequests();
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Teleport requests have been cleared.",
            };
        }
    }
}