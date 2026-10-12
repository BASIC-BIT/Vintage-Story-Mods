using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace AgentControl.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void RpcSurface_IsExactlyTheApprovedSixMethods()
    {
        Protocol.Methods.Should().BeEquivalentTo(
            "hello", "observe", "execute", "cancel", "extensions.list", "shutdownSession");
        Protocol.Methods.Should().HaveCount(6);
    }

    [Fact]
    public void RequestEnvelope_UsesStrictCaseSensitiveNames()
    {
        var request = JsonSerializer.Deserialize<RpcRequest>(
            """{"id":"1","method":"observe","params":{},"session":"secret"}""",
            Protocol.Json);
        request.Should().NotBeNull();
        request!.Method.Should().Be("observe");

        var wrongCase = JsonSerializer.Deserialize<RpcRequest>(
            """{"Id":"1","Method":"observe"}""",
            Protocol.Json);
        wrongCase!.Method.Should().BeNull();
    }

    [Fact]
    public async Task NamedPipeServer_RejectsOversizedRequests()
    {
        var pipeName = $"agentcontrol-test-{Guid.NewGuid():n}";
        await using var server = new NamedPipeRpcServer(pipeName, 128, request =>
            Task.FromResult(new RpcResponse(request.Id, true, new { accepted = true })));
        server.Start();
        await using var client = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        await client.ConnectAsync(5_000, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(client, Encoding.UTF8, false, 256, true);
        var oversized = Encoding.UTF8.GetBytes(new string('x', 256) + "\n");
        await client.WriteAsync(oversized, TestContext.Current.CancellationToken);
        await client.FlushAsync(TestContext.Current.CancellationToken);
        var responseText = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
        var response = JsonSerializer.Deserialize<RpcResponse>(responseText!, Protocol.Json);
        response!.Ok.Should().BeFalse();
        response.Error!.Code.Should().Be("invalid_request");
        response.Error.Message.Should().Contain("maximum size");
    }

    [Fact]
    public async Task NamedPipeServer_DisconnectInvokesImmediateCancellationHook()
    {
        var pipeName = $"agentcontrol-disconnect-{Guid.NewGuid():n}";
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerRelease = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new NamedPipeRpcServer(
            pipeName,
            4096,
            _ => handlerRelease.Task,
            () =>
            {
                disconnected.TrySetResult();
                handlerRelease.TrySetResult(new RpcResponse("1", true));
            });
        server.Start();

        await using (var client = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(5_000, TestContext.Current.CancellationToken);
            var request = Encoding.UTF8.GetBytes("""{"id":"1","method":"execute","params":{"actions":[]}}""" + "\n");
            await client.WriteAsync(request, TestContext.Current.CancellationToken);
            await client.FlushAsync(TestContext.Current.CancellationToken);
        }

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
