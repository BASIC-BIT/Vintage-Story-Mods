using System;
using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace DimensionCavern;

public sealed class CavernDimensionModSystem : ModSystem
{
    private const string ModId = "dimensioncavern";
    private const string ApiCacheKey = "dimensionlib:api";
    private const string DefaultDimensionId = "dimensioncavern:demo-cavern";
    private const string GeneratorId = "dimensioncavern:cavern";

    private IDimensionLibApi _dimensionLib;

    public override double ExecuteOrder()
    {
        return 1.05;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        if (!api.ObjectCache.TryGetValue(ApiCacheKey, out var cachedApi) || cachedApi is not IDimensionLibApi dimensionLib)
        {
            api.Logger.Warning("[CavernDimensionDemo] DimensionLib API was not available; cavern demo commands were not registered.");
            return;
        }

        _dimensionLib = dimensionLib;
        var registered = _dimensionLib.RegisterGenerator(new CavernDimensionGenerator(api, GeneratorId));
        if (!registered.Success)
        {
            api.Logger.Warning("[CavernDimensionDemo] Failed to register cavern generator: {0}", registered.Message);
        }

        RegisterCommands(api);
        api.Logger.Notification("[CavernDimensionDemo] Registered /caverndemo commands.");
    }

    private void RegisterCommands(ICoreServerAPI api)
    {
        api.ChatCommands.GetOrCreate("caverndemo")
            .WithDescription("Cavern Dimension demo commands")
            .RequiresPrivilege(Privilege.root)
            .BeginSubCommand("create")
                .WithDescription("Create and prepare a demo cavern dimension")
                .WithArgs(new StringArgParser("[dimensionId] [sizeChunks] [seed]", false))
                .RequiresPlayer()
                .HandleWith(HandleCreate)
                .EndSubCommand()
            .BeginSubCommand("prepare")
                .WithDescription("Prepare the demo cavern dimension")
                .WithArgs(new StringArgParser("[dimensionId]", false))
                .RequiresPlayer()
                .HandleWith(HandlePrepare)
                .EndSubCommand()
            .BeginSubCommand("enter")
                .WithDescription("Enter the demo cavern dimension")
                .WithArgs(new StringArgParser("[dimensionId]", false))
                .RequiresPlayer()
                .HandleWith(HandleEnter)
                .EndSubCommand();
    }

    private TextCommandResult HandleCreate(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        ParseArgs((string)args[0], out var dimensionId, out var sizeChunks, out var seed);
        var existing = _dimensionLib.GetDimension(dimensionId);
        if (existing.Success && !_dimensionLib.IsDimensionOrphaned(dimensionId))
        {
            return ToCommandResult(Prepare(dimensionId, player, $"Cavern dimension '{dimensionId}' already exists; prepared existing dimension."));
        }

        var registered = _dimensionLib.RegisterDimension(CreateSpec(dimensionId, sizeChunks, seed));
        if (!registered.Success)
        {
            return ToCommandResult(registered);
        }

        return ToCommandResult(Prepare(dimensionId, player, $"Created cavern dimension '{dimensionId}'."));
    }

    private TextCommandResult HandlePrepare(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var dimensionId = ParseDimensionId((string)args[0]);
        return ToCommandResult(Prepare(dimensionId, player, $"Prepared cavern dimension '{dimensionId}'."));
    }

    private TextCommandResult HandleEnter(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var dimensionId = ParseDimensionId((string)args[0]);
        var prepared = Prepare(dimensionId, player, string.Empty);
        if (!prepared.Success)
        {
            return ToCommandResult(prepared);
        }

        return ToCommandResult(_dimensionLib.TeleportToDimension(player, dimensionId));
    }

    private DimensionLibResult Prepare(string dimensionId, IServerPlayer player, string successMessage)
    {
        var prepared = _dimensionLib.PrepareGeneratedDimension(dimensionId, player);
        return prepared.Success ? DimensionLibResult.Ok(successMessage) : prepared;
    }

    private DimensionSpec CreateSpec(string dimensionId, int? sizeChunks, long? seed)
    {
        var size = Math.Max(3, Math.Min(16, sizeChunks ?? 9));
        return new DimensionSpec
        {
            DimensionId = dimensionId,
            OwnerModId = ModId,
            DimensionPlaneId = _dimensionLib.PrimaryDimensionPlaneId,
            Placement = DimensionPlacement.AutomaticSparse,
            ChunkSizeX = size,
            ChunkSizeZ = size,
            SpawnY = 68,
            GeneratorId = GeneratorId,
            VisualSettings = CreateCavernVisualSettings(),
            Seed = seed ?? 2026052902,
            Kind = DimensionKind.Pocket,
            AccessPolicy = DimensionAccessPolicy.AdminOnly,
            Mutability = DimensionMutability.Mutable,
            IsTransient = true,
        };
    }

    private static DimensionVisualSettings CreateCavernVisualSettings()
    {
        return new DimensionVisualSettings
        {
            RenderSkyCover = true,
            SkyRed = 0.035f,
            SkyGreen = 0.0035f,
            SkyBlue = 0.002f,
            SkyAlpha = 1f,
            FogRed = 0.24f,
            FogGreen = 0.045f,
            FogBlue = 0.018f,
            FogColorWeight = 0.16f,
            AmbientRed = 0.74f,
            AmbientGreen = 0.34f,
            AmbientBlue = 0.2f,
            AmbientColorWeight = 0.48f,
            FogDensity = 0.0016f,
            FogDensityWeight = 0.16f,
            CloudDensityWeight = 0.7f,
            CloudBrightnessWeight = 0.7f,
            SceneBrightness = 1.0f,
            SceneBrightnessWeight = 0.45f,
            FogBrightness = 0.95f,
            FogBrightnessWeight = 0.2f,
            MinimumSceneLight = 0.08f,
            LightLiftRed = 0.85f,
            LightLiftGreen = 0.42f,
            LightLiftBlue = 0.24f,
        };
    }

    private static void ParseArgs(string raw, out string dimensionId, out int? sizeChunks, out long? seed)
    {
        var args = new CmdArgs(raw ?? string.Empty);
        var first = args.PopWord(null);
        if (int.TryParse(first, out var parsedSize))
        {
            dimensionId = DefaultDimensionId;
            sizeChunks = parsedSize;
        }
        else
        {
            dimensionId = NormalizeDimensionId(first);
            sizeChunks = int.TryParse(args.PopWord(null), out parsedSize) ? parsedSize : null;
        }

        seed = long.TryParse(args.PopWord(null), out var parsedSeed) ? parsedSeed : null;
    }

    private static string ParseDimensionId(string raw)
    {
        var args = new CmdArgs(raw ?? string.Empty);
        return NormalizeDimensionId(args.PopWord(null));
    }

    private static string NormalizeDimensionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultDimensionId;
        }

        value = value.Trim().ToLowerInvariant();
        return value.Contains(':') ? value : $"{ModId}:{value}";
    }

    private static TextCommandResult ToCommandResult(DimensionLibResult result)
    {
        return result.Success ? TextCommandResult.Success(result.Message) : TextCommandResult.Error(result.Message, result.ErrorCode);
    }

    private static TextCommandResult ToCommandResult<T>(DimensionLibResult<T> result)
    {
        return result.Success ? TextCommandResult.Success(result.Message) : TextCommandResult.Error(result.Message, result.ErrorCode);
    }
}
