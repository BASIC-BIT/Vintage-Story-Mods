using System.Collections.Concurrent;
using System.Text.Json;
using AgentControl.Abstractions;

namespace AgentControl;

internal sealed record RegisteredExtension(AgentExtensionDescriptor Descriptor, AgentExtensionHandler Handler);

internal sealed class ExtensionRegistry : IAgentExtensionRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredExtension> _operations = new(StringComparer.Ordinal);

    public IDisposable Register(AgentExtensionDescriptor descriptor, AgentExtensionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(handler);
        ValidateName(descriptor.ExtensionId, nameof(descriptor.ExtensionId));
        ValidateName(descriptor.Operation, nameof(descriptor.Operation));

        var registration = new RegisteredExtension(descriptor, handler);
        if (!_operations.TryAdd(descriptor.Operation, registration))
        {
            throw new InvalidOperationException($"Agent operation '{descriptor.Operation}' is already registered.");
        }

        return new Registration(() => _operations.TryRemove(
            new KeyValuePair<string, RegisteredExtension>(descriptor.Operation, registration)));
    }

    public IReadOnlyList<AgentExtensionDescriptor> List() =>
        _operations.Values.Select(value => value.Descriptor).OrderBy(value => value.Operation, StringComparer.Ordinal).ToArray();

    public JsonElement Invoke(
        string operation,
        string callId,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!_operations.TryGetValue(operation, out var registration))
        {
            throw new KeyNotFoundException($"Agent operation '{operation}' is not registered.");
        }

        return registration.Handler(
            new AgentExtensionContext(callId, cancellationToken),
            arguments);
    }

    private static void ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new ArgumentException("Names must be 1-128 ASCII letters, digits, dots, dashes, or underscores.", parameterName);
        }
    }

    private sealed class Registration(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
