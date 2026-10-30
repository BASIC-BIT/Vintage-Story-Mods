using NSubstitute;
using System.Security.Cryptography;
using System.Text.Json;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardCaptureIdentityTests
{
    [Fact]
    public void NativeIdentityHashesEffectivePatchedAssetsWhileCpuExplicitlyOmitsGuide()
    {
        var missingGamePath = Path.Combine(Path.GetTempPath(), "guide-identity-" + Guid.NewGuid().ToString("N"));
        var assets = Substitute.For<IAssetManager>();
        var asset = Substitute.For<IAsset>();
        asset.Location.Returns(new AssetLocation("game:shapes/entity/humanoid/seraph/clothing/foot/commoner-boots.json"));
        asset.Data.Returns(new byte[] { 1, 2, 3 });
        asset.IsPatched.Returns(true);
        assets.GetMany(Arg.Any<string>(), null, true).Returns(new List<IAsset> { asset });
        JsonElement Identity(IAssetManager? manager = null) => JsonSerializer.SerializeToElement(
            SetupWizardCaptureIdentity.EnvironmentIdentity(missingGamePath, 1600, 1000, 1, "en", "sans-serif", "Lora", manager))
            .GetProperty("nativeGuide");

        var cpu = Identity();
        Assert.False(cpu.GetProperty("rendered").GetBoolean());
        Assert.Empty(cpu.GetProperty("assets").EnumerateArray());
        Assert.NotEmpty(cpu.GetProperty("omissions").EnumerateArray());
        var native = Identity(assets);
        Assert.True(native.GetProperty("rendered").GetBoolean());
        var captured = Assert.Single(native.GetProperty("assets").EnumerateArray());
        Assert.Equal(asset.Location.ToString(), captured.GetProperty("path").GetString());
        Assert.True(captured.GetProperty("patched").GetBoolean());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(asset.Data)).ToLowerInvariant(), captured.GetProperty("sha256").GetString());
        asset.Data.Returns(new byte[] { 4, 5, 6 });
        Assert.NotEqual(captured.GetProperty("sha256").GetString(),
            Assert.Single(Identity(assets).GetProperty("assets").EnumerateArray()).GetProperty("sha256").GetString());
    }
}
