using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentControl;

internal sealed class NamedPipeRpcServer : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly int _maxRequestBytes;
    private readonly Func<RpcRequest, Task<RpcResponse>> _handler;
    private readonly Action _onClientDisconnect;
    private readonly Action<Exception> _onFailure;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _acceptLoop;

    public NamedPipeRpcServer(
        string pipeName,
        int maxRequestBytes,
        Func<RpcRequest, Task<RpcResponse>> handler,
        Action? onClientDisconnect = null,
        Action<Exception>? onFailure = null)
    {
        _pipeName = pipeName;
        _maxRequestBytes = maxRequestBytes;
        _handler = handler;
        _onClientDisconnect = onClientDisconnect ?? (() => { });
        _onFailure = onFailure ?? (_ => { });
    }

    public void Start()
    {
        _acceptLoop = AcceptLoop();
        _ = _acceptLoop.ContinueWith(
            task => _onFailure(task.Exception!.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task AcceptLoop()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);
                _ = HandleConnection(pipe);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                break;
            }
            catch
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }

    private async Task HandleConnection(NamedPipeServerStream pipe)
    {
        await using (pipe.ConfigureAwait(false))
        using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true))
        await using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
        {
            RpcResponse response;
            try
            {
                var line = await ReadBoundedLine(reader, _shutdown.Token).ConfigureAwait(false);
                var request = JsonSerializer.Deserialize<RpcRequest>(line, Protocol.Json)
                    ?? throw new JsonException("Request was empty.");
                using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                var handlerTask = _handler(request);
                var disconnectProbe = new byte[1];
                var disconnectTask = pipe.ReadAsync(disconnectProbe, connectionCancellation.Token).AsTask();
                var completed = await Task.WhenAny(handlerTask, disconnectTask).ConfigureAwait(false);
                if (completed == disconnectTask)
                {
                    _onClientDisconnect();
                }
                else
                {
                    connectionCancellation.Cancel();
                }
                response = await handlerTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                response = new RpcResponse(string.Empty, false, Error: new RpcError("invalid_request", ex.Message));
            }
            try
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(response, Protocol.Json)).ConfigureAwait(false);
            }
            catch (IOException) when (!pipe.IsConnected)
            {
            }
        }
    }

    private async Task<string> ReadBoundedLine(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[Math.Min(_maxRequestBytes, 4096)];
        var builder = new StringBuilder();
        var byteCount = 0;
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            var newline = Array.IndexOf(buffer, '\n', 0, read);
            var take = newline < 0 ? read : newline;
            byteCount += Encoding.UTF8.GetByteCount(buffer, 0, take);
            if (byteCount > _maxRequestBytes)
            {
                throw new InvalidDataException("Request exceeds maximum size.");
            }
            builder.Append(buffer, 0, take);
            if (newline >= 0)
            {
                return builder.ToString().TrimEnd('\r');
            }
        }
        return builder.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Accept loop failures are reported through the failure callback.
            }
        }
        _shutdown.Dispose();
    }

    public static string CreateSessionSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
