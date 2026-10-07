using HarmonyLib;
using Newtonsoft.Json.Linq;
using NSubstitute;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TheBasics.GuiPreview;
using thebasics.Tests.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupGuidePreviewTests
{
    private static EntityBehaviorTexturedClothing? suppressedSkin;
    private static int suppressedReloadCalls;

    [Fact]
    public void GuideSkinUsesOriginalNativeMethodWithoutRemovingGlobalSuppression()
    {
        var inventoryType = typeof(SetupGuidePreview).GetNestedType("GuideInventory", BindingFlags.NonPublic)!;
        var inventory = (EntityBehaviorTexturedClothing)Activator.CreateInstance(inventoryType, new EntityPlayerBot())!;
        var original = AccessTools.Method(typeof(EntityBehaviorTexturedClothing), nameof(EntityBehaviorTexturedClothing.reloadSkin));
        var reverseType = typeof(SetupGuidePreview).GetNestedType("NativeSkin", BindingFlags.NonPublic)!;
        var harmony = new Harmony("thebasics.tests.guide-skin-" + Guid.NewGuid().ToString("N"));
        suppressedSkin = inventory;
        suppressedReloadCalls = 0;
        try
        {
            harmony.Patch(original, prefix: new HarmonyMethod(typeof(SetupGuidePreviewTests), nameof(SuppressSkinReload)));
            harmony.CreateClassProcessor(reverseType).Patch();
            inventory.reloadSkin();
            Assert.Equal(1, suppressedReloadCalls);
            reverseType.GetMethod("Reload")!.Invoke(null, [inventory]);
            Assert.Equal(1, suppressedReloadCalls);
            inventory.reloadSkin();
            Assert.Equal(2, suppressedReloadCalls);
            Assert.Contains(Harmony.GetPatchInfo(original)!.Prefixes, patch => patch.owner == harmony.Id);
        }
        finally
        {
            harmony.Unpatch(original, HarmonyPatchType.Prefix, harmony.Id);
            suppressedSkin = null;
        }
    }

    private static bool SuppressSkinReload(EntityBehaviorTexturedClothing __instance)
    {
        if (!ReferenceEquals(__instance, suppressedSkin)) return true;
        suppressedReloadCalls++;
        return false;
    }

    [Fact]
    public void PartiallyInitializedInventoryCanDespawnBeforeItsRendererExists()
    {
        var actor = new Vintagestory.GameContent.EntityPlayerBot();
        var inventoryType = typeof(SetupGuidePreview).GetNestedType("GuideInventory", BindingFlags.NonPublic)!;
        var inventory = (EntityBehavior)Activator.CreateInstance(inventoryType, actor)!;
        inventory.OnEntityDespawn(new EntityDespawnData { Reason = EnumDespawnReason.Removed });
        typeof(Entity).GetProperty(nameof(Entity.Properties))!.SetValue(actor, new EntityProperties
        {
            Client = new EntityClientProperties([], null)
        });
        inventory.OnEntityDespawn(new EntityDespawnData { Reason = EnumDespawnReason.Removed });
    }

    [VisualTheory]
    [InlineData("wizard-restart-dedicated", false, "Restart the server from your hosting panel")]
    [InlineData("wizard-restart-integrated", true, "Save and quit to the main menu")]
    public void WizardRestartCaptureUsesNamedHostInstructions(string scenario, bool connectedHostIsDedicated, string expected)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        using var wizard = new SetupWizardDialog(host.Api, new(SetupWizardCaptureScenes.DefaultValues()), "qa-run",
            connectedHostIsDedicated, null!, null!, null!, layoutOnly: true, captureOnly: true);
        SetupWizardCaptureScenes.Show(wizard, scenario, 0.75);
        var elements = (Dictionary<string, GuiElement>)typeof(GuiComposer)
            .GetField("staticElements", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wizard.SingleComposer)!;
        Assert.Contains(expected, string.Join('\n', elements.Values.OfType<GuiElementStaticText>().Select(element => element.GetText())));
    }

    [VisualTheory]
    [InlineData(1.25)]
    public void SpawnWithoutHomesStillOffersItsSharedGearChoice(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        draft.Set("Teleportation.RegisterHomeCommands", "0");
        draft.Set("Teleportation.RegisterSpawnCommands", "1");
        using var wizard = new SetupWizardDialog(host.Api, draft, "run", true, null!, null!, null!, layoutOnly: true);
        wizard.ShowPage("teleport.spawn");
        Assert.NotNull(wizard.SingleComposer.GetElement("HomeSpawnRequireTemporalGear"));
    }

    [VisualTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestPreviewExplainsOnlyEnabledCostsAndTiming(bool enabled)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues());
        foreach (var key in new[] { "TpaRequireTemporalGear", "TpaUseCooldown", "TpaUseTimeout" })
            draft.Set(key, enabled ? "1" : "0");
        using var wizard = new SetupWizardDialog(host.Api, draft, "run", true, null!, null!, null!, layoutOnly: true);
        wizard.ShowPage("teleport.requests");
        var text = (string)typeof(SetupWizardDialog).GetMethod("BuildPreviewText", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(wizard, null)!;
        Assert.Contains(enabled ? "TPA gear is charged when submitting" : "TPA needs no gear", text);
        Assert.Contains(enabled ? "Cooldown:" : "Cooldown off", text);
        Assert.Contains(enabled ? "Timeout:" : "Timeout off", text);
    }

    [Fact]
    public void LiveAnimationKeepsItsAnimatorBeyondCaptureTimeLimitAndResumesAfterPinnedCapture()
    {
        var api = Substitute.For<ICoreClientAPI>();
        api.Render.ScissorStack.Returns(new Stack<ElementBounds>());
        var actorType = typeof(SetupGuidePreview).GetNestedType("DetachedGuideEntity", BindingFlags.NonPublic)!;
        var actor = (Entity)Activator.CreateInstance(actorType)!;
        typeof(Entity).GetProperty(nameof(Entity.Properties))!.SetValue(actor, new EntityProperties
        {
            Client = new EntityClientProperties([], null)
            {
                AnimationsByMetaCode = new Dictionary<string, AnimationMetaData>
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
        for (var frame = 0; frame < 300; frame++)
        {
            preview.PlayAnimation("idle");
            preview.Render(0.25f, bounds);
        }
        Assert.Same(animator, actor.AnimManager.Animator);
        api.Render.Received().RenderEntityToGui(Arg.Is(0f), actor, Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(),
            Arg.Is(-1.2707963f), Arg.Any<float>(), -1);
        Assert.True((double)GetField(preview, "previewTime")! > 60);
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetAnimation("idle", 61));

        preview.SetAnimation("idle", 0.75);
        var pinnedAnimator = actor.AnimManager.Animator;
        preview.Render(0.25f, bounds);
        Assert.Equal(0.75, GetField(preview, "previewTime"));
        preview.PlayAnimation("idle");
        Assert.NotSame(pinnedAnimator, actor.AnimManager.Animator);
        var resumedAnimator = actor.AnimManager.Animator;
        preview.PlayAnimation("idle");
        Assert.Same(resumedAnimator, actor.AnimManager.Animator);
        Assert.Throws<ArgumentException>(() => preview.PlayAnimation("unknown"));
    }

    [Fact]
    public void NamedWaveFramesEaseInAndChangeNativeJointMatricesWithIdleAlias()
    {
        static AnimationKeyFrame KeyFrame(int frame, double rotation) => new()
        {
            Frame = frame,
            Elements = new() { ["UpperArmR"] = new() { RotationX = 0, RotationY = 0, RotationZ = rotation } }
        };
        var shape = new Shape
        {
            Elements = [new() { Name = "UpperArmR", From = [0, 0, 0], To = [1, 4, 1], RotationOrigin = [0, 4, 0] }],
            Animations =
            [
                new() { Code = "idle1", QuantityFrames = 80, KeyFrames = [KeyFrame(0, 0), KeyFrame(79, 0)] },
                new()
                {
                    Code = "wave", QuantityFrames = 80, OnActivityStopped = EnumEntityActivityStoppedHandling.EaseOut,
                    OnAnimationEnd = EnumEntityAnimationEndHandling.EaseOut,
                    KeyFrames = [KeyFrame(0, 0), KeyFrame(30, 90), KeyFrame(60, -90), KeyFrame(79, 0)]
                }
            ]
        };
        shape.InitForAnimations(Substitute.For<ILogger>(), "setup-guide-test");
        var actorType = typeof(SetupGuidePreview).GetNestedType("DetachedGuideEntity", BindingFlags.NonPublic)!;
        var actor = (Entity)Activator.CreateInstance(actorType)!;
        typeof(Entity).GetProperty(nameof(Entity.Properties))!.SetValue(actor, new EntityProperties
        {
            Client = new EntityClientProperties([], null)
            {
                AnimationsByMetaCode = new()
                {
                    ["idle"] = new() { Code = "idle", Animation = "idle1" },
                    ["wave"] = new() { Code = "wave", Animation = "wave", AnimationSpeed = 1.3f, EaseInSpeed = 1 }
                }
            }
        });
        actor.AnimManager.Animator = new ClientAnimator(() => 1, shape.Animations, shape.Elements, shape.JointsById);
        var preview = (SetupGuidePreview)RuntimeHelpers.GetUninitializedObject(typeof(SetupGuidePreview));
        var fixtureField = typeof(SetupGuidePreview).GetField("fixture", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fixture = Activator.CreateInstance(fixtureField.FieldType)!;
        fixtureField.FieldType.GetProperty("Animations")!.SetValue(fixture, new[] { "idle", "wave" });
        fixtureField.SetValue(preview, fixture);
        SetField(preview, "actor", actor);

        preview.SetAnimation("wave", 0);
        var identity = actor.AnimManager.Animator.Matrices.ToArray();
        Assert.Equal(new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }, identity.Skip(16).Take(16));
        Assert.Equal(0, actor.AnimManager.Animator.GetAnimationState("wave").Iterations);

        preview.SetAnimation("wave", 0.75);
        var animator = actor.AnimManager.Animator;
        var firstFrame = animator.Matrices.ToArray();
        Assert.Contains("idle1", actor.AnimManager.ActiveAnimationsByAnimCode.Keys);
        Assert.True(animator.GetAnimationState("idle1").Active);
        Assert.InRange(animator.GetAnimationState("wave").EasingFactor, 0.5f, 1f);
        Assert.InRange(animator.GetAnimationState("wave").CurrentFrame, 29.2f, 29.3f);
        Assert.False(identity.SequenceEqual(firstFrame));

        preview.SetAnimation("wave", 1.5);
        Assert.InRange(actor.AnimManager.Animator.GetAnimationState("wave").CurrentFrame, 58.4f, 58.6f);
        Assert.False(firstFrame.SequenceEqual(actor.AnimManager.Animator.Matrices));
    }

    [VisualTheory]
    [InlineData(5.5)]
    public void InstalledNativeGreetingReturnsToMovingIdleAfterCaptureResumes(double liveSeconds)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var shape = host.Api.Assets.Get(new AssetLocation("game:shapes/entity/humanoid/seraph-faceless.json")).ToObject<Shape>();
        shape.InitForAnimations(host.Api.Logger, "setup-guide-native-test");
        var player = JObject.Parse(host.Api.Assets.Get(new AssetLocation("game:entities/humanoid/player.json")).ToText());
        var metadata = player["client"]!["animations"]!
            .Where(token => token.Value<string>("code") is "idle" or "wave")
            .Select(token => token.ToObject<AnimationMetaData>()!.Init())
            .ToDictionary(animation => animation.Code);
        var api = Substitute.For<ICoreClientAPI>();
        api.Render.ScissorStack.Returns(new Stack<ElementBounds>());
        var actorType = typeof(SetupGuidePreview).GetNestedType("DetachedGuideEntity", BindingFlags.NonPublic)!;
        var actor = (Entity)Activator.CreateInstance(actorType)!;
        typeof(Entity).GetProperty(nameof(Entity.Properties))!.SetValue(actor, new EntityProperties
        {
            Client = new EntityClientProperties([], null) { AnimationsByMetaCode = metadata }
        });
        actor.AnimManager.Animator = new ClientAnimator(() => 1, shape.Animations, shape.Elements, shape.JointsById);
        var preview = (SetupGuidePreview)RuntimeHelpers.GetUninitializedObject(typeof(SetupGuidePreview));
        var fixtureField = typeof(SetupGuidePreview).GetField("fixture", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fixtureJson = JObject.Parse(host.Api.Assets.Get(new AssetLocation("thebasics:config/setup-guide.json")).ToText());
        fixtureField.SetValue(preview, fixtureJson.ToObject(fixtureField.FieldType)!);
        SetField(preview, "api", api);
        SetField(preview, "actor", actor);
        SetField(preview, "animationCode", "idle");
        var bounds = ElementBounds.Fixed(0, 0, 120, 168);
        bounds.ParentBounds = ElementBounds.Empty;
        bounds.CalcWorldBounds();

        preview.SetAnimation("wave", 0.75);
        var pinnedAnimator = actor.AnimManager.Animator;
        var pinnedMatrices = pinnedAnimator.Matrices.ToArray();
        preview.Render(0.25f, bounds);
        Assert.Same(pinnedAnimator, actor.AnimManager.Animator);
        Assert.True(pinnedMatrices.SequenceEqual(pinnedAnimator.Matrices));
        Assert.Equal(0.75, GetField(preview, "previewTime"));

        // ResumePreview uses this same transition from a named frame into live native animation.
        preview.PlayAnimation("wave");
        var liveAnimator = actor.AnimManager.Animator;
        Assert.NotSame(pinnedAnimator, liveAnimator);
        for (int frame = 0; frame < Math.Ceiling(liveSeconds * 60); frame++) preview.Render(1f / 60, bounds);
        Assert.Same(liveAnimator, actor.AnimManager.Animator);
        Assert.DoesNotContain("wave", actor.AnimManager.ActiveAnimationsByAnimCode.Keys);
        Assert.False(liveAnimator.GetAnimationState("wave").Running);
        Assert.Contains("idle1", actor.AnimManager.ActiveAnimationsByAnimCode.Keys);
        Assert.True(liveAnimator.GetAnimationState("idle1").Running);
        Assert.True(liveAnimator.GetAnimationState("idle1").Iterations >= 2);
        Assert.True((double)GetField(preview, "previewTime")! > 5);

        var idleMatrices = liveAnimator.Matrices.ToArray();
        float idleFrame = liveAnimator.GetAnimationState("idle1").CurrentFrame;
        for (int frame = 0; frame < 15; frame++) preview.Render(1f / 60, bounds);
        Assert.NotEqual(idleFrame, liveAnimator.GetAnimationState("idle1").CurrentFrame);
        Assert.False(idleMatrices.SequenceEqual(liveAnimator.Matrices));
    }

    [VisualTheory]
    [InlineData(1600, 1000, 1.25)]
    [InlineData(1280, 720, 1.25)]
    public void WizardPagesRenderWithControlsInsideViewport(int width, int height, double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, width, height, scale);
        foreach (var scenario in SetupWizardCaptureScenes.Names)
        {
            using var scene = PreviewScene.Create(scenario, host);
            host.RenderGuiDialog(scene.Dialog, 0);
            Assert.Empty(host.UnsupportedCalls);
            var manifest = JsonSerializer.SerializeToElement(host.Manifest);
            foreach (var control in manifest.GetProperty("controls").EnumerateArray())
            {
                var x = control.GetProperty("x").GetDouble();
                var y = control.GetProperty("y").GetDouble();
                Assert.InRange(x, 0, width);
                Assert.InRange(y, 0, height);
                Assert.InRange(x + control.GetProperty("width").GetDouble(), 0, width);
                Assert.InRange(y + control.GetProperty("height").GetDouble(), 0, height);
            }
        }
        using var rangesScene = PreviewScene.Create("wizard-chat-ranges", host);
        var wizard = (SetupWizardDialog)rangesScene.Dialog;
        host.RenderGuiDialog(wizard, 0);
        var previousFrame = host.Canvas.GetRgbaPixels();
        foreach (var mode in new[] { "Whisper", "Yell" })
        {
            wizard.Draft.Set("ProximityChatModeDistances." + mode, "-1");
            wizard.ShowPage("chat.ranges");
            wizard.SetPreviewTime(0.75);
            host.RenderGuiDialog(wizard, 0);
            var currentFrame = host.Canvas.GetRgbaPixels();
            Assert.False(previousFrame.SequenceEqual(currentFrame));
            previousFrame = currentFrame;
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    private static void SetField(SetupGuidePreview preview, string name, object value) =>
        typeof(SetupGuidePreview).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(preview, value);
    private static object? GetField(SetupGuidePreview preview, string name) =>
        typeof(SetupGuidePreview).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(preview);

    [Fact]
    public void DetachedPropertiesKeepSourceAttributesAndTexturesUntouched()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var attributes = new JsonObject(JObject.Parse("{value: 1}"));
        var behavior = new JsonObject(JObject.Parse("{code: 'extraskinnable', options: {value: 1}}"));
        var texture = new CompositeTexture(new AssetLocation("game:entity/humanoid/seraph-naked-hairless"));
        texture.Bake(api.Assets);
        texture.Baked.TextureSubId = 41;
        var source = new EntityProperties
        {
            Code = new AssetLocation("game:player"),
            Attributes = attributes,
            Client = new EntityClientProperties([behavior, new JsonObject(JObject.Parse("{code: 'playerinventory'}"))], null)
            {
                Textures = new Dictionary<string, CompositeTexture> { ["seraph"] = texture },
                Shape = new CompositeShape { Base = new AssetLocation("game:entity/humanoid/seraph-faceless") }
            }
        };

        var copy = SetupGuidePreview.CopyProperties(source, api);
        Assert.Same(attributes, source.Attributes);
        Assert.NotSame(source.Attributes.Token, copy.Attributes.Token);
        Assert.NotSame(source.Client.Textures["seraph"], copy.Client.Textures["seraph"]);
        Assert.NotSame(texture.Baked, copy.Client.Textures["seraph"].Baked);
        Assert.Equal(41, copy.Client.Textures["seraph"].Baked.TextureSubId);
        Assert.Single(copy.Client.BehaviorsAsJsonObj);
        Assert.NotSame(source.Client.BehaviorsAsJsonObj[0].Token, copy.Client.BehaviorsAsJsonObj[0].Token);

        copy.Attributes.Token["value"] = 2;
        copy.Client.BehaviorsAsJsonObj[0].Token["options"]["value"] = 2;
        copy.Client.Textures["seraph"].Baked.TextureSubId = 99;
        Assert.Equal(1, source.Attributes["value"].AsInt());
        Assert.Equal(1, source.Client.BehaviorsAsJsonObj[0]["options"]["value"].AsInt());
        Assert.Equal(41, texture.Baked.TextureSubId);

        source.Client.BehaviorsAsJsonObj[0].Token["code"] = "PlayerModelLib:ExtraSkinnable";
        var moddedCopy = SetupGuidePreview.CopyProperties(source, api);
        Assert.Single(moddedCopy.Client.BehaviorsAsJsonObj);
        Assert.Equal("extraskinnable", moddedCopy.Client.BehaviorsAsJsonObj[0]["code"].AsString());
        Assert.Equal("PlayerModelLib:ExtraSkinnable", source.Client.BehaviorsAsJsonObj[0]["code"].AsString());
    }
}
