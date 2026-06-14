using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.Teleportation;

public sealed class TeleportationSystem : BaseBasicModSystem
{
    private const double MovementCancelDistanceSquared = 0.05 * 0.05;
    private readonly Dictionary<string, PendingTeleportWarmup> _pendingWarmups = new(StringComparer.Ordinal);
    private long _warmupTickListener;

    protected override void BasicStartServerSide()
    {
        TeleportBackGlobalRecorder.Patch();
        API.Event.HandInteract += OnHandInteract;
        API.Event.DidUseBlock += OnDidUseBlock;
        API.Event.DidPlaceBlock += OnDidPlaceBlock;
        API.Event.DidBreakBlock += OnDidBreakBlock;
        API.Event.OnPlayerInteractEntity += OnPlayerInteractEntity;
        API.Event.BeforeActiveSlotChanged += OnBeforeActiveSlotChanged;
        API.Event.PlayerDeath += OnPlayerDeath;
        API.Event.PlayerDisconnect += OnPlayerDisconnect;
        _warmupTickListener = API.World.RegisterGameTickListener(UpdateWarmups, 100);
    }

    public TextCommandResult BeginWarmup(TeleportWarmupRequest request)
    {
        if (request?.Player?.Entity == null)
        {
            return Error(Lang.Get("thebasics:teleport-warmup-error-player-required"), "player-required");
        }

        if (request.Execute == null)
        {
            return Error(Lang.Get("thebasics:teleport-warmup-error-unavailable"), "teleport-unavailable");
        }

        if (request.WarmupSeconds <= 0)
        {
            return Execute(request);
        }

        var playerUid = request.Player.PlayerUID;
        if (_pendingWarmups.ContainsKey(playerUid))
        {
            return Error(Lang.Get("thebasics:teleport-warmup-error-already-active"), "teleport-warmup-active");
        }

        var pending = new PendingTeleportWarmup(request, DateTime.UtcNow.AddSeconds(request.WarmupSeconds));
        _pendingWarmups[playerUid] = pending;
        SubscribeDamageCancellation(pending);

        return Success(request.StartMessage ?? Lang.Get("thebasics:teleport-warmup-start", request.WarmupSeconds));
    }

    public bool CancelWarmup(IServerPlayer player, string reason)
    {
        if (player?.PlayerUID == null)
        {
            return false;
        }

        return CancelWarmup(player.PlayerUID, reason);
    }

    public override void Dispose()
    {
        if (_warmupTickListener != 0)
        {
            API.World.UnregisterGameTickListener(_warmupTickListener);
            _warmupTickListener = 0;
        }

        if (API?.Event != null)
        {
            API.Event.HandInteract -= OnHandInteract;
            API.Event.DidUseBlock -= OnDidUseBlock;
            API.Event.DidPlaceBlock -= OnDidPlaceBlock;
            API.Event.DidBreakBlock -= OnDidBreakBlock;
            API.Event.OnPlayerInteractEntity -= OnPlayerInteractEntity;
            API.Event.BeforeActiveSlotChanged -= OnBeforeActiveSlotChanged;
            API.Event.PlayerDeath -= OnPlayerDeath;
            API.Event.PlayerDisconnect -= OnPlayerDisconnect;
        }

        foreach (var pending in _pendingWarmups.Values.ToArray())
        {
            UnsubscribeDamageCancellation(pending);
        }

        _pendingWarmups.Clear();
        TeleportBackGlobalRecorder.Unpatch();
        base.Dispose();
    }

    private void UpdateWarmups(float dt)
    {
        foreach (var pending in _pendingWarmups.Values.ToArray())
        {
            if (!_pendingWarmups.ContainsKey(pending.Request.Player.PlayerUID))
            {
                continue;
            }

            if (pending.Request.Player.Entity == null || !pending.Request.Player.Entity.Alive)
            {
                CancelWarmup(pending.Request.Player.PlayerUID, "death");
                continue;
            }

            if (HasMoved(pending))
            {
                CancelWarmup(pending.Request.Player.PlayerUID, "movement");
                continue;
            }

            if (DateTime.UtcNow >= pending.CompleteAfterUtc)
            {
                CompleteWarmup(pending);
                continue;
            }

            SendReminderIfDue(pending);
        }
    }

    private static void SendReminderIfDue(PendingTeleportWarmup pending)
    {
        if (pending.Request.ReminderIntervalSeconds <= 0 || pending.Request.ReminderMessage == null)
        {
            return;
        }

        if (DateTime.UtcNow < pending.NextReminderUtc)
        {
            return;
        }

        var remainingSeconds = Math.Max(1, (int)Math.Ceiling((pending.CompleteAfterUtc - DateTime.UtcNow).TotalSeconds));
        var message = pending.Request.ReminderMessage(remainingSeconds);
        if (!string.IsNullOrWhiteSpace(message))
        {
            pending.Request.Player.SendMessage(GlobalConstants.CurrentChatGroup, message, EnumChatType.Notification);
        }

        pending.NextReminderUtc = DateTime.UtcNow.AddSeconds(pending.Request.ReminderIntervalSeconds);
    }

    private static bool HasMoved(PendingTeleportWarmup pending)
    {
        var pos = pending.Request.Player.Entity.Pos;
        var dx = pos.X - pending.StartX;
        var dy = pos.Y - pending.StartY;
        var dz = pos.Z - pending.StartZ;
        return dx * dx + dy * dy + dz * dz > MovementCancelDistanceSquared;
    }

