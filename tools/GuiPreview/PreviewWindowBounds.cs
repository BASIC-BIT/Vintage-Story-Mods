using Vintagestory.API.Client;
namespace TheBasics.GuiPreview;
/// <summary>Viewport-only equivalent of the game's platform-backed ElementWindowBounds.</summary>
internal sealed class PreviewWindowBounds : ElementBounds
{
    private readonly int width, height;
    public PreviewWindowBounds(int width, int height) { this.width = width; this.height = height; IsWindowBounds = true; Initialized = true; }
    public override double relX => 0; public override double relY => 0;
    public override double absX => 0; public override double absY => 0;
    public override double renderX => 0; public override double renderY => 0;
    public override double drawX => 0; public override double drawY => 0;
    public override double OuterWidth => width; public override double OuterHeight => height;
    public override double InnerWidth => width; public override double InnerHeight => height;
    public override int OuterWidthInt => width; public override int OuterHeightInt => height;
    public override bool RequiresRecalculation => false;
    public override void CalcWorldBounds() { }
}
