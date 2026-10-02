using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentControl.Abstractions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AgentControl;

internal sealed record ChatObservation(long Sequence, int GroupId, string Type, string Message);

internal sealed class VintageStoryAgentGame : IAgentGame
{
    private static readonly HashSet<string> SupportedControls =
    [
        "forward", "backward", "left", "right", "jump", "sneak", "sprint", "primary", "secondary"
    ];

    private readonly ICoreClientAPI _api;
    private readonly ExtensionRegistry _extensions;
    private readonly AgentControlConfig _config;
    private readonly HashSet<string> _ownedControls = new(StringComparer.Ordinal);
    private readonly Queue<ChatObservation> _chat = new();
    private long _chatSequence;

    public VintageStoryAgentGame(ICoreClientAPI api, ExtensionRegistry extensions, AgentControlConfig config)
    {
        _api = api;
        _extensions = extensions;
        _config = config;
    }

    public void RecordChat(int groupId, string message, EnumChatType type)
    {
        _chat.Enqueue(new ChatObservation(++_chatSequence, groupId, type.ToString(), message));
        while (_chat.Count > _config.RecentChatCapacity)
        {
            _chat.Dequeue();
        }
    }

    public object Observe()
    {
        var player = _api.World.Player;
        if (player?.Entity is null)
        {
            return new { connected = false, chatSequence = _chatSequence, recentChat = _chat.ToArray() };
        }

        var pos = player.Entity.Pos;
        var inventoryGroups = player.InventoryManager.InventoriesOrdered
            .Where(inventory => !string.Equals(inventory.ClassName, "creative", StringComparison.OrdinalIgnoreCase))
            .SelectMany(inventory => inventory)
            .Where(slot => !slot.Empty)
            .GroupBy(slot => slot.Itemstack!.Collectible.Code.ToString(), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        const int maxInventoryEntries = 512;
        var inventory = inventoryGroups
            .Take(maxInventoryEntries)
            .ToDictionary(group => group.Key, group => group.Sum(slot => slot.StackSize), StringComparer.Ordinal);
        var hotbar = player.InventoryManager.GetHotbarInventory()
            .Select((slot, index) => new
            {
                slot = index,
                code = slot.Empty ? null : slot.Itemstack?.Collectible.Code.ToString(),
                count = slot.Empty ? 0 : slot.StackSize
            })
            .ToArray();
        return new
        {
            connected = true,
            clientVisible = true,
            player = new
            {
                position = new { x = pos.X, y = pos.Y, z = pos.Z },
                yaw = pos.Yaw,
                pitch = player.CameraPitch,
                onGround = player.Entity.OnGround,
                activeHotbarSlot = player.InventoryManager.ActiveHotbarSlotNumber
            },
            inventory,
            inventoryTruncated = inventoryGroups.Length > maxInventoryEntries,
            hotbar,
            chatSequence = _chatSequence,
            recentChat = _chat.ToArray(),
            currentSelection = DescribeSelection(player),
            extensions = _extensions.List()
        };
    }

    public void SetLook(float yaw, float pitch)
    {
        Audit("look", new { yaw, pitch });
        var player = RequirePlayer();
        var entity = player.Entity;
        entity.Pos.Yaw = yaw;
        player.CameraPitch = Math.Clamp(pitch, -MathF.PI / 2, MathF.PI / 2);
    }

    public void SetControl(string control, bool pressed)
    {
        if (!SupportedControls.Contains(control))
        {
            throw new ArgumentOutOfRangeException(nameof(control), $"Unsupported control '{control}'.");
        }

        Audit("control", new { control, pressed });
        var controls = RequirePlayer().WorldData.EntityControls;
        switch (control)
        {
            case "forward": controls.Forward = pressed; break;
            case "backward": controls.Backward = pressed; break;
            case "left": controls.Left = pressed; break;
            case "right": controls.Right = pressed; break;
            case "jump": controls.Jump = pressed; break;
            case "sneak": controls.Sneak = pressed; break;
            case "sprint": controls.Sprint = pressed; break;
            case "primary": controls.LeftMouseDown = pressed; break;
            case "secondary": controls.RightMouseDown = pressed; break;
        }

        if (pressed)
        {
            _ownedControls.Add(control);
        }
        else
        {
            _ownedControls.Remove(control);
        }
    }

