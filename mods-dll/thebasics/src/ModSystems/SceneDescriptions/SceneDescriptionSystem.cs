using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.SceneDescriptions;

public sealed class SceneDescriptionSystem : ModSystem
{
    private ICoreClientAPI _clientApi;
    private SceneDescriptionRenderer _renderer;

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("TheBasicsSceneDescriptionBlock", typeof(SceneDescriptionBlock));
        api.RegisterBlockEntityClass("TheBasicsSceneDescription", typeof(SceneDescriptionBlockEntity));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        _renderer = new SceneDescriptionRenderer(api);
        api.Event.RegisterRenderer(_renderer, EnumRenderStage.Ortho, "thebasics-scene-descriptions");
    }

    public override void Dispose()
    {
        if (_clientApi != null && _renderer != null)
        {
            _clientApi.Event.UnregisterRenderer(_renderer, EnumRenderStage.Ortho);
        }

        _renderer?.Dispose();
        _renderer = null;
        _clientApi = null;
        base.Dispose();
    }
}
