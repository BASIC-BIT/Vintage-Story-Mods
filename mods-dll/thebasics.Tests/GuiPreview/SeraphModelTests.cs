using TheBasics.GuiPreview;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class SeraphModelTests
{
    [VisualTheory]
    [InlineData("Pip")]
    [InlineData("Mara")]
    public void LoadsRealSkinnedClothedJointedMeshWithoutWorldCalls(string identity)
    {
        using var host = Host();
        var model = SeraphModel.Load(host.Api, identity);
        Assert.True(model.Mesh.VerticesCount > 280);
        Assert.Equal(model.Mesh.VerticesCount, model.Mesh.CustomInts.Count);
        Assert.True(model.Textures.Count >= 3);
        Assert.All(model.Textures.Values, texture => Assert.Equal(texture.Width * texture.Height * 4, texture.Rgba.Length));
        Assert.Contains(host.AssetFiles, path => path.EndsWith("seraph-faceless.json", StringComparison.Ordinal));
        Assert.Contains(host.AssetFiles, path => path.Contains("seraphskinparts", StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.Ordinal));
        Assert.Contains(host.AssetFiles, path => path.Contains("seraphclothes", StringComparison.Ordinal));
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
        Assert.DoesNotContain(host.UsedCalls, call => call.StartsWith("World.", StringComparison.Ordinal));
    }

    [VisualTheory]
    [InlineData("Pip")]
    public void NativePosesAreDeterministicSeekableAndKeepClothingOnTheSameJoints(string identity)
    {
        using var host = Host();
        var model = SeraphModel.Load(host.Api, identity);
        var wave = model.Pose("wave", .5);
        Assert.Equal(wave, model.Pose("wave", .5));
        var idle = model.Pose("idle", .5);
        Assert.False(wave.SequenceEqual(idle));
        Assert.Equal(wave, model.Pose("wave", .5));
        Assert.False(idle.SequenceEqual(model.Pose("sleep", 2)));
        Assert.All(model.Mesh.CustomInts.Values.Take(model.Mesh.VerticesCount), joint => Assert.InRange(joint, 0, wave.Length / 16 - 1));
        Assert.All(wave, value => Assert.True(float.IsFinite(value)));
        Assert.Throws<ArgumentException>(() => model.Pose("unavailable", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Pose("idle", double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Pose("idle", -1));
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    [VisualTheory]
    [InlineData("UpperTorso", "LowerTorso")]
    [InlineData("LowerArmR", "UpperArmR")]
    public void NativeInverseBindCancelsTheCompleteBaseSkeletonParentChain(string elementName, string parentName)
    {
        using var host = Host();
        var model = SeraphModel.Load(host.Api, "Pip");
        var shape = (Shape)typeof(SeraphModel).GetField("shape", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model)!;
        var element = shape.GetElementByName(elementName);
        Assert.Equal(parentName, element.ParentElement!.Name);
        Assert.True(element.CountParents() >= 2);

        var bind = Mat4f.Create();
        foreach (var ancestor in element.GetParentPath().Append(element))
            Mat4f.Mul(bind, bind, ancestor.GetLocalTransformMatrix(0, Mat4f.Create()));
        Mat4f.Mul(bind, bind, element.inverseModelTransform!);

        var identity = Mat4f.Create();
        for (var index = 0; index < identity.Length; index++)
            Assert.InRange(bind[index], identity[index] - 0.00001f, identity[index] + 0.00001f);
    }

    [VisualTheory]
    [InlineData("Pip")]
    public void SameIdentityActorsShareAssetsAndKeepIndependentInterleavedPlayback(string identity)
    {
        using var host = Host();
        using var waveGuide = host.CreateGuide(host.Api, identity);
        using var idleGuide = host.CreateGuide(host.Api, identity);
        var modelField = typeof(SoftwareSeraphPreview).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(SeraphModel));
        var wave = (SeraphModel)modelField.GetValue(waveGuide)!;
        var idle = (SeraphModel)modelField.GetValue(idleGuide)!;
        var cache = (Dictionary<string, SeraphModel>)typeof(PreviewHost)
            .GetField("seraphModels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        var loaded = Assert.Single(cache).Value;
        Assert.NotSame(loaded, wave);
        Assert.NotSame(loaded, idle);
        Assert.NotSame(wave, idle);
        Assert.Same(loaded.Mesh, wave.Mesh);
        Assert.Same(loaded.Mesh, idle.Mesh);
        Assert.Same(loaded.Textures, wave.Textures);
        Assert.Same(loaded.Textures, idle.Textures);

        var animatorField = typeof(SeraphModel).GetField("animator", BindingFlags.Instance | BindingFlags.NonPublic)!;
        wave.Pose("wave", 0.5);
        var waveAnimator = animatorField.GetValue(wave);
        idle.Pose("idle", 0.75);
        var idleAnimator = animatorField.GetValue(idle);
        var laterWave = wave.Pose("wave", 1);
        var laterIdle = idle.Pose("idle", 1);

        Assert.NotSame(waveAnimator, idleAnimator);
        Assert.Same(waveAnimator, animatorField.GetValue(wave));
        Assert.Same(idleAnimator, animatorField.GetValue(idle));
        Assert.Equal(loaded.Pose("wave", 1), laterWave);
        Assert.Equal(loaded.Pose("idle", 1), laterIdle);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    [VisualTheory]
    [InlineData("Pip", "Mara")]
    public void SampleIdentitiesUseDifferentActualSkinAndOutfitAssets(string first, string second)
    {
        using var host = Host();
        var pip = SeraphModel.Load(host.Api, first);
        var mara = SeraphModel.Load(host.Api, second);
        Assert.False(pip.Textures[1].Rgba.SequenceEqual(mara.Textures[1].Rgba));
        Assert.Contains(host.AssetFiles, path => path.EndsWith("pastoral-shirt.png", StringComparison.Ordinal));
        Assert.Contains(host.AssetFiles, path => path.EndsWith("hunters-green.png", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => SeraphModel.Load(host.Api, "Unknown"));
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    private static PreviewHost Host() => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, 1);
}
