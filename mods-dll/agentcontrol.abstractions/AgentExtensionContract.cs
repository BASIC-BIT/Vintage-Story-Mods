using System.Text.Json;
using Vintagestory.API.Client;

namespace AgentControl.Abstractions;

public static class AgentControlContract
{
    public const string RegistryObjectCacheKey = "agentcontrol:registry";
    public const string ProtocolVersion = "1.0";
}

public sealed record AgentExtensionDescriptor(
    string ExtensionId,
    string ExtensionVersion,
    string Operation,
    string Description,
    bool MutatesState = false);

public sealed record AgentExtensionContext(
    string CallId,
    ICoreClientAPI ClientApi,
    CancellationToken CancellationToken);

public delegate JsonElement AgentExtensionHandler(
    AgentExtensionContext context,
    JsonElement arguments);

public interface IAgentExtensionRegistry
{
    ICoreClientAPI ClientApi { get; }

    IDisposable Register(
        AgentExtensionDescriptor descriptor,
        AgentExtensionHandler handler);
}
