using FluentAssertions;

namespace AgentControl.Tests;

public sealed class GameThreadDispatcherTests
{
    [Fact]
    public async Task Shutdown_FaultsWorkThatIsStillQueued()
    {
        var dispatcher = new GameThreadDispatcher();
        var pending = dispatcher.Invoke(() => 1);

        dispatcher.Shutdown();

        var awaitPending = async () => await pending;
        await awaitPending.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Shutdown_RejectsLaterWorkWithoutRunningIt()
    {
        var dispatcher = new GameThreadDispatcher();
        dispatcher.Shutdown();
        var invoked = false;

        var awaitRejected = async () => await dispatcher.Invoke(() => invoked = true).WaitAsync(TimeSpan.FromSeconds(5));

        await awaitRejected.Should().ThrowAsync<ObjectDisposedException>();
        dispatcher.Drain();
        invoked.Should().BeFalse();
    }
}
