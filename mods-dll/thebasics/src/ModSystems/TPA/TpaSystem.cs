using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using thebasics.Extensions;
using thebasics.ModSystems.TPA.Models;
using thebasics.Utilities.Parsers;
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
        
        /// <summary>
        /// Checks if a TPA request has expired based on the mod's timeout configuration
        /// </summary>
        public bool IsRequestExpired(TpaRequest request)
        {
            if (request == null || !Config.TpaUseTimeout)
                return false;
            
            var currentTime = DateTime.UtcNow;
            var timeoutTicks = TimeSpan.FromMinutes(Config.TpaTimeoutMinutes).Ticks;
            
            return currentTime.Ticks - request.RequestTimeRealTicks >= timeoutTicks;
        }
        
        private string GetPlayerDisplayName(IServerPlayer player)
        {
            var nickname = player.GetNickname();
            return nickname != null ? $"{player.PlayerName} ({nickname})" : player.PlayerName;
        }
        
        private void SpawnTeleportParticles(params IServerPlayer[] players)
        {
            foreach (var player in players)
            {
                if (player != null)
                {
                    API.World.SpawnParticles(GetTpaRequestParticles(player));
                }
            }
        }
        
        private void ExecuteTeleport(IServerPlayer from, IServerPlayer to)
        {
            var pos = to.Entity.Pos;
            from.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
        }
        
        private string FormatTimeRemaining(TimeSpan time)
        {
            if (time.TotalSeconds <= 0)
                return "expired";
                
            return time.TotalMinutes >= 1 
                ? $"{(int)time.TotalMinutes}m {time.Seconds}s" 
                : $"{time.Seconds}s";
        }
        
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
                player.SendMessage(GlobalConstants.CurrentChatGroup,
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
            player.SendMessage(GlobalConstants.CurrentChatGroup,
                "Your temporal gear has been dropped on the ground since your inventory and hands are full.",
                EnumChatType.Notification);
            return true;
        }

        private bool ExpireOrCancelTpaRequest(IServerPlayer requestingPlayer, TpaRequest request, TpaExpireReason reason, IServerPlayer targetPlayer = null)
        {
            // Null checks
            if (requestingPlayer == null || request == null) return false;

            // Get target player name for messages (might be offline)
            var targetPlayerName = targetPlayer?.PlayerName ?? 
                                   API.GetPlayerByUID(request.TargetPlayerUID)?.PlayerName ?? 
                                   "unknown player";

            // Handle temporal gear return if it was consumed
            bool gearReturnedSuccessfully = true;
            if (request.TemporalGearConsumed)
            {
                gearReturnedSuccessfully = ReturnTemporalGear(requestingPlayer);
            }

            // Generate appropriate message based on reason
            string message = GenerateTpaExpireMessage(reason, targetPlayerName, request.TemporalGearConsumed, gearReturnedSuccessfully);
            
            // Send message to requesting player
            var messageType = (reason == TpaExpireReason.Denied) ? EnumChatType.CommandError : EnumChatType.Notification;
            requestingPlayer.SendMessage(GlobalConstants.CurrentChatGroup, message, messageType);

            // Notify target player if they're online and it's a timeout
            if (reason == TpaExpireReason.Timeout && targetPlayer != null)
            {
                targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup,
                    $"TPA request from {requestingPlayer.PlayerName} has timed out.",
                    EnumChatType.Notification);
            }

            // Clear the outgoing request
            requestingPlayer.ClearOutgoingTpaRequest();

            return true;
        }

        private string GenerateTpaExpireMessage(TpaExpireReason reason, string targetPlayerName, bool gearConsumed, bool gearReturned)
        {
            string baseMessage = reason switch
            {
                TpaExpireReason.Timeout => $"Your TPA request to {targetPlayerName} has timed out.",
                TpaExpireReason.Denied => "Your teleport request has been denied!",
                TpaExpireReason.Cleared => "Your TPA request was cleared.",
                TpaExpireReason.PlayerRejoin => "Your temporal gear from a timed-out TPA request has been returned!",
                TpaExpireReason.Cancelled => $"Your TPA request to {targetPlayerName} has been cancelled.",
                _ => "Your TPA request has been cancelled."
            };

            // Append gear return status if applicable
            if (gearConsumed && reason != TpaExpireReason.PlayerRejoin)
            {
                if (gearReturned)
                {
                    baseMessage += " Your temporal gear has been returned.";
                }
                else
                {
                    baseMessage += " Failed to return your temporal gear. (this is probably a mod bug, report this!)";
                }
            }

            return baseMessage;
        }

        private void ReturnOwedTemporalGearsAndClearRequest(IServerPlayer player)
        {
            // Check if player has an expired outgoing TPA request
            var request = player.GetOutgoingTpaRequest();
            if (request == null || !IsRequestExpired(request)) return;

            // Use the centralized method to handle the expiration
            ExpireOrCancelTpaRequest(player, request, TpaExpireReason.PlayerRejoin);
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            // Check for any owed temporal gears in world-specific mod data and return them
            ReturnOwedTemporalGearsAndClearRequest(player);
        }

        private void CheckForExpiredRequests(float dt)
        {
            if (!Config.TpaUseTimeout) return;

            // Check all players for expired outgoing requests
            foreach (var player in API.World.AllOnlinePlayers)
            {
                var serverPlayer = player as IServerPlayer;
                if (serverPlayer == null) continue;

                var outgoingRequest = serverPlayer.GetOutgoingTpaRequest();
                if (outgoingRequest == null) continue;

                // Check if the request has expired using the centralized method
                if (IsRequestExpired(outgoingRequest))
                {
                    var targetPlayer = API.GetPlayerByUID(outgoingRequest.TargetPlayerUID);
                    
                    // Use the centralized method to handle the expiration
                    ExpireOrCancelTpaRequest(serverPlayer, outgoingRequest, TpaExpireReason.Timeout, targetPlayer);
                }
            }
        }

        protected override void BasicStartServerSide()
        {
            if (Config.AllowPlayerTpa)
            {
                // Register player join event to return owed temporal gears
                API.Event.PlayerJoin += OnPlayerJoin;
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
                    .WithDescription("Accept a teleport request. Specify player name/nickname if you have multiple requests")
                    .WithArgs(new PlayerByNameOrNicknameArgParser("player", API, false))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpAccept);

                API.ChatCommands.GetOrCreate("tpdeny")
                    .WithDescription("Deny a teleport request. Specify player name/nickname if you have multiple requests")
                    .WithArgs(new PlayerByNameOrNicknameArgParser("player", API, false))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpDeny);
                    
                API.ChatCommands.GetOrCreate("tpalist")
                    .WithDescription("List all incoming teleport requests")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpaList);

                API.ChatCommands.GetOrCreate("tpallow")
                    .WithDescription("Allow or deny all teleport requests from other players")
                    .WithArgs(new BoolArgParser("allow", "on", true))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpAllow);

                API.ChatCommands.GetOrCreate("cleartpa")
                    .WithDescription("Clear all incoming TPA requests")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpaClear);

                API.ChatCommands.GetOrCreate("tpacancel")
                    .WithDescription("Cancel your outgoing teleport request")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpaCancel);

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

            // Unregister player join event handler
            if (API?.Event != null)
            {
                API.Event.PlayerJoin -= OnPlayerJoin;
            }
            
            base.Dispose();
        }

        private TextCommandResult HandleTpa(TextCommandCallingArgs args)
        {
            return HandleTpaCommand(args, TpaRequestType.Goto);
        }

        private TextCommandResult HandleTpaHere(TextCommandCallingArgs args)
        {
            return HandleTpaCommand(args, TpaRequestType.Bring);
        }
        
        private TextCommandResult HandleTpaCommand(TextCommandCallingArgs args, TpaRequestType type)
        {
            var player = args.Caller.Player as IServerPlayer;
            var targetPlayerUid = ((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid;
            var targetPlayer = API.GetPlayerByUID(targetPlayerUid);
            
            if (targetPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot find player.",
                };
            }
            
            return HandleTpaRequest(player, targetPlayer, type);
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

            // Check for existing outgoing request
            var existingRequest = player.GetOutgoingTpaRequest();
            if (existingRequest != null && !IsRequestExpired(existingRequest))
            {
                var existingTargetPlayer = API.GetPlayerByUID(existingRequest.TargetPlayerUID);
                var existingTargetName = existingTargetPlayer != null ? GetPlayerDisplayName(existingTargetPlayer) : "unknown player";
                
                // Calculate time remaining
                var elapsedTime = DateTime.UtcNow - new DateTime(existingRequest.RequestTimeRealTicks);
                var timeRemaining = TimeSpan.FromMinutes(Config.TpaTimeoutMinutes) - elapsedTime;
                var timeString = FormatTimeRemaining(timeRemaining);
                
                // Build informative message
                var requestTypeStr = existingRequest.Type == TpaRequestType.Goto 
                    ? "teleport to" 
                    : "bring";
                    
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"You already have an outgoing request to {requestTypeStr} {existingTargetName} (expires in {timeString}). Use /tpacancel to cancel it.",
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

            SpawnTeleportParticles(player);

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

            // Check if target has multiple requests to suggest using /tpalist
            var existingRequests = targetPlayer.FindAllIncomingTpaRequests(this);
            
            if (existingRequests.Count > 0)  // Will be > 0 after this request is stored
            {
                requestMessage.Append(" Type `/tpalist` to see all requests, `/tpaccept` to accept, or `/tpdeny` to deny.");
            }
            else
            {
                requestMessage.Append(" Type `/tpaccept` to accept, or `/tpdeny` to deny.");
            }

            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, requestMessage.ToString(),
                EnumChatType.Notification);

            var request = new TpaRequest
            {
                Type = type,
                RequestTimeHours = API.World.Calendar.TotalHours,
                RequestTimeRealTicks = DateTime.UtcNow.Ticks,
                RequestPlayerUID = player.PlayerUID,
                TargetPlayerUID = targetPlayer.PlayerUID,
                TemporalGearConsumed = Config.TpaRequireTemporalGear,
            };

            // Store request only on the requester (single source of truth)
            player.SetOutgoingTpaRequest(request);
            player.SetTpaTime(API.World.Calendar);

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Teleport request has been sent to {targetPlayer.PlayerName}.",
            };
        }

        private (TpaRequest request, IServerPlayer requester, TextCommandResult error) ResolveIncomingTpaRequest(
            IServerPlayer targetPlayer, 
            PlayerUidName[] specifiedPlayers, 
            string commandName)
        {
            TpaRequest request = null;
            IServerPlayer requester = null;
            
            // If a player was specified, look for their specific request
            if (specifiedPlayers != null && specifiedPlayers.Length > 0)
            {
                var specifiedPlayerUID = specifiedPlayers[0].Uid;
                request = targetPlayer.FindIncomingTpaRequestFrom(specifiedPlayerUID, this);
                
                if (request == null)
                {
                    var specifiedPlayerName = API.GetPlayerByUID(specifiedPlayerUID)?.PlayerName ?? "that player";
                    return (null, null, new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = $"No teleport request from {specifiedPlayerName} to {commandName}!",
                    });
                }
                
                requester = API.GetPlayerByUID(specifiedPlayerUID);
            }
            else
            {
                // No player specified - check how many requests we have
                var allRequests = targetPlayer.FindAllIncomingTpaRequests(this);
                
                if (allRequests.Count == 0)
                {
                    return (null, null, new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = $"No teleport requests to {commandName}!",
                    });
                }
                else if (allRequests.Count == 1)
                {
                    // Only one request - use it
                    requester = allRequests[0].requester;
                    request = allRequests[0].request;
                }
                else
                {
                    // Multiple requests - need disambiguation
                    var requesters = string.Join(", ", allRequests.Select(r => GetPlayerDisplayName(r.requester)));
                    
                    return (null, null, new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = $"You have multiple teleport requests from: {requesters}. Please specify which one to {commandName}: /tp{commandName} [player]",
                    });
                }
            }
            
            return (request, requester, null);
        }

        private TextCommandResult HandleTpAccept(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            var playerParser = (PlayerByNameOrNicknameArgParser)args.Parsers[0];

            var specifiedPlayers = playerParser.IsMissing ? null : args.Parsers[0].GetValue() as PlayerUidName[];
            
            // Use the centralized resolution method
            var (request, targetPlayer, error) = ResolveIncomingTpaRequest(player, specifiedPlayers, "accept");
            if (error != null) return error;

            // Perform the teleport
            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, "Your teleport request has been accepted!",
                EnumChatType.CommandSuccess);

            if (request.Type == TpaRequestType.Goto)
            {
                ExecuteTeleport(targetPlayer, player);
                SpawnTeleportParticles(targetPlayer, player);
            }
            else if (request.Type == TpaRequestType.Bring)
            {
                ExecuteTeleport(player, targetPlayer);
                SpawnTeleportParticles(player, targetPlayer);
            }

            // Teleport was successful - clear the outgoing request since gear was used properly
            targetPlayer.ClearOutgoingTpaRequest();

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Teleported with {targetPlayer.PlayerName} successfully!",
            };
        }

        private TextCommandResult HandleTpDeny(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            var playerParser = (PlayerByNameOrNicknameArgParser)args.Parsers[0];

            var specifiedPlayers = playerParser.IsMissing ? null : args.Parsers[0].GetValue() as PlayerUidName[];

            // Use the centralized resolution method
            var (request, targetPlayer, error) = ResolveIncomingTpaRequest(player, specifiedPlayers, "deny");
            if (error != null) return error;
            
            // Use the centralized method to handle the denial
            ExpireOrCancelTpaRequest(targetPlayer, request, TpaExpireReason.Denied);
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"You have denied the teleport request from {targetPlayer.PlayerName}.",
            };
        }

        private TextCommandResult HandleTpAllow(TextCommandCallingArgs args)
        {
            var value = (bool)args.Parsers[0].GetValue();
            var player = args.Caller.Player as IServerPlayer;

            player.SetTpAllowed(value);
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Teleport requests are now {(value ? "allowed" : "disallowed")}).",
            };
        }

        private TextCommandResult HandleTpaClear(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            
            // Find ALL incoming TPA requests to this player and clear them
            var allRequests = player.FindAllIncomingTpaRequests(this);
            
            if (allRequests.Count == 0)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = "No teleport requests to clear.",
                };
            }
            
            // Clear all incoming requests
            foreach (var (requester, request) in allRequests)
            {
                // Use the centralized method to handle the clearing
                ExpireOrCancelTpaRequest(requester, request, TpaExpireReason.Cleared);
            }
            
            var message = allRequests.Count == 1 
                ? "Teleport request has been cleared." 
                : $"{allRequests.Count} teleport requests have been cleared.";
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = message,
            };
        }
        
        private TextCommandResult HandleTpaCancel(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            
            // Get the player's outgoing request
            var outgoingRequest = player.GetOutgoingTpaRequest();
            
            // Check if there's an active request to cancel
            if (outgoingRequest == null || IsRequestExpired(outgoingRequest))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = "No outgoing teleport request to cancel.",
                };
            }
            
            // Get target player for notifications
            var targetPlayer = API.GetPlayerByUID(outgoingRequest.TargetPlayerUID);
            
            // Use the centralized method to handle the cancellation
            ExpireOrCancelTpaRequest(player, outgoingRequest, TpaExpireReason.Cancelled, targetPlayer);
            
            // Notify the target player if they're online
            if (targetPlayer != null)
            {
                targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup,
                    $"{GetPlayerDisplayName(player)} has cancelled their teleport request.",
                    EnumChatType.Notification);
            }
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = "Your teleport request has been cancelled.",
            };
        }
        
        private TextCommandResult HandleTpaList(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            
            // Get all incoming TPA requests
            var allRequests = player.FindAllIncomingTpaRequests(this);
            
            if (allRequests.Count == 0)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = "You have no incoming teleport requests.",
                };
            }
            
            var requestList = new StringBuilder();
            requestList.AppendLine($"You have {allRequests.Count} incoming teleport request(s):");
            
            foreach (var (requester, request) in allRequests)
            {
                var requesterDisplay = GetPlayerDisplayName(requester);
                var requestType = request.Type == TpaRequestType.Goto ? "wants to teleport to you" : "wants to bring you to them";
                
                // Calculate time remaining if timeout is enabled
                if (Config.TpaUseTimeout)
                {
                    var elapsedTime = DateTime.UtcNow - new DateTime(request.RequestTimeRealTicks);
                    var timeRemaining = TimeSpan.FromMinutes(Config.TpaTimeoutMinutes) - elapsedTime;
                    var timeString = FormatTimeRemaining(timeRemaining);
                    
                    if (timeRemaining.TotalSeconds > 0)
                    {
                        requestList.AppendLine($"- {requesterDisplay} {requestType} (expires in {timeString})");
                    }
                    else
                    {
                        // This shouldn't happen as expired requests are filtered, but just in case
                        requestList.AppendLine($"- {requesterDisplay} {requestType} ({timeString})");
                    }
                }
                else
                {
                    requestList.AppendLine($"- {requesterDisplay} {requestType}");
                }
            }
            
            requestList.AppendLine("Use /tpaccept [player] or /tpdeny [player] to respond.");
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = requestList.ToString(),
            };
        }

    }
}