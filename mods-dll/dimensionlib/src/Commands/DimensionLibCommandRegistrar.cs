using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DimensionLib.Api;
using DimensionLib.Services;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Commands;

internal sealed class DimensionLibCommandRegistrar
{
    private readonly ICoreServerAPI _api;
    private readonly DimensionLibServerService _service;

    public DimensionLibCommandRegistrar(ICoreServerAPI api, DimensionLibServerService service)
    {
        _api = api;
        _service = service;
    }

    public void Register()
    {
        _api.ChatCommands.GetOrCreate("dlib")
            .WithAlias("dimensionlib")
            .WithDescription("DimensionLib debug and maintenance commands")
            .RequiresPrivilege(Privilege.root)
            .BeginSubCommand("prepare-spike")
                .WithDescription("Create, fill, relight, and send the prototype dimension")
                .RequiresPlayer()
                .HandleWith(args =>
                {
                    var player = (IServerPlayer)args.Caller.Player;
                    return ToCommandResult(_service.PrepareDebugDimension(player));
                })
                .EndSubCommand()
            .BeginSubCommand("enter-spike")
                .WithDescription("Enter the prototype dimension")
                .RequiresPlayer()
                .HandleWith(args =>
                {
                    var player = (IServerPlayer)args.Caller.Player;
                    return ToCommandResult(_service.EnterDebugDimension(player));
                })
                .EndSubCommand()
            .BeginSubCommand("exit-spike")
                .WithDescription("Return from the prototype dimension")
                .RequiresPlayer()
                .HandleWith(args =>
                {
                    var player = (IServerPlayer)args.Caller.Player;
                    return ToCommandResult(_service.ReturnPlayer(player));
                })
                .EndSubCommand()
            .BeginSubCommand("exit")
                .WithDescription("Return from the last DimensionLib transfer")
                .RequiresPlayer()
                .HandleWith(args =>
                {
                    var player = (IServerPlayer)args.Caller.Player;
                    return ToCommandResult(_service.ReturnPlayer(player));
                })
                .EndSubCommand()
            .BeginSubCommand("create-test")
                .WithDescription("Create and prepare a built-in test dimension: overworld-opposite, nether-cavern, or vanilla-overworld")
                .WithArgs(new StringArgParser("type [dimensionId] [sizeChunks] [seed]", true))
                .HandleWith(HandleCreateTestDimension)
                .EndSubCommand()
            .BeginSubCommand("prepare")
                .WithDescription("Prepare a DimensionLib dimension without entering it")
                .WithArgs(new StringArgParser("dimensionId", true))
                .RequiresPlayer()
                .HandleWith(HandlePrepareDimension)
                .EndSubCommand()
            .BeginSubCommand("send")
                .WithDescription("Force-send a prepared DimensionLib dimension without entering it")
                .WithArgs(new StringArgParser("dimensionId", true))
                .RequiresPlayer()
                .HandleWith(HandleSendDimension)
                .EndSubCommand()
            .BeginSubCommand("generators")
                .WithDescription("List registered DimensionLib generators")
                .HandleWith(_ => TextCommandResult.Success(BuildGeneratorList()))
                .EndSubCommand()
            .BeginSubCommand("enter")
                .WithDescription("Enter a prepared DimensionLib dimension")
                .WithArgs(new StringArgParser("dimensionId", true))
                .RequiresPlayer()
                .HandleWith(HandleEnterDimension)
                .EndSubCommand()
            .BeginSubCommand("enter-player")
                .WithDescription("Send an online player into a prepared DimensionLib dimension")
                .WithArgs(new StringArgParser("playerName dimensionId", true))
                .HandleWith(HandleEnterPlayerDimension)
                .EndSubCommand()
            .BeginSubCommand("tp")
                .WithDescription("Teleport yourself to overworld or a DimensionLib dimension at absolute coordinates")
                .WithArgs(new StringArgParser("dimensionId|overworld [x y z]", true))
                .RequiresPlayer()
                .HandleWith(HandleTeleportSelf)
                .EndSubCommand()
            .BeginSubCommand("tp-player")
                .WithDescription("Teleport an online player to overworld or a DimensionLib dimension at absolute coordinates")
                .WithArgs(new StringArgParser("playerName dimensionId|overworld [x y z]", true))
                .HandleWith(HandleTeleportPlayer)
                .EndSubCommand()
            .BeginSubCommand("list")
                .WithDescription("List registered DimensionLib dimensions")
                .HandleWith(_ => TextCommandResult.Success(BuildDimensionList()))
                .EndSubCommand()
            .BeginSubCommand("inspect")
                .WithDescription("Inspect the DimensionLib dimension at your current position")
                .RequiresPlayer()
                .HandleWith(args =>
                {
                    var player = (IServerPlayer)args.Caller.Player;
                    return TextCommandResult.Success(BuildDimensionInspection(player));
                })
                .EndSubCommand()
            .BeginSubCommand("validate")
                .WithDescription("Validate a DimensionLib dimension by id, or your current dimension if omitted")
                .WithArgs(new StringArgParser("dimensionId", false))
                .RequiresPlayer()
                .HandleWith(HandleValidateDimension)
                .EndSubCommand()
            .BeginSubCommand("visual")
                .WithDescription("Live-tune the current client's DimensionLib visual settings")
                .WithArgs(new StringArgParser("status|reset|preset <id>|set <key> <value>", true))
                .HandleWith(HandleVisualTuning)
                .EndSubCommand()
            .BeginSubCommand("light-floor")
                .WithDescription("Apply an experimental non-block ambient light floor to a prepared dimension")
                .WithArgs(new StringArgParser("dimensionId level", true))
                .HandleWith(HandleLightFloor)
                .EndSubCommand()
            .BeginSubCommand("release")
                .WithDescription("Release a registered DimensionLib dimension")
                .WithArgs(new StringArgParser("dimensionId [orphan|forget|clear] confirm", true))
                .HandleWith(HandleReleaseDimension)
                .EndSubCommand();
    }

