using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.SceneDescriptions;

internal static class SceneMarkerVisuals
{
    internal static Shape LoadShape(ICoreClientAPI api, SceneMarkerSymbol symbol)
    {
        var name = symbol switch
        {
            SceneMarkerSymbol.Question => "question",
            SceneMarkerSymbol.Information => "information",
            _ => "exclamation",
        };
        return api.Assets.Get(new AssetLocation($"thebasics:shapes/block/scene-symbol-{name}.json")).ToObject<Shape>();
    }

    // The same cuboids supply the 3D mesh, billboard artwork and picker preview.
    internal static void Draw(Context ctx, Shape shape, double x, double y, double size, bool extruded = false)
    {
        var unit = size / 16;
        if (extruded)
        {
            ctx.SetSourceRGBA(0.45, 0.29, 0.08, 1);
            foreach (var element in shape.Elements)
            {
                ctx.Rectangle(x + element.From[0] * unit + 3, y + (16 - element.To[1]) * unit + 3,
                    (element.To[0] - element.From[0]) * unit, (element.To[1] - element.From[1]) * unit);
            }
            ctx.Fill();
        }
        ctx.SetSourceRGBA(1, 0.82, 0.30, 1);
        foreach (var element in shape.Elements)
        {
            ctx.Rectangle(x + element.From[0] * unit, y + (16 - element.To[1]) * unit,
                (element.To[0] - element.From[0]) * unit, (element.To[1] - element.From[1]) * unit);
        }
        ctx.Fill();
    }
}
