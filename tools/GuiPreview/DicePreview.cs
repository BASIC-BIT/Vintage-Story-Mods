using System.Reflection;
using System.Runtime.ExceptionServices;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Client;

namespace TheBasics.GuiPreview;

/// <summary>Deterministic fixtures of the production dice bubble painter and speech font path.</summary>
public static class DicePreview
{
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
    {
        "dice-normal", "dice-whisper", "dice-yell", "dice-complex"
    });

    private static readonly MethodInfo CreateBubbleFont = typeof(SpeechBubbleVtmlPatches).GetMethod(
        "CreateBubbleFont", BindingFlags.Static | BindingFlags.NonPublic, [typeof(string)])
        ?? throw new MissingMethodException(typeof(SpeechBubbleVtmlPatches).FullName, "CreateBubbleFont(string)");

    public static object Render(string name, PreviewHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!Names.Contains(name, StringComparer.Ordinal)) throw new ArgumentException("Unknown dice fixture: " + name, nameof(name));
        var mode = name switch
        {
            "dice-whisper" => ProximityChatMode.Whisper,
            "dice-yell" => ProximityChatMode.Yell,
            _ => ProximityChatMode.Normal
        };
        var modeName = mode.ToString().ToLowerInvariant();
        var expression = name == "dice-complex" ? "3d6!kh2>=5 # decisive strike" : "d20";
        int[] suppliedFaces = name == "dice-complex" ? [6, 2, 5, 6, 1] : [17];
        var faces = new Queue<int>(suppliedFaces);
        var result = DiceEvaluator.EvaluateInput(expression, _ => faces.Dequeue());
        if (faces.Count != 0) throw new InvalidOperationException("Dice fixture did not consume its expected deterministic faces.");
        var summary = DicePresentation.Summary(result, mode);
        CairoFont font;
        try { font = (CairoFont)CreateBubbleFont.Invoke(null, [modeName])!; }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        host.Canvas.Clear(24, 27, 32, 255);
        using var texture = DiceBubbleTexture.Create(host.Api, summary, result.SimpleSides, font);
        if (texture.Width > host.Canvas.Width || texture.Height > host.Canvas.Height)
            throw new InvalidOperationException($"Dice fixture texture {texture.Width}x{texture.Height} exceeds the preview viewport.");
        int x = (host.Canvas.Width - texture.Width) / 2;
        int y = (host.Canvas.Height - texture.Height) / 2;
        host.Canvas.DrawTexture(texture.TextureId, x, y, texture.Width, texture.Height, premultipliedAlpha: true);
        return new
        {
            fixture = name,
            mode = modeName,
            expression,
            suppliedFaces,
            result.Value,
            result.Breakdown,
            result.SimpleSides,
            result.IsSuccessPool,
            summary,
            font = new { source = "SpeechBubbleVtmlPatches.CreateBubbleFont", font.Fontname, font.UnscaledFontsize },
            bounds = new { x, y, width = texture.Width, height = texture.Height },
            scope = "Production dice evaluation, summary, bubble font and texture painter; standalone compositing, not in-world positioning or network delivery."
        };
    }
}
