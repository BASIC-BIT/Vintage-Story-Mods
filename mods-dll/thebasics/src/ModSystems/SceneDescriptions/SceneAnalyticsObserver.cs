using System;
using ProtoBuf;
using thebasics.Extensions;
using thebasics.ModSystems.Analytics;
using thebasics.Utilities.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.SceneDescriptions;

[ProtoContract]
public sealed class SceneObservationPermission
{
    [ProtoMember(1)] public bool Enabled { get; set; }
}
[ProtoContract]
public sealed class SceneObservationMessage
{
    // These coordinates identify the loaded block only for server validation. They are never exported.
    [ProtoMember(1)] public int X { get; set; }
    [ProtoMember(2)] public int Y { get; set; }
    [ProtoMember(3)] public int Z { get; set; }
    [ProtoMember(4)] public int Dimension { get; set; }
    [ProtoMember(5)] public bool Bubble { get; set; }
    [ProtoMember(6)] public bool Held { get; set; }
}

internal sealed class SceneAnalyticsObserver : IDisposable
{
    private const string Channel = "thebasics-scene-observations";
    private readonly SceneObservationGate _gate = new();
    private ICoreClientAPI _client;
    private ICoreServerAPI _server;
    private SafeClientNetworkChannel _safe;
    private IServerNetworkChannel _serverChannel;
    private long _permissionUntil;
    private long _tick;

    internal void StartClient(ICoreClientAPI api)
    {
        _client = api;
        var channel = api.Network.RegisterChannel(Channel).RegisterMessageType<SceneObservationPermission>().RegisterMessageType<SceneObservationMessage>();
        channel.SetMessageHandler<SceneObservationPermission>(message => IgnoreFailure(() => SetPermission(message.Enabled)));
        _safe = new SafeClientNetworkChannel(channel, api, new() { EnableDebugLogging = false, MaxRetries = 0 });
    }
    internal void SetPermission(bool enabled)
    {
        _permissionUntil = enabled ? _client.World.ElapsedMilliseconds + 2500 : 0;
        if (!enabled) _gate.Clear();
    }
    internal void StartServer(ICoreServerAPI api)
    {
        _server = api;
        _serverChannel = api.Network.RegisterChannel(Channel).RegisterMessageType<SceneObservationPermission>().RegisterMessageType<SceneObservationMessage>();
        _serverChannel.SetMessageHandler<SceneObservationMessage>((player, message) => IgnoreFailure(() => Receive(player, message)));
        _tick = api.Event.RegisterGameTickListener(_ =>
        {
            IgnoreFailure(() =>
            {
                var enabled = AnalyticsService.IsEnabled && SceneDescriptionSystem.SceneMarkersEnabled(api);
                if (!enabled) _gate.Clear();
                _serverChannel.BroadcastPacket(new SceneObservationPermission { Enabled = enabled });
            });
        }, 1000);
    }
    internal void ReaderOpened(BlockPos pos = null) => IgnoreFailure(() => Send(pos, false));
    internal void BubbleRendered(BlockPos pos) => IgnoreFailure(() => Send(pos, true));
    private void Send(BlockPos pos, bool bubble)
    {
        var now = _client?.World?.ElapsedMilliseconds ?? 0;
        if (_safe?.IsConnected != true || _permissionUntil == 0 || now >= _permissionUntil) { _gate.Clear(); return; }
        var key = (bubble ? "bubble:" : "reader:") + (pos == null ? "held" : SceneReadMarks.Key(pos));
        if (!(bubble ? _gate.Observe(key, now) : _gate.Accept(key, now, 30000))) return;
        _safe.TrySendPacketWithoutQueue(new SceneObservationMessage { X = pos?.X ?? 0, Y = pos?.Y ?? 0, Z = pos?.Z ?? 0, Dimension = pos?.dimension ?? 0, Held = pos == null, Bubble = bubble });
    }
    internal void Receive(IServerPlayer player, SceneObservationMessage message)
    {
        if (!CanReceive(player, message)) return;
        SceneDescriptionData data;
        string key;
        if (message.Held)
        {
            if (message.Bubble) return;
            var stack = player.InventoryManager?.ActiveHotbarSlot?.Itemstack;
            if (stack?.Block is not SceneDescriptionBlock) return;
            data = SceneDescriptionData.ReadFrom(stack.Attributes);
            if (string.IsNullOrWhiteSpace(data.Body)) return;
            key = "held";
        }
        else
        {
            var pos = new BlockPos(message.X, message.Y, message.Z, message.Dimension);
            if (player.Entity.Pos.Dimension != pos.dimension || _server.World.BlockAccessor.GetChunkAtBlockPos(pos) == null ||
                _server.World.BlockAccessor.GetBlockEntity(pos) is not SceneDescriptionBlockEntity marker) return;
            data = marker.Data;
            var distance = player.Entity.Pos.XYZ.DistanceTo(pos.ToVec3d().Add(0.5, 0.65, 0.5));
            var targeted = player.CurrentBlockSelection?.Position?.Equals(pos) == true;
            if (!ObservationAllowed(data, distance, message.Bubble, targeted)) return;
            if (message.Bubble && SceneReadMarks.IsRead(player.GetSceneReadMarks().Marks, SceneReadMarks.Key(pos), data.ReadStamp)) return;
            key = SceneReadMarks.Key(pos);
        }
        var action = message.Bubble ? "bubble_viewed" : "reader_opened";
        if (!_gate.Accept(player.PlayerUID + ":" + action + ":" + key, _server.World.ElapsedMilliseconds, message.Bubble ? 60000 : 30000)) return;
        SceneAnalytics.Track(data, action, message.Bubble ? null : "scene_read_source", message.Held ? "held" : "placed");
    }
    private bool CanReceive(IServerPlayer player, SceneObservationMessage message) =>
        AnalyticsService.IsEnabled && SceneDescriptionSystem.SceneMarkersEnabled(_server) && player?.Entity != null && message != null;

    internal static bool ObservationAllowed(SceneDescriptionData data, double distance, bool bubble, bool targeted)
    {
        if (!double.IsFinite(distance) || distance < 0) return false;
        if (!bubble) return distance <= 8;
        return data.ShouldShowDescription(targeted) && (data.Display == SceneDescriptionDisplay.WhenTargeted && targeted || data.GetTextOpacity(distance) >= 0.5f);
    }
    private static void IgnoreFailure(Action action)
    {
        try { action(); }
        catch { /* Observations must not interrupt game callbacks. */ }
    }
    public void Dispose()
    {
        if (_server != null) _server.Event.UnregisterGameTickListener(_tick);
        _safe?.Dispose();
        _permissionUntil = 0;
        _gate.Clear();
    }
}
