using System.Reflection;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.ServerMods.NoObf;

namespace TheBasics.GuiPreview;

public sealed record SeraphTexture(int Width, int Height, byte[] Rgba);

/// <summary>Real player assets, native attachment tessellation and native skeletal poses without a world or GL context.</summary>
public sealed class SeraphModel
{
    private static readonly MethodInfo SolveByType = typeof(RegistryObjectType).GetMethod("solveByType", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException("Native item variant resolver changed.");
    private readonly Shape? shape;
    private readonly Dictionary<string, AnimationMetaData> animations = new(StringComparer.Ordinal);
    private ClientAnimator? animator;
    private Dictionary<string, AnimationMetaData> active = new(StringComparer.Ordinal);
    private string? poseCode;
    private int poseStep;
    public MeshData Mesh { get; }
    public IReadOnlyDictionary<int, SeraphTexture> Textures { get; }

    public SeraphModel(MeshData mesh, IReadOnlyDictionary<int, SeraphTexture> textures)
    {
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        Textures = textures ?? throw new ArgumentNullException(nameof(textures));
    }

    private SeraphModel(MeshData mesh, IReadOnlyDictionary<int, SeraphTexture> textures, Shape shape,
        Dictionary<string, AnimationMetaData> animations) : this(mesh, textures)
    { this.shape = shape; this.animations = animations; }

    /// <summary>Shares the loaded assets while keeping playback and seek state private to one actor.</summary>
    public SeraphModel NewPoseInstance() => shape == null ? new(Mesh, Textures) : new(Mesh, Textures, shape, animations);

    public static SeraphModel Load(ICoreClientAPI api, string identity)
    {
        ArgumentNullException.ThrowIfNull(api);
        string fixturePath = identity switch
        {
            "Pip" => "thebasics:config/setup-guide.json",
            "Mara" => "thebasics:config/setup-guide-mara.json",
            _ => throw new ArgumentException("Unknown local seraph identity.", nameof(identity))
        };
        var fixture = Json(api, fixturePath);
        var player = Json(api, "game:entities/humanoid/player.json");
        var parts = fixture["skinParts"] as JObject ?? throw new InvalidDataException("Missing seraph skin fixture.");
        string basePath = player["client"]?["shape"]?["base"]?.Value<string>() ?? throw new InvalidDataException("Missing native player shape.");
        var shape = LoadShape(api, basePath);
        var texturePaths = new Dictionary<string, AssetLocation>(StringComparer.Ordinal);
        var skinParts = player["attributes"]?["skinnableParts"] as JArray ?? throw new InvalidDataException("Missing native skin parts.");
        SeraphTexture? skin = null;
        foreach (var definition in skinParts)
        {
            string code = definition["code"]!.Value<string>()!;
            string? selected = parts[code]?.Value<string>();
            if (selected == null) continue;
            var variant = definition["variants"]!.FirstOrDefault(value => value["code"]?.Value<string>() == selected)
                ?? throw new InvalidDataException("Unknown native skin part: " + code + "/" + selected);
            string? type = definition["type"]?.Value<string>();
            if (type == "shape")
            {
                string path = variant["shape"]?["base"]?.Value<string>() ?? definition["shapeTemplate"]?["base"]?.Value<string>()
                    ?? throw new InvalidDataException("Missing native skin-part shape: " + code);
                Attach(api, shape, path.Replace("{code}", selected, StringComparison.Ordinal), "skinpart-", texturePaths);
                shape.RemoveElements(Strings(definition["disableElements"]));
            }
            else if (type == "texture")
            {
                string path = definition["textureTemplate"]?.Value<string>() ?? variant["texture"]?.Value<string>()
                    ?? throw new InvalidDataException("Missing native skin-part texture: " + code);
                path = path.Replace("{code}", selected, StringComparison.Ordinal);
                string? target = definition["textureTarget"]?.Value<string>();
                if (target != null && target != "seraph") texturePaths["skinpart-" + target] = new AssetLocation(path);
                else
                {
                    var layer = ReadTexture(api, new AssetLocation(path));
                    if (code == "baseskin") skin = layer;
                    else
                    {
                        if (skin == null) throw new InvalidDataException("Base skin must precede its overlays.");
                        Overlay(skin, layer, definition["textureRenderTo"]?["x"]?.Value<int>() ?? 0,
                            definition["textureRenderTo"]?["y"]?.Value<int>() ?? 0);
                    }
                }
            }
        }
        if (skin == null) throw new InvalidDataException("Seraph fixture has no base skin.");

        foreach (var garment in fixture["gear"] ?? throw new InvalidDataException("Missing seraph gear fixture."))
        {
            string code = garment["code"]!.Value<string>()!;
            var item = ResolveGarment(api, code);
            shape.RemoveElements(Strings(item["attributes"]?["disableElements"]));
            var composite = item["shape"] ?? throw new InvalidDataException("Missing clothing attachment shape: " + code);
            AttachGarment(api, shape, composite, item["textures"] as JObject, texturePaths);
            if (item["attributes"]?["renderOntoSkin"]?.Value<bool>() == true)
                throw new NotSupportedException("The local seraph fixture needs a supported clothing skin overlay: " + code);
        }
        shape.RemoveElements(Strings(player["attributes"]?["disableElements"]));
        // InitForAnimations caches inverse bind transforms before resolving parents. Raw JSON shapes
        // need their complete parent chains first, as in the native shape asset loading path.
        shape.ResolveReferences(api.Logger, basePath);
        shape.InitForAnimations(api.Logger, basePath);
        ValidateShape(shape);

        var metadata = player["client"]!["animations"]!.ToObject<AnimationMetaData[]>()!
            .Select(value => value.Init()).GroupBy(value => value.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        var allowed = new Dictionary<string, AnimationMetaData>(StringComparer.Ordinal);
        foreach (string code in fixture["animations"]!.Values<string>().Select(code => code ?? throw new InvalidDataException("Empty fixture animation.")))
        {
            if (!metadata.TryGetValue(code, out var animation)) throw new InvalidDataException("Missing native animation: " + code);
            allowed[code] = animation;
        }
        if (!allowed.ContainsKey("idle")) throw new InvalidDataException("Seraph fixture requires idle animation.");
        var required = allowed.Values.Select(value => value.Animation).ToHashSet(StringComparer.Ordinal);
        foreach (var animation in shape.Animations.Where(value => required.Contains(value.Code)))
            animation.GenerateAllFrames(shape.Elements, shape.JointsById);
        if (required.Except(shape.Animations.Select(value => value.Code), StringComparer.Ordinal).Any())
            throw new InvalidDataException("Native shape is missing a fixture animation.");

        var textures = new Dictionary<int, SeraphTexture> { [1] = skin };
        var positions = new Dictionary<string, TextureAtlasPosition>(StringComparer.Ordinal)
        {
            ["seraph"] = Position(1), ["skinpart-seraph"] = Position(1)
        };
        foreach (var texture in texturePaths)
        {
            if (texture.Key is "seraph" or "skinpart-seraph") continue;
            int id = textures.Count + 1;
            textures[id] = ReadTexture(api, texture.Value);
            positions[texture.Key] = Position(id);
        }
        var source = new TextureSource(positions);
        var tesselator = new ShapeTesselator(null!, null!, null!, null!);
        tesselator.TesselateShape(new TesselationMetaData { TexSource = source, WithJointIds = true, UsesColorMap = false, TypeForLogging = "local seraph" },
            shape, out var mesh);
        return new SeraphModel(mesh, textures, shape, allowed);
    }

    public float[] Pose(string code, double time)
    {
        if (!double.IsFinite(time) || time < 0 || time > (int.MaxValue - 1) / 60d) throw new ArgumentOutOfRangeException(nameof(time));
        if (shape == null)
        {
            int count = Math.Max(1, Mesh.CustomInts?.Values.Take(Mesh.VerticesCount).DefaultIfEmpty(0).Max() + 1 ?? 1);
            return Enumerable.Range(0, count).SelectMany(_ => Mat4f.Create()).ToArray();
        }
        if (!animations.TryGetValue(code, out var selected)) throw new ArgumentException("Unknown seraph animation.", nameof(code));
        int target = (int)Math.Floor(time * 60);
        if (animator == null || poseCode != code || target < poseStep)
        {
            animator = new ClientAnimator(() => 1, shape.Animations.Where(value => value.PrevNextKeyFrameByFrame != null).ToArray(), shape.Elements, shape.JointsById);
            poseCode = code; poseStep = 0;
            var idle = animations["idle"].Clone();
            active = new Dictionary<string, AnimationMetaData>(StringComparer.Ordinal) { [idle.Animation] = idle };
            if (code != "idle") { var chosen = selected.Clone(); active[chosen.Animation] = chosen; }
        }
        for (; poseStep < target; poseStep++) animator.OnFrame(active, 1f / 60f);
        return (float[])animator.Matrices.Clone();
    }

    private static JObject ResolveGarment(ICoreClientAPI api, string code)
    {
        var location = new AssetLocation(code);
        string[] segments = location.Path.Split('-', 3);
        if (location.Domain != "game" || segments.Length != 3 || segments[0] != "clothes" ||
            segments[1] is not ("upperbody" or "lowerbody" or "foot" or "emblem" or "waist"))
            throw new InvalidDataException("Unsupported local clothing fixture: " + code);
        var item = Json(api, "game:itemtypes/wearable/seraph/" + segments[1] + ".json");
        if (!(item["variantgroups"]?.Any(group => group["code"]?.Value<string>() == segments[1] &&
            group["states"]?.Values<string>().Contains(segments[2], StringComparer.Ordinal) == true) ?? false))
            throw new InvalidDataException("Unknown native clothing variant: " + code);
        var variants = new Vintagestory.API.Datastructures.OrderedDictionary<string, string> { ["category"] = segments[1], [segments[1]] = segments[2] };
        SolveByType.Invoke(null, [item, location.Path, variants]);
        if (item["attributes"]?["wearableAttachment"]?.Value<bool>() != true || item["attributes"]?["attachableToEntity"] != null)
            throw new NotSupportedException("Unsupported native clothing attachment: " + code);
        return item;
    }

    private static void AttachGarment(ICoreClientAPI api, Shape parent, JToken composite, JObject? overrides, Dictionary<string, AssetLocation> textures)
    {
        string path = composite["base"]?.Value<string>() ?? throw new InvalidDataException("Clothing has no shape path.");
        Attach(api, parent, path, "", textures, overrides);
        if (composite["overlays"] is JArray overlays)
            foreach (var overlay in overlays) AttachGarment(api, parent, overlay, overrides, textures);
    }

    private static void Attach(ICoreClientAPI api, Shape parent, string path, string prefix,
        Dictionary<string, AssetLocation> textures, JObject? overrides = null)
    {
        var child = LoadShape(api, path);
        child.SubclassForStepParenting(prefix);
        child.Textures ??= new Dictionary<string, AssetLocation>();
        if (overrides != null)
            foreach (var texture in overrides.Properties())
            {
                var value = texture.Value;
                if (value.Type != JTokenType.String && value is not JObject { Count: 1 })
                    throw new NotSupportedException("Clothing composite texture needs explicit support: " + texture.Name);
                child.Textures[texture.Name] = new AssetLocation(value.Type == JTokenType.String ? value.Value<string>()! : value["base"]!.Value<string>()!);
            }
        parent.StepParentShape(child, path, "seraph-faceless", api.Logger, (key, location) => textures[prefix + key] = location);
    }

    private static Shape LoadShape(ICoreClientAPI api, string path) =>
        api.Assets.Get(new AssetLocation(path).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
    private static JObject Json(ICoreClientAPI api, string path) => api.Assets.Get(new AssetLocation(path)).ToObject<JObject>();
    private static string[] Strings(JToken? token) => token?.Values<string>().Where(value => value != null).ToArray()! ?? [];
    private static TextureAtlasPosition Position(int id) => new() { atlasTextureId = id, x2 = 1, y2 = 1 };

    private static SeraphTexture ReadTexture(ICoreClientAPI api, AssetLocation location)
    {
        using var bitmap = SKBitmap.Decode(api.Assets.Get(location.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png")).Data)
            ?? throw new InvalidDataException("Could not decode seraph texture: " + location);
        if (bitmap.Width is <= 0 or > 4096 || bitmap.Height is <= 0 or > 4096) throw new InvalidDataException("Seraph texture dimensions are invalid.");
        var bytes = new byte[checked(bitmap.Width * bitmap.Height * 4)];
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y); int at = (y * bitmap.Width + x) * 4;
            bytes[at] = color.Red; bytes[at + 1] = color.Green; bytes[at + 2] = color.Blue; bytes[at + 3] = color.Alpha;
        }
        return new SeraphTexture(bitmap.Width, bitmap.Height, bytes);
    }

    private static void Overlay(SeraphTexture target, SeraphTexture layer, int left, int top)
    {
        if (left < 0 || top < 0 || left >= target.Width || top >= target.Height)
            throw new InvalidDataException("Native skin overlay has an invalid origin.");
        // The native eye overlay starts at x57 in a 64px skin, with an 8px source. Clip its transparent edge.
        for (int y = 0; y < Math.Min(layer.Height, target.Height - top); y++)
        for (int x = 0; x < Math.Min(layer.Width, target.Width - left); x++)
        {
            int source = (y * layer.Width + x) * 4, destination = ((top + y) * target.Width + left + x) * 4;
            double alpha = layer.Rgba[source + 3] / 255d, oldAlpha = target.Rgba[destination + 3] / 255d;
            double combined = alpha + oldAlpha * (1 - alpha);
            for (int channel = 0; channel < 3; channel++)
                target.Rgba[destination + channel] = combined == 0 ? (byte)0 : (byte)Math.Round((layer.Rgba[source + channel] * alpha + target.Rgba[destination + channel] * oldAlpha * (1 - alpha)) / combined);
            target.Rgba[destination + 3] = (byte)Math.Round(combined * 255);
        }
    }

    private static void ValidateShape(Shape shape)
    {
        foreach (var root in shape.Elements)
            root.WalkRecursive(element =>
            {
                if (!string.IsNullOrEmpty(element.ClimateColorMap) || !string.IsNullOrEmpty(element.SeasonColorMap) ||
                    element.From?.Length != 3 || element.To?.Length != 3 || element.From.Concat(element.To).Any(value => !double.IsFinite(value)))
                    throw new InvalidDataException("Unsupported native seraph geometry.");
            });
    }

    private sealed class TextureSource(Dictionary<string, TextureAtlasPosition> positions) : ITexPositionSource
    {
        public TextureAtlasPosition this[string code] => positions.TryGetValue(code, out var position)
            ? position : throw new InvalidDataException("Missing seraph texture: " + code);
        public Size2i AtlasSize => new(1, 1);
    }
}
