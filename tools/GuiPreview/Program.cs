using System.Globalization;
using System.Text.Json;

namespace TheBasics.GuiPreview;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "help")
            {
                Console.WriteLine("GUI preview: render --game PATH --assets MOD_ASSETS --output DIR [--scenario all|NAME] [--scale 1] [--width 1600] [--height 1000] [--baseline DIR]");
                Console.WriteLine("Image comparison: compare --expected PNG --actual PNG --diff PNG");
                return 0;
            }
            var options = ParseOptions(args.Skip(1).ToArray());
            if (args[0] == "compare")
            {
                EnsureKnown(options, "expected", "actual", "diff");
                var comparison = ImageComparison.Compare(Required(options, "expected"), Required(options, "actual"), Required(options, "diff"));
                Console.WriteLine(JsonSerializer.Serialize(comparison));
                return comparison.Matches ? 0 : 1;
            }
            if (args[0] != "render") throw new ArgumentException("Unknown command: " + args[0]);
            EnsureKnown(options, "game", "assets", "output", "scenario", "scale", "width", "height", "baseline");
            var game = Required(options, "game");
            var assets = Required(options, "assets");
            var output = Path.GetFullPath(Required(options, "output"));
            var width = int.Parse(options.GetValueOrDefault("width", "1600"), CultureInfo.InvariantCulture);
            var height = int.Parse(options.GetValueOrDefault("height", "1000"), CultureInfo.InvariantCulture);
            var scale = double.Parse(options.GetValueOrDefault("scale", "1"), CultureInfo.InvariantCulture);
            if (width is < 64 or > 4096 || height is < 64 or > 4096 || !double.IsFinite(scale) || scale is < 0.5 or > 3)
                throw new ArgumentException("Viewport must be 64..4096 pixels per axis; scale must be 0.5..3.");
            if (options.TryGetValue("baseline", out var baselineDirectory) &&
                string.Equals(Path.TrimEndingDirectorySeparator(output), Path.TrimEndingDirectorySeparator(Path.GetFullPath(baselineDirectory)), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output and baseline directories must be different.");
            Directory.CreateDirectory(output);
            var selected = options.GetValueOrDefault("scenario", "all");
            var names = PreviewScene.Names.Concat(DicePreview.Names).ToArray();
            var scenarios = selected == "all" ? names : new[] { selected };
            var failed = false;
            foreach (var scenario in scenarios)
            {
                if (!names.Contains(scenario, StringComparer.Ordinal)) throw new ArgumentException("Unknown scenario: " + scenario);
                using var host = new PreviewHost(game, assets, width, height, scale);
                try
                {
                    object? fixture = null;
                    using var scene = DicePreview.Names.Contains(scenario) ? null : PreviewScene.Create(scenario, host);
                    if (scene is null) fixture = DicePreview.Render(scenario, host);
                    else host.RenderGuiDialog(scene.Dialog, 0);
                    var imagePath = Path.Combine(output, scenario + ".png");
                    host.Canvas.SavePng(imagePath);
                    File.WriteAllText(Path.Combine(output, scenario + ".json"), JsonSerializer.Serialize(new
                    {
                        scenario,
                        width,
                        height,
                        scale,
                        fixture,
                        fidelity = "Production dialog and widgets; standalone composition has not been calibrated against in-game screenshots.",
                        environment = host.Manifest
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    if (options.TryGetValue("baseline", out var baseline))
                    {
                        var comparison = ImageComparison.Compare(Path.Combine(baseline, scenario + ".png"), imagePath, Path.Combine(output, scenario + ".diff.png"));
                        failed |= !comparison.Matches;
                        Console.WriteLine(scenario + ": " + JsonSerializer.Serialize(comparison));
                    }
                    else Console.WriteLine(imagePath);
                }
                catch (Exception exception)
                {
                    File.WriteAllText(Path.Combine(output, scenario + ".failed.json"), JsonSerializer.Serialize(new
                    { scenario, error = exception.GetBaseException().ToString(), environment = host.Manifest }, new JsonSerializerOptions { WriteIndented = true }));
                    throw;
                }
            }
            return failed ? 1 : 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.GetBaseException());
            return 1;
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args.Length % 2 != 0) throw new ArgumentException("Every option requires a value.");
        for (var index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || !result.TryAdd(args[index][2..], args[index + 1]))
                throw new ArgumentException("Invalid or repeated option: " + args[index]);
        }
        return result;
    }
    private static string Required(Dictionary<string, string> options, string key) => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value : throw new ArgumentException("Missing --" + key);
    private static void EnsureKnown(Dictionary<string, string> options, params string[] keys)
    {
        foreach (var key in options.Keys) if (!keys.Contains(key, StringComparer.Ordinal)) throw new ArgumentException("Unknown option: --" + key);
    }
}
