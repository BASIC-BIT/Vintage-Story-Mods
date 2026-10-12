using System.Text.Json;
using AgentControl.Abstractions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AgentControl.Sample;

public sealed class SelectionDescribeModSystem : ModSystem
{
    private IDisposable? _registration;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        if (!api.ObjectCache.TryGetValue(AgentControlContract.RegistryObjectCacheKey, out var value) ||
            value is not IAgentExtensionRegistry registry)
        {
            throw new InvalidOperationException("Agent Control extension registry is unavailable.");
        }

        _registration = registry.Register(
            new AgentExtensionDescriptor(
                "agentcontrol.sample",
                "0.1.0",
                "selection.describe",
                "Describes the client's current block or entity selection."),
            (context, arguments) => Describe(api));
    }

    private static JsonElement Describe(ICoreClientAPI api)
    {
        var player = api.World.Player;
        if (player?.CurrentBlockSelection is { } block)
        {
            return JsonSerializer.SerializeToElement(new
            {
                kind = "block",
                position = new { x = block.Position.X, y = block.Position.Y, z = block.Position.Z },
                face = block.Face?.Code,
                blockCode = api.World.BlockAccessor.GetBlock(block.Position).Code?.ToString()
            });
        }

        if (player?.CurrentEntitySelection?.Entity is { } entity)
        {
            return JsonSerializer.SerializeToElement(new
            {
                kind = "entity",
                entityId = entity.EntityId,
                code = entity.Code?.ToString(),
                name = entity.GetName()
            });
        }

        return JsonSerializer.SerializeToElement(new { kind = "none" });
    }

    public override void Dispose()
    {
        _registration?.Dispose();
        base.Dispose();
    }
}
