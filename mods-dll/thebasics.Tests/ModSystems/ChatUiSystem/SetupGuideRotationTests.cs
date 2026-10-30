using NSubstitute;
using System.Reflection;
using System.Runtime.CompilerServices;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupGuideRotationTests
{
    [Fact]
    public void DragCoastsThenSettlesAndNewGesturesAndCapturesCancelMomentum()
    {
        long clock = 1000;
        var api = Substitute.For<ICoreClientAPI>();
        api.ElapsedMilliseconds.Returns(_ => clock);
        api.Render.ScissorStack.Returns(new Stack<ElementBounds>());
        var actorType = typeof(SetupGuidePreview).GetNestedType("DetachedGuideEntity", BindingFlags.NonPublic)!;
        var actor = (Entity)Activator.CreateInstance(actorType)!;
        typeof(Entity).GetProperty(nameof(Entity.Properties))!.SetValue(actor, new EntityProperties
        {
            Client = new EntityClientProperties([], null)
            {
                AnimationsByMetaCode = new()
                {
                    ["idle"] = new() { Code = "idle", Animation = "idle" }
                }
            }
        });
        var animator = new ClientAnimator(() => 1, [], [], new Dictionary<int, AnimationJoint>());
        actor.AnimManager.Animator = animator;
        var preview = (SetupGuidePreview)RuntimeHelpers.GetUninitializedObject(typeof(SetupGuidePreview));
        var fixtureField = typeof(SetupGuidePreview).GetField("fixture", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fixture = Activator.CreateInstance(fixtureField.FieldType)!;
        fixtureField.FieldType.GetProperty("Animations")!.SetValue(fixture, new[] { "idle" });
        fixtureField.SetValue(preview, fixture);
        SetField(preview, "api", api);
        SetField(preview, "actor", actor);
        SetField(preview, "animationCode", "idle");
        SetField(preview, "previewAnimator", animator);
        SetField(preview, "rotationYaw", -1.2707963f);
        var bounds = ElementBounds.Fixed(0, 0, 120, 168);
        bounds.ParentBounds = ElementBounds.Empty;
        bounds.CalcWorldBounds();

        float RenderYaw(float dt)
        {
            preview.Render(dt, bounds);
            return (float)api.Render.ReceivedCalls()
                .Last(call => call.GetMethodInfo().Name == nameof(IRenderAPI.RenderEntityToGui)).GetArguments()[5]!;
        }

        float initial = RenderYaw(0);
        preview.BeginDrag(100);
        clock += 20;
        preview.DragTo(150);
        float dragged = RenderYaw(0);
        Assert.InRange(initial - dragged, 0.499f, 0.501f);
        preview.DragTo(155);
        float sameClockMove = RenderYaw(0);
        Assert.InRange(dragged - sameClockMove, 0.049f, 0.051f);
        dragged = sameClockMove;
        preview.EndDrag();
        float firstCoast = RenderYaw(0.016f);
        float secondCoast = RenderYaw(0.016f);
        Assert.InRange(dragged - firstCoast, 0.1f, 0.2f);
        Assert.InRange(firstCoast - secondCoast, 0.01f, dragged - firstCoast);

        // Catch the moving character; a gesture without motion must not reuse the previous fling.
        preview.BeginDrag(400);
        Assert.Equal(secondCoast, RenderYaw(0.016f));
        preview.EndDrag();
        Assert.Equal(secondCoast, RenderYaw(0.016f));

        preview.BeginDrag(100);
        clock += 20;
        preview.DragTo(80);
        float oppositeDrag = RenderYaw(0);
        Assert.InRange(oppositeDrag - secondCoast, 0.199f, 0.201f);
        preview.EndDrag();
        float boundedCoast = RenderYaw(10);
        Assert.InRange(boundedCoast - oppositeDrag, 0.1f, 0.5f);
        Assert.Equal(boundedCoast, RenderYaw(float.NaN));
        for (int frame = 0; frame < 200; frame++) RenderYaw(1f / 60);
        float settled = RenderYaw(0);
        Assert.Equal(settled, RenderYaw(1));

        // Holding still before release drops stale velocity, and explicit cancellation also stops it.
        preview.BeginDrag(100);
        clock += 20;
        preview.DragTo(110);
        float held = RenderYaw(0);
        clock += 200;
        preview.EndDrag();
        Assert.Equal(held, RenderYaw(0.05f));
        preview.BeginDrag(100);
        clock += 20;
        preview.DragTo(110);
        float cancelled = RenderYaw(0);
        preview.EndDrag(false);
        Assert.Equal(cancelled, RenderYaw(0.05f));

        preview.BeginDrag(100);
        clock += 20;
        preview.DragTo(150);
        preview.EndDrag();
        preview.SetAnimation("idle", 0.75);
        Assert.Equal(initial, RenderYaw(0.05f));
        preview.BeginDrag(100);
        clock += 20;
        preview.DragTo(150);
        preview.EndDrag();
        Assert.Equal(initial, RenderYaw(0.05f));
        preview.PlayAnimation("idle");
        Assert.Equal(initial, RenderYaw(0.05f));
        Assert.Equal(0, actor.Pos.Yaw);
    }

    private static void SetField(SetupGuidePreview preview, string name, object value) =>
        typeof(SetupGuidePreview).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(preview, value);
}
