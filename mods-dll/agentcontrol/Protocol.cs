using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentControl;

public static class Protocol
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = false
    };

    public static readonly HashSet<string> Methods =
    [
        "hello",
        "observe",
        "execute",
        "cancel",
        "extensions.list",
        "shutdownSession"
    ];
}

public sealed record RpcRequest(
    string Id,
    string Method,
    JsonElement? Params = null,
    string? Session = null);

public sealed record RpcError(string Code, string Message);

public sealed record RpcResponse(
    string Id,
    bool Ok,
    object? Result = null,
    RpcError? Error = null);

public sealed record ActionReceipt(
    int Index,
    string Type,
    string Status,
    long StartedMs,
    long FinishedMs,
    object? Detail = null);

public sealed record ExecutionReceipt(
    string ExecutionId,
    string Status,
    long StartedMs,
    long FinishedMs,
    IReadOnlyList<ActionReceipt> Actions,
    string? Error = null);
