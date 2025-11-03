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

        private static string L(string key, params object[] args) => Lang.Get($"thebasics:{key}", args);
        
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
            return nickname != null ? L("tpa.player.displayName", player.PlayerName, nickname) : player.PlayerName;
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
            {
                return L("tpa.time.expired");
            }

            if (time.TotalMinutes >= 1)
            {
                return L("tpa.time.minutesSeconds", (int)time.TotalMinutes, time.Seconds);
            }

            return L("tpa.time.seconds", time.Seconds);
        }

        private string DescribeOutgoingAction(TpaRequestType type)
        {
            return type == TpaRequestType.Goto
                ? L("tpa.request.action.outgoing.goto")
                : L("tpa.request.action.outgoing.bring");
        }

        private string DescribeIncomingSummary(TpaRequestType type)
        {
            return type == TpaRequestType.Goto
                ? L("tpa.request.summary.goto")
                : L("tpa.request.summary.bring");
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
                    L("tpa.gear.returnedHand"),
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
                L("tpa.gear.dropped"),
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
                                   L("tpa.player.unknown");

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
                    L("tpa.notification.targetTimeout", GetPlayerDisplayName(requestingPlayer)),
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
                TpaExpireReason.Timeout => L("tpa.expire.timeout", targetPlayerName),
                TpaExpireReason.Denied => L("tpa.expire.denied"),
                TpaExpireReason.Cleared => L("tpa.expire.cleared"),
                TpaExpireReason.PlayerRejoin => L("tpa.expire.rejoin"),
                TpaExpireReason.Cancelled => L("tpa.expire.cancelled", targetPlayerName),
                _ => L("tpa.expire.generic")
            };

            // Append gear return status if applicable
            if (gearConsumed && reason != TpaExpireReason.PlayerRejoin)
            {
                if (gearReturned)
                {
                    baseMessage += " " + L("tpa.expire.gearReturned");
                }
                else
                {
                    baseMessage += " " + L("tpa.expire.gearFailed");
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
                API.Permissions.RegisterPrivilege("tpa", L("tpa.privilege.description"));

                API.ChatCommands.GetOrCreate("tpa")
                    .WithDescription(L("tpa.command.tpa.description"))
                    .RequiresPrivilege(Config.AllowTpaPrivilegeByDefault ? Privilege.chat : "tpa")
                    .WithArgs(new PlayersArgParser(L("command.arg.player"), API, true))
                    .RequiresPlayer()
                    .HandleWith(HandleTpa);

                API.ChatCommands.GetOrCreate("tpahere")
                    .WithDescription(L("tpa.command.tpahere.description"))
                    .RequiresPrivilege(Config.AllowTpaPrivilegeByDefault ? Privilege.chat : "tpa")
                    .WithArgs(new PlayersArgParser(L("command.arg.player"), API, true))
                    .RequiresPlayer()
                    .HandleWith(HandleTpaHere);

                API.ChatCommands.GetOrCreate("tpaccept")
                    .WithDescription(L("tpa.command.tpaccept.description"))
                    .WithArgs(new PlayerByNameOrNicknameArgParser(L("command.arg.player"), API, false))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpAccept);

                API.ChatCommands.GetOrCreate("tpdeny")
                    .WithDescription(L("tpa.command.tpdeny.description"))
                    .WithArgs(new PlayerByNameOrNicknameArgParser(L("command.arg.player"), API, false))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpDeny);
                    
                API.ChatCommands.GetOrCreate("tpalist")
                    .WithDescription(L("tpa.command.tpalist.description"))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpaList);

                API.ChatCommands.GetOrCreate("tpallow")
                    .WithDescription(L("tpa.command.tpallow.description"))
                    .WithArgs(new BoolArgParser(L("command.arg.allow"), L("command.arg.on"), true))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpAllow);

                API.ChatCommands.GetOrCreate("cleartpa")
                    .WithDescription(L("tpa.command.cleartpa.description"))
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(HandleTpaClear);

                API.ChatCommands.GetOrCreate("tpacancel")
                    .WithDescription(L("tpa.command.tpacancel.description"))
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
                    StatusMessage = L("common.error.playerNotFound"),
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
                        StatusMessage = L("tpa.error.requiresGear"),
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
                    StatusMessage = L("tpa.error.cooldown", hoursString),
                };
            }

            // Validate self-teleport prevention
            if (targetPlayer.PlayerUID == player.PlayerUID)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = L("tpa.error.self"),
                };
            }

            // Validate target player permissions
            if (!targetPlayer.GetTpAllowed())
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = L("tpa.error.targetDisabled"),
                };
            }

            // Check for existing outgoing request
            var existingRequest = player.GetOutgoingTpaRequest();
            if (existingRequest != null && !IsRequestExpired(existingRequest))
            {
                var existingTargetPlayer = API.GetPlayerByUID(existingRequest.TargetPlayerUID);
                var existingTargetName = existingTargetPlayer != null ? GetPlayerDisplayName(existingTargetPlayer) : L("tpa.player.unknown");
                
                // Calculate time remaining
                var elapsedTime = DateTime.UtcNow - new DateTime(existingRequest.RequestTimeRealTicks);
                var timeRemaining = TimeSpan.FromMinutes(Config.TpaTimeoutMinutes) - elapsedTime;
                var timeString = FormatTimeRemaining(timeRemaining);
                
                // Build informative message
                var requestTypeStr = DescribeOutgoingAction(existingRequest.Type);
                    
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = L("tpa.error.existingOutgoing", requestTypeStr, existingTargetName, timeString),
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
                        StatusMessage = L("tpa.error.consumeGear"),
                    };
                }
            }

            SpawnTeleportParticles(player);

            var requestMessage = type == TpaRequestType.Bring
                ? L("tpa.request.message.bring", GetPlayerDisplayName(player))
                : L("tpa.request.message.goto", GetPlayerDisplayName(player));

            // Check if target has multiple requests to suggest using /tpalist
            var existingRequests = targetPlayer.FindAllIncomingTpaRequests(this);
            
            if (existingRequests.Count > 0)  // Will be > 0 after this request is stored
            {
                requestMessage += " " + L("tpa.request.instructions.withlist");
            }
            else
            {
                requestMessage += " " + L("tpa.request.instructions.simple");
            }

            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, requestMessage,
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
                StatusMessage = L("tpa.success.requestSent", GetPlayerDisplayName(targetPlayer)),
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
                    var specifiedPlayerName = API.GetPlayerByUID(specifiedPlayerUID)?.PlayerName ?? L("tpa.player.thatPlayer");
                    return (null, null, new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = L("tpa.error.noRequestFromPlayer", specifiedPlayerName, commandName),
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
                        StatusMessage = L("tpa.error.noRequestsForCommand", commandName),
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
                        StatusMessage = L("tpa.error.multipleRequests", requesters, commandName, commandName),
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
            targetPlayer.SendMessage(GlobalConstants.CurrentChatGroup, L("tpa.notification.accepted"),
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
                StatusMessage = L("tpa.success.teleported", GetPlayerDisplayName(targetPlayer)),
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
                StatusMessage = L("tpa.success.denied", GetPlayerDisplayName(targetPlayer)),
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
                StatusMessage = value ? L("tpa.success.tpallow.enabled") : L("tpa.success.tpallow.disabled"),
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
                    StatusMessage = L("tpa.success.clear.none"),
                };
            }
            
            // Clear all incoming requests
            foreach (var (requester, request) in allRequests)
            {
                // Use the centralized method to handle the clearing
                ExpireOrCancelTpaRequest(requester, request, TpaExpireReason.Cleared);
            }
            
            var message = allRequests.Count == 1 
                ? L("tpa.success.clear.single") 
                : L("tpa.success.clear.multiple", allRequests.Count);
            
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
                    StatusMessage = L("tpa.success.cancel.none"),
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
                    L("tpa.notification.cancelled", GetPlayerDisplayName(player)),
                    EnumChatType.Notification);
            }
            
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = L("tpa.success.cancelled"),
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
                    StatusMessage = L("tpa.list.none"),
                };
            }
            
            var requestList = new StringBuilder();
            requestList.AppendLine(L("tpa.list.header", allRequests.Count));

            foreach (var (requester, request) in allRequests)
            {
                var requesterDisplay = GetPlayerDisplayName(requester);
                var requestType = DescribeIncomingSummary(request.Type);
                
                // Calculate time remaining if timeout is enabled
                if (Config.TpaUseTimeout)
                {
                    var elapsedTime = DateTime.UtcNow - new DateTime(request.RequestTimeRealTicks);
                    var timeRemaining = TimeSpan.FromMinutes(Config.TpaTimeoutMinutes) - elapsedTime;
                    var timeString = FormatTimeRemaining(timeRemaining);
                    
                    if (timeRemaining.TotalSeconds > 0)
                    {
                        requestList.AppendLine(L("tpa.list.entry.withExpiry", requesterDisplay, requestType, timeString));
                    }
                    else
                    {
                        // This shouldn't happen as expired requests are filtered, but just in case
                        requestList.AppendLine(L("tpa.list.entry.expired", requesterDisplay, requestType, timeString));
                    }
                }
                else
                {
                    requestList.AppendLine(L("tpa.list.entry", requesterDisplay, requestType));
                }
            }
            
            requestList.AppendLine(L("tpa.list.instructions"));

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = requestList.ToString(),
            };
        }

    }
}