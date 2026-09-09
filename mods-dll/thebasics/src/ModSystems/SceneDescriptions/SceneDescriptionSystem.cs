using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.SceneDescriptions;

public sealed class SceneDescriptionSystem : ModSystem
{
    private ICoreClientAPI _clientApi;
    private SceneMarkerIconRenderer _iconRenderer;
    private SceneMarkerSelection _selection;

    internal void Register(SceneDescriptionBlockEntity marker) { _iconRenderer?.Register(marker); _selection?.Register(marker); }
    internal void Unregister(SceneDescriptionBlockEntity marker) { _iconRenderer?.Unregister(marker); _selection?.Unregister(marker); }

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("TheBasicsSceneDescriptionBlock", typeof(SceneDescriptionBlock));
        api.RegisterBlockEntityClass("TheBasicsSceneDescription", typeof(SceneDescriptionBlockEntity));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        foreach (var symbol in System.Enum.GetValues<SceneMarkerSymbol>())
        {
            var shape = SceneMarkerVisuals.LoadShape(api, symbol);
            api.Gui.Icons.CustomIcons["thebasics-scene-title-" + ((int)symbol + 1)] =
                (ctx, x, y, width, height, _) => SceneMarkerVisuals.Draw(ctx, shape, x, y, System.Math.Min(width, height), SceneMarkerColor.Parchment, symbol);
        }
        _iconRenderer = new SceneMarkerIconRenderer(api);
        _selection = new SceneMarkerSelection(api);
        api.Event.RegisterRenderer(_iconRenderer, EnumRenderStage.Opaque, "thebasics-scene-icons");
    }

    public override void Dispose()
    {
        if (_clientApi != null)
            foreach (var symbol in System.Enum.GetValues<SceneMarkerSymbol>())
                _clientApi.Gui.Icons.CustomIcons.Remove("thebasics-scene-title-" + ((int)symbol + 1));
        if (_clientApi != null && _iconRenderer != null)
            _clientApi.Event.UnregisterRenderer(_iconRenderer, EnumRenderStage.Opaque);
        _iconRenderer?.Dispose();
        _selection?.Dispose();
        _selection = null;
        _iconRenderer = null;
        _clientApi = null;
        base.Dispose();
    }
}