    public void ReleaseOwnedControls()
    {
        if (_api.World.Player is null)
        {
            _ownedControls.Clear();
            return;
        }

        foreach (var control in _ownedControls.ToArray())
        {
            SetControl(control, false);
        }
        _ownedControls.Clear();
    }

    public void SelectSlot(int slot)
    {
        if (slot is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Hotbar slot must be 0-9.");
        }
        Audit("select_slot", new { slot });
        RequirePlayer().InventoryManager.ActiveHotbarSlotNumber = slot;
    }

    public void Send(string text)
    {
        if (!_config.GrantMutationOnEnable)
        {
            throw new UnauthorizedAccessException("This session has no mutation grant.");
        }
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(text), "Chat/command text must be 1-1024 characters.");
        }

        Audit("send", ContentAudit(text));
        _api.SendChatMessage(text);
    }

    public bool Evaluate(JsonElement condition)
    {
        var kind = condition.GetProperty("kind").GetString();
        return kind switch
        {
            "chat_contains" => _chat.Any(item =>
                item.Sequence > condition.GetProperty("afterSequence").GetInt64() &&
                item.Message.Contains(condition.GetProperty("text").GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)),
            "inventory_count" => InventoryCount(condition.GetProperty("code").GetString() ?? string.Empty)
                .CompareTo(condition.GetProperty("count").GetInt32()) is var comparison &&
                Compare(comparison, condition.GetProperty("operator").GetString()),
            "on_ground" => RequirePlayer().Entity.OnGround == condition.GetProperty("value").GetBoolean(),
            _ => throw new InvalidOperationException($"Unknown wait condition '{kind}'.")
        };
    }

    public ValueTask<JsonElement> InvokeExtension(
        string operation,
        string callId,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var descriptor = _extensions.List().SingleOrDefault(item => item.Operation == operation)
            ?? throw new KeyNotFoundException($"Agent operation '{operation}' is not registered.");
        if (descriptor.MutatesState && !_config.GrantMutationOnEnable)
        {
            throw new UnauthorizedAccessException("This session has no mutation grant.");
        }
        Audit("extension.invoke", new { operation, descriptor.MutatesState });
        return ValueTask.FromResult(_extensions.Invoke(operation, callId, arguments, cancellationToken));
    }

    public void Audit(string eventName, object details) =>
        _api.Logger.Audit("[agentcontrol] {0} {1}", eventName, JsonSerializer.Serialize(details, Protocol.Json));

    private IClientPlayer RequirePlayer() =>
        _api.World.Player ?? throw new InvalidOperationException("No client player is active.");

    private int InventoryCount(string code) =>
        RequirePlayer().InventoryManager.InventoriesOrdered
            .Where(inventory => !string.Equals(inventory.ClassName, "creative", StringComparison.OrdinalIgnoreCase))
            .SelectMany(inventory => inventory)
            .Where(slot => !slot.Empty && string.Equals(slot.Itemstack.Collectible.Code.ToString(), code, StringComparison.Ordinal))
            .Sum(slot => slot.StackSize);

    private static bool Compare(int comparison, string? operation) => operation switch
    {
        "eq" => comparison == 0,
        "ne" => comparison != 0,
        "gt" => comparison > 0,
        "gte" => comparison >= 0,
        "lt" => comparison < 0,
        "lte" => comparison <= 0,
        _ => throw new InvalidOperationException($"Unknown comparison operator '{operation}'.")
    };

    private object ContentAudit(string text)
    {
        if (string.Equals(_config.AuditContentMode, "full", StringComparison.OrdinalIgnoreCase))
        {
            return new { kind = text.StartsWith('/') ? "command" : "chat", content = text };
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return new { kind = text.StartsWith('/') ? "command" : "chat", length = text.Length, sha256 = hash };
    }

    internal static object? DescribeSelection(IClientPlayer player)
    {
        if (player.CurrentBlockSelection is { } block)
        {
            return new
            {
                kind = "block",
                position = new { x = block.Position.X, y = block.Position.Y, z = block.Position.Z },
                face = block.Face?.Code
            };
        }
        if (player.CurrentEntitySelection?.Entity is { } entity)
        {
            return new { kind = "entity", entityId = entity.EntityId, code = entity.Code?.ToString() };
        }
        return null;
    }
}
