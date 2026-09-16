using System.Reflection;
using TheBasics.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Xunit;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class PreviewHostTests
{
    [VisualTheory]
    [InlineData(1.25)]
    public void Constructor_failure_after_localization_load_restores_process_globals(double scale)
    {
        var before = GlobalSnapshot.Capture();
        var missingAssets = Path.Combine(Path.GetTempPath(), "missing-gui-assets-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => new PreviewHost(GamePath, missingAssets, 1600, 1000, scale));

        before.AssertRestored();
    }

    [VisualTheory]
    [InlineData(1.25)]
    public void Unsupported_api_access_throws_and_records_the_exact_member(double scale)
    {
        var before = GlobalSnapshot.Capture();
        using (var host = new PreviewHost(GamePath, ModAssetsPath, 1600, 1000, scale))
        {
            var error = Assert.Throws<NotSupportedException>(() => { _ = host.Api.Network; });

            var unsupported = Assert.Single(host.UnsupportedCalls);
            Assert.Contains("get_Network", unsupported);
            Assert.Contains(unsupported, error.Message);
            Assert.Contains(host.UsedCalls, call => call.Contains("get_Network", StringComparison.Ordinal));
        }
        before.AssertRestored();
    }

    [VisualTheory]
    [InlineData("language-default")]
    public void Separate_hosts_render_identical_pixels_and_reload_their_own_assets(string scenario)
    {
        var before = GlobalSnapshot.Capture();
        var first = Render(scenario);
        before.AssertRestored();
        var second = Render(scenario);
        before.AssertRestored();

        Assert.Equal(first.Pixels, second.Pixels);
        Assert.Equal(first.Assets, second.Assets);
        Assert.Contains(first.Assets, path => path.Contains("textures", StringComparison.Ordinal));
        Assert.Equal(first.Calls, second.Calls);
    }

    private static (byte[] Pixels, string[] Assets, string[] Calls) Render(string scenario)
    {
        using var host = new PreviewHost(GamePath, ModAssetsPath, 1600, 1000, 1);
        using var scene = PreviewScene.Create(scenario, host);
        host.SetClock(0);
        host.RenderGuiDialog(scene.Dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
        return (host.Canvas.GetRgbaPixels(), host.AssetFiles.ToArray(), host.UsedCalls.ToArray());
    }

    private static string GamePath => Environment.GetEnvironmentVariable("VINTAGE_STORY")
        ?? throw new InvalidOperationException("VINTAGE_STORY is required for full GUI tests.");

    private static string ModAssetsPath => Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")
        ?? throw new InvalidOperationException("THEBASICS_GUI_ASSETS is required for full GUI tests.");

    private sealed record GlobalSnapshot(
        float Scale,
        string? SearchPath,
        string? Locale,
        string StandardFont,
        string DecorativeFont,
        Dictionary<string, ITranslationService> Languages,
        object PatternCache)
    {
        private static readonly FieldInfo CacheField = typeof(GuiElement).GetField("cachedPatterns", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("The game GUI pattern cache binding changed.");

        public static GlobalSnapshot Capture() => new(
            RuntimeEnv.GUIScale,
            Environment.GetEnvironmentVariable("PATH"),
            Lang.CurrentLocale,
            GuiStyle.StandardFontName,
            GuiStyle.DecorativeFontName,
            new Dictionary<string, ITranslationService>(Lang.AvailableLanguages),
            CacheField.GetValue(null)!);

        public void AssertRestored()
        {
            Assert.Equal(Scale, RuntimeEnv.GUIScale);
            Assert.Equal(SearchPath, Environment.GetEnvironmentVariable("PATH"));
            Assert.Equal(Locale, Lang.CurrentLocale);
            Assert.Equal(StandardFont, GuiStyle.StandardFontName);
            Assert.Equal(DecorativeFont, GuiStyle.DecorativeFontName);
            Assert.Equal(Languages.Keys.OrderBy(key => key), Lang.AvailableLanguages.Keys.OrderBy(key => key));
            foreach (var pair in Languages) Assert.Same(pair.Value, Lang.AvailableLanguages[pair.Key]);
            Assert.Same(PatternCache, CacheField.GetValue(null));
        }
    }
}