    private TextCommandResult HandleCreateTestDimension(TextCommandCallingArgs args)
    {
        var raw = (string)args[0] ?? string.Empty;
        var cmdArgs = new CmdArgs(raw);
        var testId = cmdArgs.PopWord(string.Empty);
        var second = cmdArgs.PopWord(null);
        string dimensionId = null;
        string sizeText;
        if (int.TryParse(second, out _))
        {
            sizeText = second;
        }
        else
        {
            dimensionId = second;
            sizeText = cmdArgs.PopWord(null);
        }

        var seedText = cmdArgs.PopWord(null);

        int? sizeChunks = int.TryParse(sizeText, out var parsedSize) ? parsedSize : null;
        long? seed = long.TryParse(seedText, out var parsedSeed) ? parsedSeed : null;

        var player = args.Caller.Player as IServerPlayer;
        return ToCommandResult(_service.CreateTestDimension(testId, dimensionId, sizeChunks, seed, player));
    }

    private TextCommandResult HandleEnterPlayerDimension(TextCommandCallingArgs args)
    {
        var raw = (string)args[0] ?? string.Empty;
        var cmdArgs = new CmdArgs(raw);
        var playerName = cmdArgs.PopWord(string.Empty);
        var dimensionId = cmdArgs.PopWord(string.Empty);

        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(dimensionId))
        {
            return TextCommandResult.Error("Usage: /dlib enter-player <playerName> <dimensionId>");
        }

        var player = FindOnlinePlayer(playerName);
        if (player == null)
        {
            return TextCommandResult.Error($"Online player '{playerName}' was not found.", "player-not-found");
        }

