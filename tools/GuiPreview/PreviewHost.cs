using System.Reflection;
using Path = System.IO.Path;
using System.Runtime.InteropServices;
using Cairo;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace TheBasics.GuiPreview;

/// <summary>Bounded standalone environment for the original compiled game GUI.</summary>
public sealed class PreviewHost : IDisposable
{
    public ICoreClientAPI Api { get; }
    public SoftwareCanvas Canvas { get; }
    public SortedSet<string> UsedCalls { get; } = new(StringComparer.Ordinal);
    public List<string> UnsupportedCalls { get; } = [];
    public SortedSet<string> AssetFiles { get; } = new(StringComparer.Ordinal);
    public List<string> RecordedEffects { get; } = [];
    private readonly string gamePath, modAssetsPath;
    private readonly ElementBounds window;
    private readonly Stack<ElementBounds> scissor = new();
    private readonly List<GuiDialog> loaded = [], opened = [];
    private readonly Dictionary<string, object> settingsValues = new() { ["guiScale"] = 1f, ["dialogTransparency"] = 0.8f, ["showBlockInteractionHelp"] = true, ["language"] = "en" };
    private int mouseX = -1, mouseY = -1;
    private long elapsed;
    private bool disposed;
    private readonly float oldScale;
    private readonly Dictionary<string, ITranslationService> oldLanguages = new(Lang.AvailableLanguages);
    private readonly string? oldLocale = Lang.CurrentLocale;
    private readonly string? oldPath = Environment.GetEnvironmentVariable("PATH");
    private static readonly FieldInfo PatternCacheField = typeof(GuiElement).GetField("cachedPatterns", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingFieldException("Game GUI pattern cache changed");
    private readonly object oldPatterns = PatternCacheField.GetValue(null)!;
    private readonly string oldStandardFont = GuiStyle.StandardFontName, oldDecorativeFont = GuiStyle.DecorativeFontName;
    private TextTextureUtil text = null!;
    private IconUtil icons = null!;
    private readonly Dictionary<string, object> services = new();

    public PreviewHost(string gamePath, string modAssetsPath, int width, int height, double scale = 1)
    {
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        this.gamePath = Path.GetFullPath(gamePath); this.modAssetsPath = Path.GetFullPath(modAssetsPath);
        oldScale = RuntimeEnv.GUIScale;
        try
        {
            // Cairo and Skia use the installed native DLLs. This does not launch the client.
            Environment.SetEnvironmentVariable("PATH", Path.Combine(gamePath, "Lib") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
            Canvas = new SoftwareCanvas(width, height);
            PatternCacheField.SetValue(null, Activator.CreateInstance(PatternCacheField.FieldType));
            GuiStyle.StandardFontName = "sans-serif"; GuiStyle.DecorativeFontName = "Lora";
            RuntimeEnv.GUIScale = (float)scale;
            settingsValues["guiScale"] = (float)scale;
            window = new PreviewWindowBounds(width, height);
            Api = Proxy<ICoreClientAPI>("Api", Core);
            services["Gui"] = Proxy<IGuiAPI>("Gui", Gui);
            services["Render"] = Proxy<IRenderAPI>("Render", Render);
            services["Input"] = Proxy<IInputAPI>("Input", Input);
            services["Assets"] = Proxy<IAssetManager>("Assets", Assets);
            services["Logger"] = Proxy<ILogger>("Logger", Log);
            services["Settings"] = Proxy<ISettings>("Settings", Settings);
            services["World"] = Proxy<IClientWorldAccessor>("World", World);
            text = new TextTextureUtil(Api); icons = new IconUtil(Api);
            Lang.AvailableLanguages.Clear();
            var translation = new TranslationService("en", Api.Logger);
            translation.PreLoad(Path.Combine(gamePath, "assets"));
            foreach (var path in Directory.EnumerateFiles(Path.Combine(gamePath, "assets", "game", "lang"), "en.json", SearchOption.AllDirectories)) AssetFiles.Add(Path.GetFullPath(path));
            Lang.AvailableLanguages["en"] = translation; Lang.ChangeLanguage("en");
            var langPath = Path.Combine(modAssetsPath, "thebasics", "lang", "en.json");
            foreach (var entry in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(langPath))!)
                Lang.AvailableLanguages["en"].GetAllEntries()["thebasics:" + entry.Key] = entry.Value;
            AssetFiles.Add(langPath);
        }
        catch { RestoreGlobals(); throw; }
    }
    private T Proxy<T>(string name, System.Func<MethodInfo, object?[], object?> handler) where T : class => StrictApiProxy.Create<T>((m, a) => { UsedCalls.Add(name + "." + m); return handler(m, a); });
    private object? Unknown(MethodInfo m) { var call = m.DeclaringType!.Name + "." + m; UnsupportedCalls.Add(call); throw new NotSupportedException("Standalone GUI API operation not implemented: " + call); }
    private object? Core(MethodInfo m, object?[] a)
    {
        if (m.Name.StartsWith("get_") && services.TryGetValue(m.Name[4..], out var s)) return s;
        return m.Name switch { "get_ElapsedMilliseconds" => elapsed, "get_IsShuttingDown" => disposed, "get_Side" => EnumAppSide.Client, _ => Unknown(m) };
    }
    private object? World(MethodInfo m, object?[] a)
    {
        if (m.Name == "get_ElapsedMilliseconds") return elapsed;
        if (m.Name == "PlaySoundAt") { RecordedEffects.Add("sound:" + a[0]); return null; }
        return Unknown(m);
    }
    private object? Log(MethodInfo m, object?[] a)
    {
        if (m.Name == "Error") throw new InvalidDataException("Production GUI logged an error: " + string.Join(" ", a.Select(v => v?.ToString())));
        if (m.Name is "Debug" or "VerboseDebug" or "Notification" or "Warning" or "Event" or "Audit") { RecordedEffects.Add("log:" + m.Name + ":" + string.Join(" ", a.Select(v => v?.ToString()))); return null; }
        return Unknown(m);
    }
    private object? Input(MethodInfo m, object?[] a) => m.Name switch { "get_MouseX" => mouseX, "get_MouseY" => mouseY, "get_KeyboardKeyStateRaw" or "get_KeyboardKeyState" => new bool[512], "GetHotKeyByCode" => null, "get_MouseGrabbed" => false, _ => Unknown(m) };
    private object? Settings(MethodInfo m, object?[] a) => m.Name switch
    {
        "get_Bool" => Setting<bool>(),
        "get_Int" => Setting<int>(),
        "get_Float" => Setting<float>(),
        "get_String" => Setting<string>(),
        _ => Unknown(m)
    };
    private ISettingsClass<T> Setting<T>() => Proxy<ISettingsClass<T>>("Settings<" + typeof(T).Name + ">", (m, a) =>
    {
        var key = (string)a[0]!;
        if (m.Name == "Get") return settingsValues.TryGetValue(key, out var value) ? (T)value : a[1];
        if (m.Name == "get_Item") return settingsValues.TryGetValue(key, out var value) ? (T)value : throw new NotSupportedException("Unknown required setting: " + key);
        return Unknown(m);
    });
    private object? Assets(MethodInfo m, object?[] a)
    {
        if (m.Name is "Get" or "TryGet")
        {
            var loc = a[0] as AssetLocation ?? new AssetLocation((string)a[0]!);
            var path = Path.GetFullPath(Path.Combine(loc.Domain == "game" ? Path.Combine(gamePath, "assets") : modAssetsPath, loc.Domain, loc.Path));
            if (!File.Exists(path)) throw new FileNotFoundException("Required preview asset missing", path);
            AssetFiles.Add(path); return new Asset(File.ReadAllBytes(path), loc, null!);
        }
        return Unknown(m);
    }
    private object? Gui(MethodInfo m, object?[] a)
    {
        switch (m.Name)
        {
            case "get_WindowBounds": return window;
            case "get_TextTexture": return text;
            case "get_Icons": return icons;
            case "get_LoadedGuis": return loaded;
            case "get_OpenedGuis": return opened;
            case "CreateCompo": ((ElementBounds)a[1]!).ParentBounds ??= window; return typeof(GuiComposer).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(ICoreClientAPI), typeof(ElementBounds), typeof(string)], null)!.Invoke([Api, a[1], a[0]]);
            case "LoadCairoTexture": return Upload((ImageSurface)a[0]!, (bool)a[1]!);
            case "LoadOrUpdateCairoTexture":
                var surface = (ImageSurface)a[0]!; var texture = (LoadedTexture?)a[2] ?? new LoadedTexture(Api);
                texture.TextureId = Upload(surface, (bool)a[1]!, texture.TextureId); texture.Width = surface.Width; texture.Height = surface.Height; a[2] = texture; return null;
            case "DeleteTexture": Canvas.DeleteTexture((int)a[0]!); return null;
            case "GetDialogPosition": return null;
            case "RegisterDialog": loaded.AddRange((GuiDialog[])a[0]!); return null;
            case "RequestFocus": foreach (var d in loaded) d.UnFocus(); ((GuiDialog)a[0]!).Focus(); return null;
            case "TriggerDialogOpened": opened.Add((GuiDialog)a[0]!); return null;
            case "TriggerDialogClosed": opened.Remove((GuiDialog)a[0]!); return null;
            case "PlaySound": RecordedEffects.Add("sound:" + a[0]); return null;
        }
        return Unknown(m);
    }
    private int Upload(ImageSurface surface, bool linear, int id = 0)
    {
        surface.Flush(); var bytes = new byte[surface.Stride * surface.Height]; Marshal.Copy(surface.DataPtr, bytes, 0, bytes.Length);
        return Canvas.UploadTexture(bytes, surface.Width, surface.Height, surface.Stride, linear, id);
    }
    private object? Render(MethodInfo m, object?[] a)
    {
        double N(int i) => Convert.ToDouble(a[i]);
        switch (m.Name)
        {
            case "get_FrameWidth": return Canvas.Width;
            case "get_FrameHeight": return Canvas.Height;
            case "get_ScissorStack": return scissor;
            case "get_StandardFontName": return GuiStyle.StandardFontName;
            case "get_DecorativeFontName": return GuiStyle.DecorativeFontName;
            case "BitmapCreateFromPng": return new BitmapExternal(SKBitmap.Decode((byte[])a[0]!) ?? throw new InvalidDataException("PNG decode failed"));
            case "GlToggleBlend": if (!(bool)a[0]! || (EnumBlendMode)a[1]! != EnumBlendMode.Standard) return Unknown(m); return null;
            case "GLDeleteTexture": Canvas.DeleteTexture((int)a[0]!); return null;
            case "GlPushMatrix": Canvas.PushMatrix(); return null;
            case "GlPopMatrix": Canvas.PopMatrix(); return null;
            case "GlTranslate": Canvas.Translate(N(0), N(1), N(2)); return null;
            case "GlScissor": Canvas.SetScissor((int)a[0]!, (int)a[1]!, (int)a[2]!, (int)a[3]!); return null;
            case "GlScissorFlag": Canvas.ScissorEnabled = (bool)a[0]!; return null;
            case "PushScissor": PushScissor((ElementBounds?)a[0], (bool)a[1]!); return null;
            case "PopScissor": scissor.Pop(); ApplyScissor(); return null;
            case "RenderRectangle": Canvas.DrawRectangle(N(0), N(1), N(2), N(3), N(4), (int)a[5]!); return null;
            case "Render2DLoadedTexture": var t = (LoadedTexture)a[0]!; Canvas.DrawTexture(t.TextureId, N(1), N(2), t.Width, t.Height, N(3)); return null;
            case "Render2DTexture":
            case "RenderTexture":
            case "Render2DTexturePremultipliedAlpha":
                if (a[0] is not int id) return Unknown(m);
                bool pre = m.Name == "Render2DTexturePremultipliedAlpha";
                if (a[1] is ElementBounds b) Canvas.DrawTexture(id, (int)b.renderX, (int)b.renderY, b.OuterWidthInt, b.OuterHeightInt, N(2), (Vec4f?)a[3], pre);
                else Canvas.DrawTexture(id, pre || m.Name == "RenderTexture" ? (int)N(1) : N(1), pre || m.Name == "RenderTexture" ? (int)N(2) : N(2), (float)N(3), (float)N(4), N(5), (Vec4f?)a[6], pre);
                return null;
        }
        return Unknown(m);
    }
    private void ApplyScissor()
    {
        Canvas.ScissorEnabled = scissor.Count > 0;
        if (scissor.TryPeek(out var b) && b != null) Canvas.SetScissor((int)b.renderX, (int)(Canvas.Height - b.renderY - b.InnerHeight), (int)b.InnerWidth, (int)b.InnerHeight);
        else Canvas.ScissorEnabled = false;
    }
    private void PushScissor(ElementBounds? b, bool stacking)
    {
        if (b == null) Canvas.ScissorEnabled = false;
        else
        {
            if (stacking && scissor.TryPeek(out var previous) && previous != null)
            {
                double x = Math.Max(previous.renderX, b.renderX), y = Math.Max(previous.renderY, b.renderY);
                double right = Math.Min(previous.renderX + previous.InnerWidth, b.renderX + b.InnerWidth);
                double bottom = Math.Min(previous.renderY + previous.InnerHeight, b.renderY + b.InnerHeight);
                Canvas.SetScissor((int)x, (int)(Canvas.Height - bottom), Math.Max(0, (int)(right - x)), Math.Max(0, (int)(bottom - y)));
            }
            else Canvas.SetScissor((int)Math.Round(b.renderX), (int)Math.Round(Canvas.Height - b.renderY - b.InnerHeight), (int)b.InnerWidth, (int)b.InnerHeight);
            Canvas.ScissorEnabled = true;
        }
        scissor.Push(b!);
    }
    public void SetMouse(int x, int y) { mouseX = x; mouseY = y; }
    public void SetClock(long milliseconds) => elapsed = milliseconds;
    public void RenderGuiDialog(GuiDialog dialog, float deltaTime = 1f / 60) { Canvas.Clear(30, 30, 30, 255); dialog.OnRenderGUI(deltaTime); dialog.OnFinalizeFrame(deltaTime); }
    private object[] ControlBounds() => loaded.SelectMany(dialog => dialog.Composers.SelectMany(composer =>
        ((Dictionary<string, GuiElement>)(typeof(GuiComposer).GetField("staticElements", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("Game composer elements changed")).GetValue(composer.Value)!)
        .Select(element => (object)new
        {
            dialog = dialog.GetType().FullName,
            composer = composer.Key,
            key = element.Key,
            type = element.Value.GetType().FullName,
            x = element.Value.Bounds.renderX,
            y = element.Value.Bounds.renderY,
            width = element.Value.Bounds.OuterWidth,
            height = element.Value.Bounds.OuterHeight
        }))).ToArray();

    public object Manifest => new
    {
        viewport = new { Canvas.Width, Canvas.Height },
        controls = ControlBounds(),
        commands = Canvas.Commands.ToArray(),
        UsedCalls,
        UnsupportedCalls,
        assets = AssetFiles.Select(p => new { path = p, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p))) }),
        gameAssembly = typeof(GuiComposer).Assembly.GetName().FullName,
        binaries = new[] { typeof(GuiComposer).Assembly, typeof(Vintagestory.Common.Asset).Assembly, typeof(thebasics.ModSystems.ChatUiSystem.ChatUiSystem).Assembly, typeof(PreviewHost).Assembly }.Distinct().Select(a => new { name = a.GetName().Name, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))) }),
        os = RuntimeInformation.OSDescription,
        locale = Lang.CurrentLocale,
        RecordedEffects,
        elapsed,
        mouseX,
        mouseY,
        scale = RuntimeEnv.GUIScale,
        fonts = new { GuiStyle.StandardFontName, GuiStyle.DecorativeFontName }
    };
    private void RestoreGlobals()
    {
        RuntimeEnv.GUIScale = oldScale; Environment.SetEnvironmentVariable("PATH", oldPath);
        GuiStyle.StandardFontName = oldStandardFont; GuiStyle.DecorativeFontName = oldDecorativeFont;
        var patterns = PatternCacheField.GetValue(null)!;
        if (!ReferenceEquals(patterns, oldPatterns))
        {
            foreach (var pair in (Dictionary<AssetLocation, KeyValuePair<SurfacePattern, ImageSurface>>)patterns) { pair.Value.Key.Dispose(); pair.Value.Value.Dispose(); }
            PatternCacheField.SetValue(null, oldPatterns);
        }
        Lang.AvailableLanguages.Clear(); foreach (var l in oldLanguages) Lang.AvailableLanguages[l.Key] = l.Value;
        typeof(Lang).GetProperty(nameof(Lang.CurrentLocale))!.SetValue(null, oldLocale);
    }
    public void Dispose() { if (disposed) return; disposed = true; try { foreach (var d in loaded.ToArray()) d.Dispose(); } finally { RestoreGlobals(); } }
}
