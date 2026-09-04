using System.Text.Json;

namespace AgentControl.Abstractions;

public static class AgentControlContract
{
    public const string RegistryObjectCacheKey = "agentcontrol:registry";
    public const string ProtocolVersion = "1.0";
}

// MutatesState is self-declared by the extension and trusted when a session has no mutation grant.
public sealed record AgentExtensionDescriptor(
    string ExtensionId,
    string ExtensionVersion,
    string Operation,
    string Description,
    bool MutatesState = false);

public sealed record AgentExtensionContext(
    string CallId,
    CancellationToken CancellationToken);

public delegate JsonElement AgentExtensionHandler(
    AgentExtensionContext context,
    JsonElement arguments);

public interface IAgentExtensionRegistry
{
    IDisposable Register(
        AgentExtensionDescriptor descriptor,
        AgentExtensionHandler handler);
}
