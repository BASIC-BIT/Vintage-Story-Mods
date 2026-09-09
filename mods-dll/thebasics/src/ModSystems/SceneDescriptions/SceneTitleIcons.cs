using System;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.SceneDescriptions;

internal static class SceneTitleIcons
{
    // Built-in DrawIcon names from Vintage Story 1.22.7. Custom and SVG assets are discovered live.
    private static readonly string[] BuiltIn = { "airbrush", "basket", "belt", "boots", "bracers", "cape", "dice", "eraser", "erode", "floodfill", "gloves", "growshrink", "handheld", "hat", "import", "lake", "left", "leftmousebutton", "line", "mask", "medal", "move", "necklace", "none", "offhand", "paintbrush", "plus", "pullover", "raiselower", "redo", "repeat", "right", "rightmousebutton", "ring", "select", "shirt", "tree", "trousers", "undo", "wpBee", "wpCave", "wpCircle", "wpCross", "wpHome", "wpLadder", "wpPick", "wpPlayer", "wpRocks", "wpRuins", "wpSpiral", "wpStar1", "wpStar2", "wpTrader", "wpVessel" };

    internal static string[] Available(ICoreClientAPI api) => BuiltIn
        .Concat(api.Gui.Icons.CustomIcons.Keys)
        .Concat(api.Assets.GetMany("textures/", loadAsset: false)
            .Where(asset => asset.Location.Path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            .Select(asset => "svg:" + asset.Location))
        .Where(name => SceneDescriptionData.NormalizeIconName(name).Length > 0)
        .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

    internal static bool Draw(ICoreClientAPI api, Context ctx, ImageSurface surface, string name)
    {
        try
        {
            if (name.StartsWith("svg:", StringComparison.Ordinal))
            {
                var location = new AssetLocation(name[4..]);
                if (!location.Path.StartsWith("textures/", StringComparison.Ordinal) ||
                    !location.Path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return false;
                var asset = api.Assets.TryGet(location);
                if (asset == null) return false;
                api.Gui.DrawSvg(asset, surface, 0, 0, surface.Width, surface.Height, ColorUtil.WhiteArgb);
            }
            else api.Gui.Icons.DrawIcon(ctx, name, 0, 0, surface.Width, surface.Height, ColorUtil.WhiteArgbDouble);
            return true;
        }
        catch (Exception)
        {
            // A malformed asset or third-party renderer must not break the picker or world bubble.
            using var clear = new Context(surface);
            clear.Operator = Operator.Clear;
            clear.Paint();
            return false;
        }
    }
}
