using thebasics.Utilities;
using System.Linq;

namespace thebasics.ModSystems.SceneDescriptions;

public static class SceneDescriptionFormatter
{
    internal static string ToFloatingVtml(SceneDescriptionData source, string readPrompt)
    {
        var data = (source ?? new SceneDescriptionData()).Clone().Normalize();
        var title = string.IsNullOrWhiteSpace(data.Title) ? string.Empty : $"<strong>{VtmlUtils.EscapeVtml(data.Title)}</strong>";
        var body = string.Empty;
        if (!string.IsNullOrWhiteSpace(data.Body))
            body = data.ShowBodyInBubble
                ? VtmlUtils.EscapeVtml(FloatingPreview(data.Body)).Replace("\n", "<br>")
                : $"<font size=\"16\">{VtmlUtils.EscapeVtml(readPrompt)}</font>";
        return string.Join("<br>", new[] { title, body }.Where(text => text.Length > 0));
    }

    public static string ToVtml(SceneDescriptionData data)
    {
        data = (data ?? new SceneDescriptionData()).Clone().Normalize();
        var title = VtmlUtils.EscapeVtml(data.Title);
        var body = VtmlUtils.EscapeVtml(data.Body).Replace("\n", "<br>");
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
        return VtmlUtils.EscapeVtml(body);
    }
}