        return ToCommandResult(_service.EnterDimension(player, dimensionId));
    }

    private TextCommandResult HandleTeleportSelf(TextCommandCallingArgs args)
    {
        var raw = (string)args[0] ?? string.Empty;
        var cmdArgs = new CmdArgs(raw);
        var target = cmdArgs.PopWord(string.Empty);
        if (string.IsNullOrWhiteSpace(target))
        {
            return TextCommandResult.Error("Usage: /dlib tp <dimensionId|overworld> [x y z]");
        }

        var player = (IServerPlayer)args.Caller.Player;
        return ToCommandResult(TeleportPlayerToTarget(player, target, cmdArgs));
    }

    private TextCommandResult HandleTeleportPlayer(TextCommandCallingArgs args)
    {
        var raw = (string)args[0] ?? string.Empty;
        var cmdArgs = new CmdArgs(raw);
        var playerName = cmdArgs.PopWord(string.Empty);
        var target = cmdArgs.PopWord(string.Empty);
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(target))
        {
            return TextCommandResult.Error("Usage: /dlib tp-player <playerName> <dimensionId|overworld> [x y z]");
        }

        var player = FindOnlinePlayer(playerName);
        if (player == null)
        {
            return TextCommandResult.Error($"Online player '{playerName}' was not found.", "player-not-found");
        }

        return ToCommandResult(TeleportPlayerToTarget(player, target, cmdArgs));
    }

    private DimensionLibResult TeleportPlayerToTarget(IServerPlayer player, string target, CmdArgs cmdArgs)
    {
        if (!TryPopOptionalCoordinates(cmdArgs, out var hasCoordinates, out var x, out var y, out var z, out var errorMessage))
        {
            return DimensionLibResult.Fail(errorMessage, "invalid-coordinates");
        }

        if (IsOverworldTarget(target))
        {
            var spawn = _api.World.DefaultSpawnPosition;
            var location = new DimensionLocation
            {
                DimensionPlaneId = 0,
                X = hasCoordinates ? x : spawn.X,
                Y = hasCoordinates ? y : spawn.Y,
                Z = hasCoordinates ? z : spawn.Z,
                Yaw = player.Entity.Pos.Yaw,
                Pitch = player.Entity.Pos.Pitch,
                Roll = player.Entity.Pos.Roll,
            };

            var result = _service.TeleportToLocation(player, location);
            return result.Success
                ? DimensionLibResult.Ok($"Teleported {player.PlayerName} to overworld at {location.X:0.#}, {location.Y:0.#}, {location.Z:0.#}.")
                : result;
        }

        var lookup = _service.GetDimension(target);
        if (!lookup.Success)
        {
            return DimensionLibResult.Fail(lookup.Message, lookup.ErrorCode);
        }

        var dimension = lookup.Value;
        return _service.TeleportToDimension(player, dimension.DimensionId, new DimensionTeleportOptions
        {
            RecordReturn = false,
            X = hasCoordinates ? x : dimension.SpawnX,
            Y = hasCoordinates ? y : dimension.SpawnY,
            Z = hasCoordinates ? z : dimension.SpawnZ,
            Yaw = player.Entity.Pos.Yaw,
            Pitch = player.Entity.Pos.Pitch,
            Roll = player.Entity.Pos.Roll,
        });
    }

    private TextCommandResult HandlePrepareDimension(TextCommandCallingArgs args)
    {
        var dimensionId = ((string)args[0])?.Trim();
        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return TextCommandResult.Error("Usage: /dlib prepare <dimensionId>");
        }

        var player = (IServerPlayer)args.Caller.Player;
        var lookup = _service.GetDimension(dimensionId);
        if (!lookup.Success)
        {
            return TextCommandResult.Error(lookup.Message, lookup.ErrorCode);
        }

        var result = string.IsNullOrWhiteSpace(lookup.Value.GeneratorId)
            ? _service.PrepareDimension(dimensionId, sendToPlayer: player)
            : _service.PrepareGeneratedDimension(dimensionId, player);

        return ToCommandResult(result);
    }

    private TextCommandResult HandleSendDimension(TextCommandCallingArgs args)
    {
        var dimensionId = ((string)args[0])?.Trim();
        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return TextCommandResult.Error("Usage: /dlib send <dimensionId>");
        }

        var player = (IServerPlayer)args.Caller.Player;
        return ToCommandResult(_service.ForceSendDimension(dimensionId, player));
    }

    private TextCommandResult HandleValidateDimension(TextCommandCallingArgs args)
    {
        var dimensionId = ((string)args[0])?.Trim();
        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            var player = (IServerPlayer)args.Caller.Player;
            var lookup = _service.GetDimensionAt(player.Entity.Pos.AsBlockPos);
            if (!lookup.Success)
            {
                return TextCommandResult.Error(lookup.Message, lookup.ErrorCode);
            }

            dimensionId = lookup.Value.DimensionId;
        }

        var result = _service.ValidateDimension(dimensionId);
        return result.Success ? TextCommandResult.Success(result.Value) : TextCommandResult.Error(result.Message, result.ErrorCode);
    }

    private string BuildGeneratorList()
    {
        var generatorIds = _service.GeneratorIds.OrderBy(id => id, System.StringComparer.Ordinal).ToList();
        return generatorIds.Count == 0
            ? "No DimensionLib generators are registered."
            : string.Join("\n", generatorIds);
    }

    private TextCommandResult HandleEnterDimension(TextCommandCallingArgs args)
    {
        var dimensionId = ((string)args[0])?.Trim();
        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return TextCommandResult.Error("Usage: /dlib enter <dimensionId>");
        }

        var player = (IServerPlayer)args.Caller.Player;
        return ToCommandResult(_service.EnterDimension(player, dimensionId));
    }

    private TextCommandResult HandleReleaseDimension(TextCommandCallingArgs args)
    {
        var raw = (string)args[0] ?? string.Empty;
        var cmdArgs = new CmdArgs(raw);
        var dimensionId = cmdArgs.PopWord(string.Empty);
        var modeText = cmdArgs.PopWord("orphan");
        var confirm = cmdArgs.PopWord(string.Empty);

        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return TextCommandResult.Error("Usage: /dlib release <dimensionId> [orphan|forget|clear] confirm");
        }

        if (!TryParseReleaseMode(modeText, out var mode))
        {
            return TextCommandResult.Error("Release mode must be orphan, forget, or clear.");
        }

        if (!string.Equals(confirm, "confirm", System.StringComparison.OrdinalIgnoreCase))
        {
            return TextCommandResult.Success($"Run /dlib release {dimensionId} {modeText} confirm to release this dimension. Mode={mode}.");
        }

        return ToCommandResult(_service.ReleaseDimension(dimensionId, mode));
    }

    private TextCommandResult HandleVisualTuning(TextCommandCallingArgs args)
    {
        var raw = (string)args[0] ?? string.Empty;
        var player = args.Caller.Player as IServerPlayer;
        return ToCommandResult(_service.SendVisualTuning(player, raw));
    }

    private TextCommandResult HandleLightFloor(TextCommandCallingArgs args)
    {
        var raw = (string)args[0] ?? string.Empty;
        var cmdArgs = new CmdArgs(raw);
        var dimensionId = cmdArgs.PopWord(string.Empty);
        var levelText = cmdArgs.PopWord(string.Empty);
        if (string.IsNullOrWhiteSpace(dimensionId) || !int.TryParse(levelText, out var level))
        {
            return TextCommandResult.Error("Usage: /dlib light-floor <dimensionId> <level 0..31>");
        }

        return ToCommandResult(_service.ApplyAmbientLightFloor(dimensionId, level));
    }

    private string BuildDimensionList()
    {
        var dimensions = _service.Dimensions.OrderBy(dimension => dimension.DimensionId, System.StringComparer.Ordinal).ToList();
        if (dimensions.Count == 0)
        {
            return "No DimensionLib dimensions are registered.";
        }

        return string.Join("\n", dimensions.Select(dimension =>
            $"{dimension.DimensionId}: owner={dimension.OwnerModId}, plane={dimension.DimensionPlaneId}, chunks=({dimension.ChunkX},{dimension.ChunkZ}) {dimension.ChunkSizeX}x{dimension.ChunkSizeZ}, generator={dimension.GeneratorId ?? "none"}, visual={(dimension.VisualSettings == null ? "none" : "explicit")}, kind={dimension.Kind}, mutability={dimension.Mutability}, access={dimension.AccessPolicy}, prepared={_service.IsDimensionPrepared(dimension.DimensionId)}, orphaned={_service.IsDimensionOrphaned(dimension.DimensionId)}"));
    }

    private string BuildDimensionInspection(IServerPlayer player)
    {
        var lookup = _service.GetDimensionAt(player.Entity.Pos.AsBlockPos);
        if (!lookup.Success)
        {
            return lookup.Message;
        }

        var dimension = lookup.Value;
        return $"{dimension.DimensionId}: owner={dimension.OwnerModId}, plane={dimension.DimensionPlaneId}, chunks=({dimension.ChunkX},{dimension.ChunkZ}) {dimension.ChunkSizeX}x{dimension.ChunkSizeZ}, blocks=({dimension.MinBlockX},{dimension.MinBlockZ})..({dimension.MaxBlockX},{dimension.MaxBlockZ}), spawn=({dimension.SpawnX:0.#},{dimension.SpawnY},{dimension.SpawnZ:0.#}), generator={dimension.GeneratorId ?? "none"}, visual={(dimension.VisualSettings == null ? "none" : "explicit")}, seed={dimension.Seed}, kind={dimension.Kind}, mutability={dimension.Mutability}, access={dimension.AccessPolicy}, prepared={_service.IsDimensionPrepared(dimension.DimensionId)}, orphaned={_service.IsDimensionOrphaned(dimension.DimensionId)}";
    }

    private static bool TryParseReleaseMode(string value, out DimensionReleaseMode mode)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "orphan":
            case "markorphaned":
            case "mark-orphaned":
                mode = DimensionReleaseMode.MarkOrphaned;
                return true;
            case "forget":
            case "forgetonly":
            case "forget-only":
                mode = DimensionReleaseMode.ForgetOnly;
                return true;
            case "clear":
            case "clearblocks":
            case "clear-blocks":
                mode = DimensionReleaseMode.ClearBlocksAndForget;
                return true;
            default:
                mode = DimensionReleaseMode.MarkOrphaned;
                return false;
        }
    }

    private static bool TryPopOptionalCoordinates(CmdArgs cmdArgs, out bool hasCoordinates, out double x, out double y, out double z, out string errorMessage)
    {
        x = 0;
        y = 0;
        z = 0;
        errorMessage = string.Empty;
        var xText = cmdArgs.PopWord(null);
        if (string.IsNullOrWhiteSpace(xText))
        {
            hasCoordinates = false;
            return true;
        }

        hasCoordinates = true;
        var yText = cmdArgs.PopWord(null);
        var zText = cmdArgs.PopWord(null);
        if (string.IsNullOrWhiteSpace(yText) || string.IsNullOrWhiteSpace(zText))
        {
            errorMessage = "Coordinates must be omitted or provided as x y z.";
            return false;
        }

        if (!TryParseCoordinate(xText, out x) || !TryParseCoordinate(yText, out y) || !TryParseCoordinate(zText, out z))
        {
            errorMessage = "Coordinates must be numeric absolute x y z values.";
            return false;
        }

        return true;
    }

    private static bool TryParseCoordinate(string value, out double coordinate)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate);
    }

    private static bool IsOverworldTarget(string target)
    {
        target = (target ?? string.Empty).Trim();
        return string.Equals(target, "overworld", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, "world", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, "main", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, "0", System.StringComparison.OrdinalIgnoreCase);
    }

    private static TextCommandResult ToCommandResult(DimensionLibResult result)
    {
        return result.Success ? TextCommandResult.Success(result.Message) : TextCommandResult.Error(result.Message, result.ErrorCode);
    }

    private IServerPlayer FindOnlinePlayer(string playerNameOrUid)
    {
        return _api.World.AllOnlinePlayers
            .OfType<IServerPlayer>()
            .FirstOrDefault(player =>
                string.Equals(player.PlayerName, playerNameOrUid, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(player.PlayerUID, playerNameOrUid, System.StringComparison.OrdinalIgnoreCase));
    }
}
