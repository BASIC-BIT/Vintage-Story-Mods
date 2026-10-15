using System;
using Cairo;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A sample landscape with the game's parchment palette and native player icons.</summary>
public static class SetupWizardMapPreview
{
    private const int Size = 250;
    private const double PixelsPerBlock = 0.65;
    private static readonly int[] Terrain = CreateTerrain();
    private static readonly IconUtil FallbackIcons = new(null);

    public static void Draw(Context context, SetupWizardDraft draft, IconUtil icons)
    {
        context.Save();
        try
        {
            context.Translate(30, 10);
            var background = GuiStyle.DialogDefaultBgColor;
            context.SetSourceRGBA(background[0], background[1], background[2], 1);
            context.Rectangle(-2, -2, Size + 4, Size + 4);
            context.Fill();
            context.Rectangle(0, 0, Size, Size);
            context.Clip();
            context.Antialias = Antialias.None;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size;)
                {
                    int color = Terrain[y * Size + x];
                    int end = x + 1;
                    while (end < Size && Terrain[y * Size + end] == color) end++;
                    context.SetSourceRGB((color >> 16 & 255) / 255d, (color >> 8 & 255) / 255d, (color & 255) / 255d);
                    context.Rectangle(x, y, end - x, 1);
                    context.Fill();
                    x = end;
                }
            context.Antialias = Antialias.Default;
            icons ??= FallbackIcons;
            bool managed = IsOn(draft, "ManageMapPlayerVisibility");
            bool hidden = managed && IsOn(draft, "MapHideOtherPlayers");
            double.TryParse(draft.Get("MapPlayerRenderDistance"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double range);
            // Unmanaged markers are an illustrative vanilla view, independent of the disabled draft controls.
            if (!hidden)
            {
                if (!managed || range < 0 || range >= 30) Player(context, icons, 24, -18, 0.8, false);
                if (!managed || range < 0 || range >= 80) Player(context, icons, -64, 48, -0.6, false);
                if (!managed || range < 0 || range >= 140) Player(context, icons, 112, 84, 2.2, false);
            }
            Player(context, icons, 0, 0, 0, true);
        }
        finally { context.Restore(); }
    }

    private static void Player(Context context, IconUtil icons, double dx, double dz, double yaw, bool self)
    {
        context.Save();
        context.Translate(Size / 2d + dx * PixelsPerBlock, Size / 2d + dz * PixelsPerBlock);
        context.Rotate(-yaw + Math.PI);
        icons.DrawMapPlayer(context, -8, -8, 16, 16,
            self ? new[] { 0d, 0d, 0d, 1d } : new[] { 0.3, 0.3, 0.3, 1d },
            self ? new[] { 1d, 1d, 1d, 1d } : new[] { 0.7, 0.7, 0.7, 1d });
        context.Restore();
    }

    private static bool IsOn(SetupWizardDraft draft, string key) =>
        ConfigAdminSettingDefinition.TryParseBool(draft.Get(key), out bool value) && value;

    private static int[] CreateTerrain()
    {
        int Color(string code) => Convert.ToInt32(ChunkMapLayer.hexColorsByCode[code].TrimStart('#'), 16);
        int land = Color("land"), forest = Color("forest"), plants = Color("plant"), water = Color("lake"), shore = Color("wateredge");
        int road = Color("road"), settlement = Color("settlement");
        var pixels = new int[Size * Size];
        var wet = new bool[pixels.Length];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                double bank = 178 + 19 * Math.Sin(y / 39d) + 6 * Math.Sin(y / 11d);
                wet[y * Size + x] = x > bank && x < bank + 24 + 7 * Math.Sin(y / 28d) ||
                    Math.Pow((x - 203) / 42d, 2) + Math.Pow((y - 51) / 31d, 2) < 1;
            }
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int index = y * Size + x;
                if (wet[index])
                {
                    bool edge = x > 0 && !wet[index - 1] || x + 1 < Size && !wet[index + 1] ||
                        y > 0 && !wet[index - Size] || y + 1 < Size && !wet[index + Size];
                    pixels[index] = edge ? shore : water;
                    continue;
                }
                uint noise = unchecked((uint)(x * 374761393 + y * 668265263));
                noise = (noise ^ noise >> 13) * 1274126177;
                double trees = Math.Sin(x / 27d) + Math.Cos(y / 34d) + Math.Sin((x + y) / 23d);
                int color = trees > 0.7 ? (noise % 5 == 0 ? plants : forest) : land;
                if (Math.Abs(y - (169 - x * 0.36 + 4 * Math.Sin(x / 18d))) < 1.5) color = road;
                if (x is > 91 and < 146 && y is > 108 and < 153 && (x - 91) % 18 < 8 && (y - 108) % 16 < 7) color = settlement;
                double shade = 0.92 + (noise % 7) * 0.012 + 0.06 * Math.Sin((x + y) / 17d);
                int r = Math.Clamp((int)((color >> 16 & 255) * shade), 0, 255);
                int g = Math.Clamp((int)((color >> 8 & 255) * shade), 0, 255);
                int b = Math.Clamp((int)((color & 255) * shade), 0, 255);
                pixels[index] = r << 16 | g << 8 | b;
            }
        return pixels;
    }
}
