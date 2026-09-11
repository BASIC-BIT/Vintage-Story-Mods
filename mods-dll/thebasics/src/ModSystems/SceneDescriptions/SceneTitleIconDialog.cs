using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.SceneDescriptions;

internal sealed class SceneTitleIconDialog : GuiDialog
{
    private const int PageSize = 32;
    private readonly Action<string> _choose;
    private readonly Action _closed;
    private readonly string[] _icons;
    private string _query = string.Empty;
    private int _page;
    public override string ToggleKeyCombinationCode => null;
    public override bool UnregisterOnClose => true;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;

    internal SceneTitleIconDialog(ICoreClientAPI api, Action<string> choose, Action closed) : base(api)
    {
        _choose = choose;
        _closed = closed;
        _icons = SceneTitleIcons.Available(api);
        Compose();
    }

    private void Compose()
    {
        var matches = _icons.Where(name => name.Contains(_query, StringComparison.OrdinalIgnoreCase)).ToArray();
        var pages = Math.Max(1, (matches.Length + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        SingleComposer?.Dispose();
        var bounds = ElementBounds.Fixed(0, 0, 510, 395).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        SingleComposer = capi.Gui.CreateCompo("thebasics-scene-title-icon", ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(bounds)
            .AddDialogTitleBar(Lang.Get("thebasics:scene-title-icon"), () => TryClose())
            .BeginChildElements(bounds)
            .AddTextInput(ElementBounds.Fixed(8, 40, 350, 30), null, CairoFont.TextInput(), "search")
            .AddSmallButton(Lang.Get("thebasics:scene-icon-search"), () =>
            {
                _query = SingleComposer.GetTextInput("search").GetText().Trim();
                _page = 0;
                Compose();
                return true;
            }, ElementBounds.Fixed(375, 40, 125, 30));
        var entries = matches.Skip(_page * PageSize).Take(PageSize).ToArray();
        for (var index = 0; index < entries.Length; index++)
        {
            var name = entries[index];
            var x = 8 + index % 8 * 62;
            var y = 85 + index / 8 * 62;
            var tile = ElementBounds.Fixed(x, y, 54, 54);
            SingleComposer.AddSmallButton("", () => Choose(name), tile);
            SingleComposer.AddSceneDrawing(ElementBounds.Fixed(x + 7, y + 7, 40, 40),
                (ctx, surface, _) => SceneTitleIcons.Draw(capi, ctx, surface, name));
            SingleComposer.AddHoverText(name, CairoFont.WhiteSmallText(), 400, tile.FlatCopy());
        }
        SingleComposer.AddSmallButton(Lang.Get("thebasics:scene-icon-none"), () => Choose(string.Empty), ElementBounds.Fixed(8, 350, 100, 30))
            .AddSmallButton("<", () => { _page--; Compose(); return true; }, ElementBounds.Fixed(230, 350, 45, 30))
            .AddStaticText($"{_page + 1} / {pages}", CairoFont.WhiteSmallText(), ElementBounds.Fixed(285, 355, 70, 25))
            .AddSmallButton(">", () => { _page++; Compose(); return true; }, ElementBounds.Fixed(365, 350, 45, 30))
            .EndChildElements().Compose();
        SingleComposer.GetTextInput("search").SetValue(_query);
    }

    private bool Choose(string icon)
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
