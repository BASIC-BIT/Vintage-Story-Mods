using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneTitleIconDialog : GuiDialog
{
    private readonly Action<int> _choose;
    private readonly Action _closed;
    public override string ToggleKeyCombinationCode => null;
    public override bool UnregisterOnClose => true;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    internal SceneTitleIconDialog(ICoreClientAPI api, Action<int> choose, Action closed) : base(api)
    {
        _choose = choose;
        _closed = closed;
        var bounds = ElementBounds.Fixed(0, 0, 380, 155).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = api.Gui.CreateCompo("thebasics-scene-title-icon", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(bounds)
            .AddDialogTitleBar(Lang.Get("thebasics:scene-title-icon"), () => TryClose())
            .BeginChildElements(bounds);
        foreach (var symbol in Enum.GetValues<SceneMarkerSymbol>())
        {
            var shape = SceneMarkerVisuals.LoadShape(api, symbol);
            var x = 8 + (int)symbol * 62;
            SingleComposer.AddSmallButton("", () => Choose((int)symbol + 1), ElementBounds.Fixed(x, 45, 54, 54));
            SingleComposer.AddSceneDrawing(ElementBounds.Fixed(x + 7, 52, 40, 40),
                (ctx, surface, _) => SceneMarkerVisuals.Draw(ctx, shape, 0, 0, surface.Width, SceneMarkerColor.Parchment, symbol));
        }
        SingleComposer.AddSmallButton(Lang.Get("thebasics:scene-icon-none"), () => Choose(0), ElementBounds.Fixed(8, 116, 140, 30))
            .EndChildElements().Compose();
    }

    private bool Choose(int icon)
    {
        _choose(icon);
        TryClose();
        return true;
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        _closed();
        Dispose();
    }
}
