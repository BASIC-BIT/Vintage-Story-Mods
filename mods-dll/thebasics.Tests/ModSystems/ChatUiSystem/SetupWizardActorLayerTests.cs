using NSubstitute;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardActorLayerTests
{
    [Fact]
    public void ActorLayerDoesNotIntersectANullNativeScissorEntry()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var stack = new Stack<ElementBounds>();
        stack.Push(null!);
        api.Render.ScissorStack.Returns(stack);
        var actor = Substitute.For<ISetupGuidePreview>();
        using var pool = new SetupWizardActorPool(api, (_, _) => actor);
        var bounds = ElementBounds.Fixed(0, 0, 100, 100).WithEmptyParent();
        var layer = new SetupWizardActorLayer(api, bounds, pool,
            () => [new("pip", "Pip", "idle", 0, 0, 0, 30, 48)], () => true);

        layer.RenderInteractiveElements(0);

        api.Render.Received(1).PushScissor(bounds, false);
    }

    [Fact]
    public void ActorLayerPinsSeeksAndResumesWithoutRestartingEachLiveFrame()
    {
        var api = Substitute.For<ICoreClientAPI>();
        api.Render.ScissorStack.Returns(new Stack<ElementBounds>());
        var actor = Substitute.For<ISetupGuidePreview>();
        actor.IsReady.Returns(true);
        var created = 0;
        using var pool = new SetupWizardActorPool(api, (_, identity) => { Assert.Equal("Pip", identity); created++; return actor; });
        var pinned = true;
        double time = 0.75;
        var bounds = ElementBounds.Fixed(20, 30, 310, 180).WithEmptyParent();
        var layer = new SetupWizardActorLayer(api, bounds, pool,
            () => [new("pip", "Pip", "wave", time, 40, 10, 42, 70, 0.55f, 0.2f)], () => pinned);
        layer.ComposeElements(null!, null!);
        Assert.True(layer.IsReady);
        layer.RenderInteractiveElements(0.1f);
        actor.Received(1).SetAnimation("wave", 0.75);
        actor.Received().Render(0, Arg.Is<ElementBounds>(value => value.fixedX == 40 && value.fixedY == 10), 0.55f, 0.2f);
        layer.RenderInteractiveElements(0.1f);
        actor.Received(1).SetAnimation("wave", 0.75);

        time = 1.25;
        layer.RenderInteractiveElements(0.1f);
        actor.Received(1).SetAnimation("wave", 1.25);
        pinned = false;
        layer.RenderInteractiveElements(0.1f);
        actor.Received(1).PlayAnimation("wave");
        time = 1.35;
        layer.RenderInteractiveElements(0.1f);
        actor.Received(1).PlayAnimation("wave");
        Assert.Equal(2, actor.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ISetupGuidePreview.SetAnimation)));
        pinned = true;
        layer.RenderInteractiveElements(0.1f);
        actor.Received(1).SetAnimation("wave", 1.35);
        Assert.Equal(1, created);
    }

    [Fact]
    public void SeparateActorSlotsHaveIndependentInstancesAndAreDisposedOnce()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var actors = new List<ISetupGuidePreview>();
        var pool = new SetupWizardActorPool(api, (_, _) =>
        {
            var actor = Substitute.For<ISetupGuidePreview>();
            actors.Add(actor);
            return actor;
        });
        var first = pool.Get("sleep-0", "Pip");
        Assert.Same(first, pool.Get("sleep-0", "Pip"));
        Assert.NotSame(first, pool.Get("sleep-1", "Pip"));
        Assert.NotSame(first, pool.Get("sleep-0", "Mara"));
        pool.Dispose();
        pool.Dispose();
        Assert.Equal(3, actors.Count);
        foreach (var actor in actors) actor.Received(1).Dispose();
    }

    [Theory]
    [InlineData(112d)]
    [InlineData(140d)]
    public void SleepUsesFourIndependentNativeActorsAndOnlySleepingPlayersUseTheLiePose(double height)
    {
        var actors = SetupWizardDialog.SleepActors(4, height);
        Assert.Equal(4, actors.Length);
        Assert.Equal(4, actors.Select(actor => actor.Slot).Distinct().Count());
        Assert.Equal(new[] { "sleep", "sleep", "idle", "idle" }, actors.Select(actor => actor.Animation));
        Assert.Equal(new[] { "Pip", "Mara", "Pip", "Mara" }, actors.Select(actor => actor.Identity));
        foreach (var actor in actors)
        {
            Assert.InRange(actor.X, 0, 310 - actor.Width);
            Assert.InRange(actor.Y, 0, height - actor.Height);
        }
        Assert.All(SetupWizardDialog.SleepActors(0, height), actor => Assert.Equal("idle", actor.Animation));
    }
}
