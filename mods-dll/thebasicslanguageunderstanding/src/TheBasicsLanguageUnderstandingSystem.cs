#nullable enable

using System;
using thebasics.ModSystems.ProximityChat;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasicslanguageunderstanding;

public sealed class TheBasicsLanguageUnderstandingSystem : ModSystem
{
    private const string ConfigName = "thebasicslanguageunderstanding.json";
    private OnnxMiniLmEmbeddingProvider? _provider;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Server;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        var proximityChat = api.ModLoader.GetModSystem<RPProximityChatSystem>();
        if (proximityChat == null)
        {
            api.Logger.Warning("[thebasics-language-understanding] The BASICs proximity chat system was not available; semantic provider was not registered.");
            return;
        }

        var config = LoadConfig(api);
        _provider = new OnnxMiniLmEmbeddingProvider(api, Mod.Info.ModID, config);
        proximityChat.RegisterSemanticEmbeddingProvider(_provider);
    }

    private static LanguageUnderstandingConfig LoadConfig(ICoreServerAPI api)
    {
        LanguageUnderstandingConfig? config = null;
        try
        {
            config = api.LoadModConfig<LanguageUnderstandingConfig>(ConfigName);
        }
        catch (Exception ex)
        {
            api.Logger.Warning($"[thebasics-language-understanding] Failed to load {ConfigName}; using defaults: {ex.Message}");
        }

        config ??= new LanguageUnderstandingConfig();
        config.InitializeDefaultsIfNeeded();
        api.StoreModConfig(config, ConfigName);
        return config;
    }

    public override void Dispose()
    {
        _provider?.Dispose();
        base.Dispose();
    }
}
