using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace thebasics.ModSystems.ChatUiSystem;

public static class SetupWizardCaptureIdentity
{
    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    public static object EnvironmentIdentity(string gamePath, int width, int height, double scale, string locale, string standard, string decorative)
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
            locale, viewport = new { width, height }, scale
        };
    }
}
