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

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupGuidePreviewTests
{
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
        var bounds = ElementBounds.Fixed(0, 0, 120, 168);
        bounds.ParentBounds = ElementBounds.Empty;
        bounds.CalcWorldBounds();
        for (var frame = 0; frame < 300; frame++)
        {
            preview.PlayAnimation("idle");
            preview.Render(0.25f, bounds);
        }
        Assert.Same(animator, actor.AnimManager.Animator);
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
    }
}
