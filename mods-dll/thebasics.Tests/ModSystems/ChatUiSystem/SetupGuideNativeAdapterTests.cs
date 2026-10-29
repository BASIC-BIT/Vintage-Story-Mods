using NSubstitute;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupGuideNativeAdapterTests
{
    [Fact]
    public void SleepPoseFitUsesJointTransformedBoundsAndTheNativeGuiTransform()
    {
        var mesh = new MeshData
        {
            VerticesCount = 4,
            xyz = [0.25f, 0.1f, -0.5f, 0.75f, 0.1f, -0.5f, 0.25f, 0.6f, 1.5f, 0.75f, 0.6f, 1.5f],
            CustomInts = new CustomMeshDataPartInt { Values = [0, 0, 0, 0], Count = 4 }
        };
        var joints = Mat4f.Create();
        joints[12] = 3;
        joints[13] = 2;
        joints[14] = -1;
        var yaw = MathF.PI / 2;

        Assert.True(SetupGuidePreview.TryGetPoseBounds(mesh, joints, yaw, out var pose));
        Assert.Equal(0, pose.MinX, 5);
        Assert.Equal(2, pose.MaxX, 5);
        Assert.Equal(-2.6, pose.MinY, 5);
        Assert.Equal(-2.1, pose.MaxY, 5);
        var fit = SetupGuidePreview.FitPoseBounds(pose, 108, 35, 2);
        var pixelScale = fit.Size * 2;
        var left = fit.X + fit.Size + pixelScale * pose.MinX;
        var right = fit.X + fit.Size + pixelScale * pose.MaxX;
        var top = fit.Y + 2 * fit.Size + pixelScale * pose.MinY;
        var bottom = fit.Y + 2 * fit.Size + pixelScale * pose.MaxY;
        Assert.Equal(108 * 0.9, right - left, 4);
        Assert.Equal(54, (left + right) / 2, 4);
        Assert.Equal(17.5, (top + bottom) / 2, 4);
        Assert.InRange(top, 0, 35);
        Assert.InRange(bottom, 0, 35);
        Assert.False(SetupGuidePreview.TryGetPoseBounds(null!, joints, yaw, out _));
    }

    [Theory]
    [InlineData("Pip", "config/setup-guide.json")]
    [InlineData("Mara", "config/setup-guide-mara.json")]
    public void IdentitySelectsItsOwnFixtureBeforeCreatingAnActor(string identity, string path)
    {
        var api = Substitute.For<ICoreClientAPI>();
        var asset = new Asset(Encoding.UTF8.GetBytes("{}"), new AssetLocation("thebasics:" + path), null!);
        api.Assets.Get(Arg.Any<AssetLocation>()).Returns(asset);

        Assert.Throws<InvalidOperationException>(() => new SetupGuidePreview(api, identity));

        api.Assets.Received(1).Get(Arg.Is<AssetLocation>(location => location.Domain == "thebasics" && location.Path == path));
        api.World.DidNotReceive().GetEntityType(Arg.Any<AssetLocation>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public void UnknownIdentityCannotLoadAssetsOrCreateAnActor(string? identity)
    {
        var api = Substitute.For<ICoreClientAPI>();

        Assert.Throws<ArgumentException>(() => new SetupGuidePreview(api, identity!));

        api.Assets.DidNotReceive().Get(Arg.Any<AssetLocation>());
        api.World.DidNotReceive().GetEntityType(Arg.Any<AssetLocation>());
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0.4f, 0.4f)]
    [InlineData(1.5f, 1f)]
    public void NativeDrawClampsOpacityAddsYawAndRestoresWhiteAndModelMatrix(float opacity, float expectedOpacity)
    {
        var (preview, api, actor, renderer) = CreatePreview();
        var shader = Substitute.For<IShaderProgram>();
        api.Render.CurrentActiveShader.Returns(shader);
        var alphas = new List<float>();
        shader.When(value => value.Uniform("rgbaIn", 1f, 1f, 1f, Arg.Any<float>()))
            .Do(call => alphas.Add(call.ArgAt<float>(4)));
        api.Render.When(render => render.RenderEntityToGui(Arg.Any<float>(), actor, Arg.Any<double>(), Arg.Any<double>(),
            Arg.Any<double>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<int>()))
            .Do(call => renderer.RenderToGui(call.ArgAt<float>(0), call.ArgAt<double>(2), call.ArgAt<double>(3),
                call.ArgAt<double>(4), call.ArgAt<float>(5), call.ArgAt<float>(6)));
        var originalMatrix = renderer.ModelMat.ToArray();

        preview.Render(0, Bounds(), opacity, 0.7f);

        Assert.Equal(new[] { expectedOpacity, 1f }, alphas);
        Assert.Equal(originalMatrix, renderer.ModelMat);
        shader.Received().Uniform("applyAnimation", 0);
        api.Render.Received().RenderEntityToGui(Arg.Is<float>(dt => dt == 0), actor, Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
            Arg.Is<float>(yaw => Math.Abs(yaw - (-1.2707963f + 0.7f)) < 0.0001f), Arg.Any<float>(), -1);
        Assert.Equal(0, actor.Pos.Yaw);
    }

    [Fact]
    public void NativeDrawFailureStillRestoresWhiteAndModelMatrix()
    {
        var (_, api, _, renderer) = CreatePreview();
        var shader = Substitute.For<IShaderProgram>();
        api.Render.CurrentActiveShader.Returns(shader);
        renderer.GetType().GetProperty("Opacity")!.SetValue(renderer, 0.4f);
        var originalMatrix = renderer.ModelMat.ToArray();
        shader.When(value => value.Uniform("rgbaIn", 1f, 1f, 1f, 0.4f))
            .Do(_ => throw new InvalidOperationException("Native draw failed."));

        Assert.Throws<InvalidOperationException>(() => renderer.RenderToGui(0, 0, 0, 0, 0, 40));

        shader.Received().Uniform("rgbaIn", 1f, 1f, 1f, 1f);
        shader.Received().Uniform("applyAnimation", 0);
        Assert.Equal(originalMatrix, renderer.ModelMat);
    }

    [Theory]
    [InlineData(float.NaN, 0f, "opacity")]
    [InlineData(float.PositiveInfinity, 0f, "opacity")]
    [InlineData(1f, float.NaN, "yawOffset")]
    [InlineData(1f, float.NegativeInfinity, "yawOffset")]
    public void NonFiniteRenderParametersCannotReachTheRenderer(float opacity, float yawOffset, string parameter)
    {
        var (preview, api, _, _) = CreatePreview();

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => preview.Render(0, Bounds(), opacity, yawOffset));

        Assert.Equal(parameter, error.ParamName);
        api.Render.DidNotReceive().GlPushMatrix();
    }

    private static (SetupGuidePreview Preview, ICoreClientAPI Api, Entity Actor, EntityShapeRenderer Renderer) CreatePreview()
    {
        var api = Substitute.For<ICoreClientAPI>();
        api.Render.ScissorStack.Returns(new Stack<ElementBounds>());
        var actorType = typeof(SetupGuidePreview).GetNestedType("DetachedGuideEntity", BindingFlags.NonPublic)!;
        var actor = (Entity)Activator.CreateInstance(actorType)!;
        typeof(Entity).GetProperty(nameof(Entity.Properties))!.SetValue(actor, new EntityProperties
        {
            Client = new EntityClientProperties([], null) { Size = 1 }
        });
        typeof(Entity).GetField("shapeFresh", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(actor, true);
        var rendererType = typeof(SetupGuidePreview).GetNestedType("GuideRenderer", BindingFlags.NonPublic)!;
        var renderer = (EntityShapeRenderer)RuntimeHelpers.GetUninitializedObject(rendererType);
        renderer.capi = api;
        renderer.entity = actor;
        renderer.ModelMat = Mat4f.Create();
        var preview = (SetupGuidePreview)RuntimeHelpers.GetUninitializedObject(typeof(SetupGuidePreview));
        SetField(preview, "api", api);
        SetField(preview, "actor", actor);
        SetField(preview, "renderer", renderer);
        SetField(preview, "rotationYaw", -1.2707963f);
        return (preview, api, actor, renderer);
    }

    private static ElementBounds Bounds()
    {
        var bounds = ElementBounds.Fixed(0, 0, 120, 168);
        bounds.ParentBounds = ElementBounds.Empty;
        bounds.CalcWorldBounds();
        return bounds;
    }

    private static void SetField(SetupGuidePreview preview, string name, object value) =>
        typeof(SetupGuidePreview).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(preview, value);
}
