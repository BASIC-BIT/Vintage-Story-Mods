using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ChatUiSystem;

public static class SetupWizardCaptureIdentity
{
    public static string HashFile(string path) => HashBytes(File.ReadAllBytes(path));
    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static object EnvironmentIdentity(string gamePath, int width, int height, double scale, string locale, string standard, string decorative,
        IAssetManager nativeAssets = null)
    {
        var fontRoot = Path.Combine(gamePath, "assets", "game", "fonts");
        var gameFiles = new[] { Path.Combine(gamePath, "assets", "game", "lang", locale + ".json") }
            .Concat(Directory.Exists(Path.Combine(gamePath, "assets", "game", "textures", "gui"))
                ? Directory.EnumerateFiles(Path.Combine(gamePath, "assets", "game", "textures", "gui"), "*", SearchOption.AllDirectories)
                : Array.Empty<string>());
        object[] FileIdentities(System.Collections.Generic.IEnumerable<string> files) => files.Order(StringComparer.Ordinal).Select(path => (object)new
        { path = Path.GetRelativePath(gamePath, path).Replace('\\', '/'), sha256 = File.Exists(path) ? HashFile(path) : null }).ToArray();
        return new
        {
            gameBinaries = new[] { "VintagestoryAPI.dll", "VintagestoryLib.dll", "Lib/cairo-sharp.dll", "Lib/SkiaSharp.dll" }
                .Select(name => new { name, sha256 = File.Exists(Path.Combine(gamePath, name)) ? HashFile(Path.Combine(gamePath, name)) : null }).ToArray(),
            gameAssets = FileIdentities(gameFiles),
            fonts = new { standard, decorative, platform = RuntimeInformation.OSDescription,
                fontFiles = FileIdentities(Directory.Exists(fontRoot) ? Directory.EnumerateFiles(fontRoot, "*", SearchOption.AllDirectories) : Array.Empty<string>()) },
            locale, viewport = new { width, height }, scale,
            nativeGuide = NativeGuideIdentity(nativeAssets)
        };
    }

    private static object NativeGuideIdentity(IAssetManager assets)
    {
        var prefixes = new[] { "entities/humanoid/player.json", "shapes/entity/humanoid/", "textures/entity/humanoid/",
            "itemtypes/wearable/seraph/", "shaders/entity", "shaderincludes/", "config/setup-guide.json" };
        return new
        {
            rendered = assets != null,
            scope = assets == null ? "Native guide omitted from standalone composition."
                : "Loaded native guide assemblies and effective asset bytes in player, humanoid shape/skin/clothing, seraph wearable, entity shader/include and guide fixture families.",
            loadedAssemblies = assets == null ? Array.Empty<object>() : AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name is "VSSurvivalMod" or "VSEssentials")
                .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
                .Select(assembly => (object)new { name = assembly.GetName().Name, sha256 = HashFile(assembly.Location) }).ToArray(),
            assets = assets == null ? Array.Empty<object>() : prefixes.SelectMany(prefix => assets.GetMany(prefix, null, true))
                .DistinctBy(asset => asset.Location.ToString()).OrderBy(asset => asset.Location.ToString(), StringComparer.Ordinal)
                .Select(asset => (object)new { path = asset.Location.ToString(), sha256 = HashBytes(asset.Data), patched = asset.IsPatched }).ToArray(),
            omissions = assets == null ? new[] { "Native 3D guide is not rendered." }
                : new[] { "Runtime object mutations and code patches are not hashed; assets referenced outside the listed native families are not hashed." }
        };
    }
}
