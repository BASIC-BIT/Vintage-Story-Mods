using thebasics.Utilities;
using System.Linq;

namespace thebasics.ModSystems.SceneDescriptions;

public static class SceneDescriptionFormatter
{
    // The game decodes lt/gt/nbsp but not amp. A neutral token boundary keeps authored entities literal.
    internal static string EscapeLiteral(string text) => string.Join("&<font></font>", (text ?? string.Empty).Split('&').Select(VtmlUtils.EscapeVtml));

    internal static string ToFloatingVtml(SceneDescriptionData source)
    {
        var data = (source ?? new SceneDescriptionData()).Clone().Normalize();
        var title = string.IsNullOrWhiteSpace(data.Title) ? string.Empty : $"<strong>{EscapeLiteral(data.Title)}</strong>";
        var body = data.ShowBodyInBubble ? EscapeLiteral(FloatingPreview(data.Body)).Replace("\n", "<br>") : string.Empty;
        return string.Join("<br>", new[] { title, body }.Where(text => text.Length > 0));
    }

    public static string ToVtml(SceneDescriptionData data)
    {
        data = (data ?? new SceneDescriptionData()).Clone().Normalize();
        var title = EscapeLiteral(data.Title);
        var body = EscapeLiteral(data.Body).Replace("\n", "<br>");
        var titleLine = string.IsNullOrWhiteSpace(title) ? string.Empty : $"<strong>{title}</strong><br>";
        return $"{titleLine}{body}";
    }

    public static string FloatingPreview(string body)
    {
        body ??= string.Empty;
        var preview = string.Join("\n", body.Split('\n').Take(12));
        if (preview.Length > 600) preview = preview[..600];
        return preview.Length < body.Length ? preview.TrimEnd() + "..." : preview;
    }

    public static string InspectorPreview(string body)
    {
        body = (body ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (body.Length > 180) body = body[..177] + "...";
        return EscapeLiteral(body);
    }
}
