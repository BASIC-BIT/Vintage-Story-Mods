using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.SceneDescriptions;

public sealed class SceneDescriptionSystem : ModSystem
{
    private ICoreClientAPI _clientApi;
    private SceneDescriptionRenderer _renderer;
    private Harmony _harmony;

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("TheBasicsSceneDescriptionBlock", typeof(SceneDescriptionBlock));
        api.RegisterBlockEntityClass("TheBasicsSceneDescription", typeof(SceneDescriptionBlockEntity));
        _harmony = new Harmony("thebasics.scene-marker-padlocks");
        _harmony.CreateClassProcessor(typeof(SceneDescriptionPadlockPatch)).Patch();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        _renderer = new SceneDescriptionRenderer(api);
        api.Event.RegisterRenderer(_renderer, EnumRenderStage.Ortho, "thebasics-scene-descriptions");
    }

    public override void Dispose()
    {
        _harmony?.UnpatchAll("thebasics.scene-marker-padlocks");
        _harmony = null;
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