    private void CompleteWarmup(PendingTeleportWarmup pending)
    {
        _pendingWarmups.Remove(pending.Request.Player.PlayerUID);
        UnsubscribeDamageCancellation(pending);

        var result = Execute(pending.Request);
        if (!string.IsNullOrWhiteSpace(result?.StatusMessage))
        {
            pending.Request.Player.SendMessage(GlobalConstants.CurrentChatGroup, result.StatusMessage, ChatType(result.Status));
        }
    }

    private TextCommandResult Execute(TeleportWarmupRequest request)
    {
        try
        {
            return request.Execute(request.Player) ?? Error(Lang.Get("thebasics:teleport-warmup-error-unavailable"), "teleport-unavailable");
        }
        catch (Exception exception)
        {
            API.Logger.Error($"THEBASICS: Teleport warmup execution failed for {request.Player.PlayerName}: {exception}");
            return Error(Lang.Get("thebasics:teleport-warmup-error-unavailable"), "teleport-unavailable");
        }
    }

    private bool CancelWarmup(string playerUid, string reason)
    {
        if (!_pendingWarmups.TryGetValue(playerUid, out var pending))
        {
            return false;
        }

        _pendingWarmups.Remove(playerUid);
        UnsubscribeDamageCancellation(pending);
        pending.Request.OnCancelled?.Invoke(pending.Request.Player, reason);

        if (reason != "disconnect" && pending.Request.Player?.Entity != null)
        {
            pending.Request.Player.SendMessage(GlobalConstants.CurrentChatGroup, CancelMessage(reason), EnumChatType.CommandError);
        }

        return true;
    }

    private static string CancelMessage(string reason)
    {
        return reason switch
        {
            "movement" => Lang.Get("thebasics:teleport-warmup-cancelled-movement"),
            "damage" => Lang.Get("thebasics:teleport-warmup-cancelled-damage"),
            "interaction" => Lang.Get("thebasics:teleport-warmup-cancelled-interaction"),
            "death" => Lang.Get("thebasics:teleport-warmup-cancelled-death"),
            _ => Lang.Get("thebasics:teleport-warmup-cancelled")
        };
    }

    private void SubscribeDamageCancellation(PendingTeleportWarmup pending)
    {
        if (!pending.Request.CancelOnDamage)
        {
            return;
        }

        var health = pending.Request.Player.Entity.GetBehavior<EntityBehaviorHealth>();
        if (health == null)
        {
            return;
        }

        pending.DamageHandler = (damage, source) =>
        {
            if (damage > 0f && source?.Type != EnumDamageType.Heal)
            {
                CancelWarmup(pending.Request.Player.PlayerUID, "damage");
            }

            return damage;
        };
        health.onDamaged += pending.DamageHandler;
    }

    private static void UnsubscribeDamageCancellation(PendingTeleportWarmup pending)
    {
        if (pending.DamageHandler == null || pending.Request.Player?.Entity == null)
        {
            return;
        }

        var health = pending.Request.Player.Entity.GetBehavior<EntityBehaviorHealth>();
        if (health != null)
        {
            health.onDamaged -= pending.DamageHandler;
        }

        pending.DamageHandler = null;
    }

    private void CancelForInteraction(IServerPlayer player)
    {
        if (player?.PlayerUID == null || !_pendingWarmups.TryGetValue(player.PlayerUID, out var pending))
        {
            return;
        }

        if (pending.Request.CancelOnInteraction)
        {
            CancelWarmup(player.PlayerUID, "interaction");
        }
    }

    private void OnHandInteract(IServerPlayer player, EnumHandInteractNw enumHandInteract, float secondsPassed, ref EnumHandling handling)
    {
        CancelForInteraction(player);
    }

    private void OnDidUseBlock(IServerPlayer byPlayer, BlockSelection blockSel)
    {
        CancelForInteraction(byPlayer);
    }

    private void OnDidPlaceBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel, ItemStack withItemStack)
    {
        CancelForInteraction(byPlayer);
    }

    private void OnDidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
    {
        CancelForInteraction(byPlayer);
    }

    private void OnPlayerInteractEntity(Entity entity, IPlayer byPlayer, ItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling handling)
    {
        CancelForInteraction(byPlayer as IServerPlayer);
    }

    private EnumHandling OnBeforeActiveSlotChanged(IServerPlayer player, ActiveSlotChangeEventArgs args)
    {
        CancelForInteraction(player);
        return EnumHandling.PassThrough;
    }

    private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
    {
        CancelWarmup(byPlayer, "death");
    }

    private void OnPlayerDisconnect(IServerPlayer byPlayer)
    {
        CancelWarmup(byPlayer, "disconnect");
    }

    private static EnumChatType ChatType(EnumCommandStatus status)
    {
        return status == EnumCommandStatus.Success ? EnumChatType.CommandSuccess : EnumChatType.CommandError;
    }

    private static TextCommandResult Success(string message)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = message
        };
    }

    private static TextCommandResult Error(string message, string errorCode)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Error,
            StatusMessage = message,
            ErrorCode = errorCode
        };
    }

    private sealed class PendingTeleportWarmup
    {
        public PendingTeleportWarmup(TeleportWarmupRequest request, DateTime completeAfterUtc)
        {
            Request = request;
            CompleteAfterUtc = completeAfterUtc;
            StartX = request.Player.Entity.Pos.X;
            StartY = request.Player.Entity.Pos.Y;
            StartZ = request.Player.Entity.Pos.Z;
            NextReminderUtc = DateTime.UtcNow.AddSeconds(request.ReminderIntervalSeconds);
        }

        public TeleportWarmupRequest Request { get; }

        public DateTime CompleteAfterUtc { get; }

        public double StartX { get; }

        public double StartY { get; }

        public double StartZ { get; }

        public DateTime NextReminderUtc { get; set; }

        public OnDamagedDelegate DamageHandler { get; set; }
    }
}
