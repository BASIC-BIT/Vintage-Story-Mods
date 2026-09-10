using System;

namespace thebasics.ModSystems.SceneDescriptions;

internal static class SceneBubbleLayout
{
    // Fixed texel density: adding lines grows the panel instead of shrinking every letter.
    internal static (float Width, float Height) Size(int textureWidth, int textureHeight, float guiScale, float scale)
    {
        var pixelsPerBlock = 200 * (guiScale > 0 ? guiScale : 1);
        return (textureWidth / pixelsPerBlock * scale, textureHeight / pixelsPerBlock * scale);
    }

    internal static double CullRadius(float width, float height, float indicatorScale)
    {
        var bottom = 0.8 * indicatorScale * 1.1 / 2 + 0.15;
        return Math.Max(2, Math.Sqrt(width * width / 4 + (height + bottom) * (height + bottom)));
    }
}
