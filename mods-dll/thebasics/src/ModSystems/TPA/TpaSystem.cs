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
        private long _timeoutCheckTimer;
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
            itemSlot.MarkDirty();
            player.Entity.MarkShapeModified();
            player.BroadcastPlayerData(true);

            return true;
        }

        private bool TryPutInHandSlot(ItemSlot slot, ItemStack itemStack, IServerPlayer player)
        {
            if (slot != null && slot.Empty)
            {
                slot.Itemstack = itemStack;
                slot.MarkDirty();
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    "Your temporal gear has been returned to your hand.",
                    EnumChatType.Notification);
                return true;
            }
            return false;
        }

        private bool ReturnTemporalGear(IServerPlayer player)
        {
            // Null check - player might have disconnected
            if (player == null)
            {
                return false;
            }

            // Create a temporal gear itemstack to return
            var temporalGearItem = API.World.GetItem(new AssetLocation("game:gear-temporal"));
            if (temporalGearItem == null)
            {
                API.Logger.Error("Could not find temporal gear item to return to player - this is probably a mod bug!");
                return false;
            }

            var temporalGearStack = new ItemStack(temporalGearItem, 1);

            // Priority 1: Try to put it in inventory if there's space
            if (player.InventoryManager.TryGiveItemstack(temporalGearStack, slotNotifyEffect: true))
            {
                return true;
            }

            // Priority 2: Try to put it in hand if hand is free
            var leftHand = player.Entity.LeftHandItemSlot;
            var rightHand = player.Entity.RightHandItemSlot;
            
            if (TryPutInHandSlot(leftHand, temporalGearStack, player) ||
                TryPutInHandSlot(rightHand, temporalGearStack, player))
            {
                return true;
            }

            // Priority 3: Drop it on the ground as last resort
            API.World.SpawnItemEntity(temporalGearStack, player.Entity.Pos.XYZ);
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                "Your temporal gear has been dropped on the ground since your inventory and hands are full.",
                EnumChatType.Notification);
            return true;
        }

        private void CheckForExpiredRequests(float dt)
        {
            if (!Config.TpaUseTimeout) return;

            var timeoutMinutes = Config.TpaTimeoutMinutes;
            var currentTime = DateTime.UtcNow;
            var timeoutTicks = TimeSpan.FromMinutes(timeoutMinutes).Ticks;

            // Get all players and check their requests
            foreach (var player in API.World.AllOnlinePlayers)
            {
                var serverPlayer = player as IServerPlayer;
                if (serverPlayer == null) continue;

                var requests = serverPlayer.GetTpaRequests().ToList();
                var expiredRequests = requests.Where(r =>
                    currentTime.Ticks - r.RequestTimeRealTicks >= timeoutTicks).ToList();

                foreach (var expiredRequest in expiredRequests)
                {
                    var requestingPlayer = API.GetPlayerByUID(expiredRequest.RequestPlayerUID);
                    
                    // Return temporal gear if it was consumed
                    if (expiredRequest.TemporalGearConsumed && requestingPlayer != null)
                    {
                        if (ReturnTemporalGear(requestingPlayer))
                        {
                            requestingPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                                $"Your TPA request to {serverPlayer.PlayerName} has timed out. Your temporal gear has been returned.",
                                EnumChatType.Notification);
                        }
                        else
                        {
                            requestingPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                                $"Your TPA request to {serverPlayer.PlayerName} has timed out. Failed to return your temporal gear. (this is probably a mod bug, report this!)",
                                EnumChatType.CommandError);
                        }
                    }
                    else if (requestingPlayer != null)
                    {
                        requestingPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                            $"Your TPA request to {serverPlayer.PlayerName} has timed out.",
                            EnumChatType.Notification);
                    }

                    // Notify the target player
                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                        $"TPA request from {requestingPlayer?.PlayerName ?? "unknown player"} has timed out.",
                        EnumChatType.Notification);

                    // Remove the expired request
                    serverPlayer.RemoveTpaRequest(expiredRequest);
                }
            }
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

                // Set up timeout checking timer if timeouts are enabled
                if (Config.TpaUseTimeout)
                {
                    // Check for expired requests every 30 seconds
                    _timeoutCheckTimer = API.World.RegisterGameTickListener(CheckForExpiredRequests, 30000);
                }
            }
        }

        public override void Dispose()
        {
            // Unregister the timeout check timer to prevent resource leaks
            if (_timeoutCheckTimer != 0)
            {
                API.World.UnregisterGameTickListener(_timeoutCheckTimer);
                _timeoutCheckTimer = 0;
            }
            
            base.Dispose();
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
            // Validate temporal gear requirement FIRST, but don't consume yet
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
            }

            // Validate cooldown restrictions
            if (!player.CanTpa(API.World.Calendar, Config)) // TODO: Dynamic error message
            {
                var hoursString = Config.TpaCooldownInGameHours.ToString("0.##");

                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Please wait {hoursString} hours between teleport requests.",
                };
            }

            // Validate self-teleport prevention
            if (targetPlayer.PlayerUID == player.PlayerUID)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "You cannot /tpa to yourself!",
                };
            }

            // Validate target player permissions
            if (!targetPlayer.GetTpAllowed())
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Player has teleport requests from other players disabled.",
                };
            }

            // ALL validations passed - NOW consume the temporal gear
            if (Config.TpaRequireTemporalGear)
            {
                if (!RemoveTemporalGear(player))
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = "Failed to consume temporal gear. Please try again.",
                    };
                }
            }

            API.World.SpawnParticles(GetTpaRequestParticles(player));


            var requestMessage = new StringBuilder();

            requestMessage.Append(player.PlayerName);
            if (type == TpaRequestType.Bring)
            {
                requestMessage.Append(" has requested to bring you to them.");
            }
            else if (type == TpaRequestType.Goto)
            {
                requestMessage.Append(" has requested to teleport to you.");
            }

            requestMessage.Append(" Type `/tpaccept` to accept, or `/tpdeny` to deny.");

            targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, requestMessage.ToString(),
                EnumChatType.Notification);

            targetPlayer.AddTpaRequest(new TpaRequest
            {
                Type = type,
                RequestTimeHours = API.World.Calendar.TotalHours,
                RequestTimeRealTicks = DateTime.UtcNow.Ticks,
                RequestPlayerUID = player.PlayerUID,
                TargetPlayerUID = targetPlayer.PlayerUID,
                TemporalGearConsumed = Config.TpaRequireTemporalGear,
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
                API.World.SpawnParticles(GetTpaRequestParticles(targetPlayer));
                API.World.SpawnParticles(GetTpaRequestParticles(player));
            }

            else if (request.Type == TpaRequestType.Bring)
            {
                var pos = targetPlayer.Entity.Pos;
                player.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
                API.World.SpawnParticles(GetTpaRequestParticles(targetPlayer));
                API.World.SpawnParticles(GetTpaRequestParticles(player));
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
            
            // Return temporal gear if it was consumed for this request
            if (request.TemporalGearConsumed)
            {
                if (ReturnTemporalGear(targetPlayer))
                {
                    targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                        "Your teleport request has been denied! Your temporal gear has been returned.",
                        EnumChatType.CommandError);
                }
                else
                {
                    targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                        "Your teleport request has been denied! Failed to return your temporal gear. (this is probably a mod bug, report this!)",
                        EnumChatType.CommandError);
                }
            }
            else
            {
                targetPlayer.SendMessage(GlobalConstants.GeneralChatGroup, "Your teleport request has been denied!",
                    EnumChatType.CommandError);
            }
            
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
            var requests = player.GetTpaRequests().ToList();
            
            // Return temporal gears for any requests that consumed them
            foreach (var request in requests)
            {
                if (request.TemporalGearConsumed)
                {
                    var requestingPlayer = API.GetPlayerByUID(request.RequestPlayerUID);
                    if (requestingPlayer != null)
                    {
                        if (ReturnTemporalGear(requestingPlayer))
                        {
                            requestingPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                                "Your TPA request was cleared. Your temporal gear has been returned.",
                                EnumChatType.Notification);
                        }
                        else
                        {
                            requestingPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                                "Your TPA request was cleared. Failed to return your temporal gear. (this is probably a mod bug, report this!)",
                                EnumChatType.CommandError);
                        }
                    }
                }
            }
            
            player.ClearTpaRequests();
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Teleport requests have been cleared.",
            };
        }

    }
}