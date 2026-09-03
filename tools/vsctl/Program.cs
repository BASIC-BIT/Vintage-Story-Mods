using System.IO.Pipes;
using System.Text;
using System.Text.Json;

try
{
    var parsed = Arguments.Parse(args);
    var client = new RpcClient(parsed.PipeName);
    var hello = await client.Invoke("hello", null, null);
    if (!hello.GetProperty("ok").GetBoolean())
    {
        throw new InvalidOperationException(hello.ToString());
    }

    var helloResult = hello.GetProperty("result");
    var session = helloResult.GetProperty("session").GetString()
        ?? throw new InvalidOperationException("The controller did not return a session secret.");
    JsonElement response = parsed.Command switch
    {
        "hello" => RedactHello(hello),
        "observe" => await client.Invoke("observe", null, session),
        "extensions" => await client.Invoke("extensions.list", null, session),
        "execute" => await client.Invoke("execute", ReadExecuteParams(parsed.File), session),
        "cancel" => await client.Invoke("cancel", parsed.ExecutionId is null
            ? JsonSerializer.SerializeToElement(new { })
            : JsonSerializer.SerializeToElement(new { executionId = parsed.ExecutionId }), session),
        "shutdown" => await client.Invoke("shutdownSession", JsonSerializer.SerializeToElement(new { }), session),
        _ => throw new InvalidOperationException($"Unknown command '{parsed.Command}'.")
    };
    Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
    return response.GetProperty("ok").GetBoolean() ? 0 : 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"vsctl: {ex.Message}");
    return 1;
}

static JsonElement RedactHello(JsonElement hello)
{
    var result = hello.GetProperty("result");
    return JsonSerializer.SerializeToElement(new
    {
        id = hello.GetProperty("id").GetString(),
        ok = true,
        result = new
        {
            protocolVersion = result.GetProperty("protocolVersion").GetString(),
            modVersion = result.GetProperty("modVersion").GetString(),
            gameVersion = result.GetProperty("gameVersion").GetString(),
            session = "[redacted]",
            mutationGranted = result.GetProperty("mutationGranted").GetBoolean(),
            methods = result.GetProperty("methods")
        }
    });
}

static JsonElement ReadExecuteParams(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        throw new ArgumentException("execute requires --file <path>.");
    }
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var root = document.RootElement;
    if (root.ValueKind == JsonValueKind.Array)
    {
        return JsonSerializer.SerializeToElement(new { actions = root.Clone() });
    }
    if (root.TryGetProperty("actions", out _))
    {
        return root.Clone();
    }
    return JsonSerializer.SerializeToElement(new { actions = new[] { root.Clone() } });
}

internal sealed record Arguments(string Command, string PipeName, string? File, string? ExecutionId)
{
    public static Arguments Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            throw new ArgumentException("usage: vsctl <hello|observe|extensions|execute|cancel|shutdown> [--pipe NAME] [--file PATH] [--execution-id ID]");
        }
        var pipe = "vintage-story-agentcontrol";
        string? file = null;
        string? executionId = null;
        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--pipe": pipe = RequireValue(args, ref index); break;
                case "--file": file = RequireValue(args, ref index); break;
                case "--execution-id": executionId = RequireValue(args, ref index); break;
                default: throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }
        return new Arguments(args[0], pipe, file, executionId);
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException("Option value is missing.");
        }
        return args[index];
    }
}

internal sealed class RpcClient(string pipeName)
{
    public async Task<JsonElement> Invoke(string method, JsonElement? parameters, string? session)
    {
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5_000);
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
        var request = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid().ToString("n"),
            method,
            @params = parameters,
            session
        });
        await writer.WriteLineAsync(request);
        var response = await reader.ReadLineAsync()
            ?? throw new EndOfStreamException("Controller closed the pipe without a response.");
        if (Encoding.UTF8.GetByteCount(response) > 1_048_576)
        {
            throw new InvalidDataException("Controller response exceeded the CLI limit.");
        }
        using var document = JsonDocument.Parse(response);
        return document.RootElement.Clone();
    }
}
