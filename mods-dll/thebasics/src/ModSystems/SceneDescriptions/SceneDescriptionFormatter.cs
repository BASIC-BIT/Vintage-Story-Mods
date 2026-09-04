using thebasics.Utilities;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SceneDescriptions;

public static class SceneDescriptionFormatter
{
    public static string ToVtml(SceneDescriptionData data)
    {
        data = (data ?? new SceneDescriptionData()).Clone().Normalize();
        var oocPrefix = data.Kind == SceneDescriptionKind.OocNotice
            ? Lang.Get("thebasics:scene-description-ooc-prefix")
            : string.Empty;
        return ToVtml(data, oocPrefix);
    }

    internal static string ToVtml(SceneDescriptionData data, string oocPrefix)
    {
        data = (data ?? new SceneDescriptionData()).Clone().Normalize();
        var title = VtmlUtils.EscapeVtml(data.Title);
        var body = VtmlUtils.EscapeVtml(data.Body).Replace("\n", "<br>");
        var prefix = data.Kind == SceneDescriptionKind.OocNotice
            ? $"<strong>{VtmlUtils.EscapeVtml(oocPrefix)}</strong> "
            : string.Empty;
        var titleLine = string.IsNullOrWhiteSpace(title) ? string.Empty : $"<strong>{title}</strong><br>";
        var formattedBody = data.Kind == SceneDescriptionKind.Environmental ? $"<i>{body}</i>" : body;
        return $"{prefix}{titleLine}{formattedBody}";
    }
}
