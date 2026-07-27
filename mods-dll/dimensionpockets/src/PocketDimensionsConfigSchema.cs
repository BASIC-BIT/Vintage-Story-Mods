using System.Collections.Generic;
using BasicConfig;

namespace PocketDimensions;

internal static class PocketDimensionsConfigSchema
{
    public const string ConfigId = "pocketdimensions";

    private static readonly BasicConfigSchema<PocketDimensionsConfig> _schema = BuildSchema();

    public static IReadOnlyList<BasicConfigSettingDefinition<PocketDimensionsConfig>> Settings => _schema.Settings;

    public static BasicConfigSchema<PocketDimensionsConfig> Build()
    {
        return _schema;
    }

    private static BasicConfigSchema<PocketDimensionsConfig> BuildSchema()
    {
        var capabilityModes = new[]
        {
            nameof(PocketCapabilityMode.Disabled),
            nameof(PocketCapabilityMode.Privilege),
            nameof(PocketCapabilityMode.OwnerOrPrivilege),
            nameof(PocketCapabilityMode.OwnerMemberOrPrivilege),
            nameof(PocketCapabilityMode.Public),
        };
        var elevatorLandingModes = new[]
        {
            nameof(PocketElevatorLandingMode.RequireElevatorBlock),
            nameof(PocketElevatorLandingMode.ClearHeadroomOnly),
            nameof(PocketElevatorLandingMode.AutoPlaceElevatorIfMissing),
        };

        return new BasicConfigSchemaBuilder<PocketDimensionsConfig>()
            .Text(Setting(nameof(PocketDimensionsConfig.CreatePrivilege), "Access", "Create privilege", "Privilege used by create and connected-space operations when their capability mode requires a privilege."), c => c.CreatePrivilege, (c, v) => c.CreatePrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.EnterPrivilege), "Access", "Enter command privilege", "Privilege required for /pocket enter and related directory teleport checks."), c => c.EnterPrivilege, (c, v) => c.EnterPrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.ExitPrivilege), "Access", "Exit command privilege", "Privilege required for /pocket exit."), c => c.ExitPrivilege, (c, v) => c.ExitPrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.ConfigPrivilege), "Access", "Config privilege", "Privilege required to open /pocket config."), c => c.ConfigPrivilege, (c, v) => c.ConfigPrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.ReleasePrivilege), "Access", "Release privilege", "Privilege required to release/orphan a pocket."), c => c.ReleasePrivilege, (c, v) => c.ReleasePrivilege = v)

            .Select(Setting(nameof(PocketDimensionsConfig.CreatePocketCapabilityMode), "Capabilities", "Create pockets", "Who can create root pocket spaces."), c => c.CreatePocketCapabilityMode, (c, v) => c.CreatePocketCapabilityMode = v, capabilityModes)
            .Select(Setting(nameof(PocketDimensionsConfig.CreateLayerCapabilityMode), "Capabilities", "Create connected spaces", "Who can create connected pocket spaces from elevators or the directory."), c => c.CreateLayerCapabilityMode, (c, v) => c.CreateLayerCapabilityMode = v, capabilityModes)
            .Select(Setting(nameof(PocketDimensionsConfig.EditLayerCapabilityMode), "Capabilities", "Edit connected spaces", "Who can edit connected pocket space metadata."), c => c.EditLayerCapabilityMode, (c, v) => c.EditLayerCapabilityMode = v, capabilityModes)
            .Select(Setting(nameof(PocketDimensionsConfig.DirectoryVisibilityCapabilityMode), "Capabilities", "Directory visibility", "Who can see pocket spaces in the directory."), c => c.DirectoryVisibilityCapabilityMode, (c, v) => c.DirectoryVisibilityCapabilityMode = v, capabilityModes)
            .Select(Setting(nameof(PocketDimensionsConfig.DirectoryTeleportCapabilityMode), "Capabilities", "Directory teleport", "Who can enter pockets from the directory."), c => c.DirectoryTeleportCapabilityMode, (c, v) => c.DirectoryTeleportCapabilityMode = v, capabilityModes)
            .Select(Setting(nameof(PocketDimensionsConfig.UseWaystoneCapabilityMode), "Capabilities", "Use Waystones", "Who can right-click bound Pocket Waystones."), c => c.UseWaystoneCapabilityMode, (c, v) => c.UseWaystoneCapabilityMode = v, capabilityModes)
            .Select(Setting(nameof(PocketDimensionsConfig.UseElevatorCapabilityMode), "Capabilities", "Use elevators", "Who can use Pocket Elevators."), c => c.UseElevatorCapabilityMode, (c, v) => c.UseElevatorCapabilityMode = v, capabilityModes)
            .Select(Setting(nameof(PocketDimensionsConfig.UsePocketBlocksCapabilityMode), "Capabilities", "Use pocket blocks", "Who can interact with blocks inside managed pockets."), c => c.UsePocketBlocksCapabilityMode, (c, v) => c.UsePocketBlocksCapabilityMode = v, capabilityModes)
            .Select(Setting(nameof(PocketDimensionsConfig.MutatePocketBlocksCapabilityMode), "Capabilities", "Modify pocket blocks", "Who can place or break non-protected blocks inside managed pockets."), c => c.MutatePocketBlocksCapabilityMode, (c, v) => c.MutatePocketBlocksCapabilityMode = v, capabilityModes)

            .Text(Setting(nameof(PocketDimensionsConfig.UseWaystonePrivilege), "Waystones", "Waystone use privilege", "Privilege required by Waystone capability checks when not public or owner/member allowed."), c => c.UseWaystonePrivilege, (c, v) => c.UseWaystonePrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.UseReturnPedestalPrivilege), "Waystones", "Return pedestal privilege", "Privilege required to use return pedestals when restricted by future policy."), c => c.UseReturnPedestalPrivilege, (c, v) => c.UseReturnPedestalPrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.BindPrivilege), "Waystones", "Bind privilege", "Privilege required to bind Waystones."), c => c.BindPrivilege, (c, v) => c.BindPrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.UnbindPrivilege), "Waystones", "Unbind privilege", "Privilege required to unbind Waystones."), c => c.UnbindPrivilege, (c, v) => c.UnbindPrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.PlaceWaystonePrivilege), "Waystones", "Place privilege", "Privilege required to place Pocket Waystones."), c => c.PlaceWaystonePrivilege, (c, v) => c.PlaceWaystonePrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.BreakWaystonePrivilege), "Waystones", "Break privilege", "Privilege required to break unbound Pocket Waystones."), c => c.BreakWaystonePrivilege, (c, v) => c.BreakWaystonePrivilege = v)
            .Bool(Setting(nameof(PocketDimensionsConfig.AllowWaystoneCrafting), "Waystones", "Enable Waystone crafting", "Enable the built-in Pocket Waystone recipe. Requires restart.", BasicConfigReloadBehavior.RestartRequired), c => c.AllowWaystoneCrafting, (c, v) => c.AllowWaystoneCrafting = v)

            .Int(Setting(nameof(PocketDimensionsConfig.DefaultSizeChunks), "Dimensions", "Default size chunks", "Default pocket size in chunks."), c => c.DefaultSizeChunks, (c, v) => c.DefaultSizeChunks = v, 1, 64)
            .Int(Setting(nameof(PocketDimensionsConfig.MaxSizeChunks), "Dimensions", "Max size chunks", "Maximum pocket size in chunks."), c => c.MaxSizeChunks, (c, v) => c.MaxSizeChunks = v, 1, 64)
            .Int(Setting(nameof(PocketDimensionsConfig.DefaultSpawnY), "Dimensions", "Default spawn Y", "Default pocket spawn Y. 0 uses half world height."), c => c.DefaultSpawnY, (c, v) => c.DefaultSpawnY = v, 0, 4096)

            .Select(Setting(nameof(PocketDimensionsConfig.ElevatorLandingMode), "Elevators", "Landing mode", "How elevator travel handles missing target elevators."), c => c.ElevatorLandingMode, (c, v) => c.ElevatorLandingMode = v, elevatorLandingModes)
            .Text(Setting(nameof(PocketDimensionsConfig.UseElevatorPrivilege), "Elevators", "Elevator use privilege", "Privilege required by elevator capability checks when not public or owner/member allowed."), c => c.UseElevatorPrivilege, (c, v) => c.UseElevatorPrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.UsePocketBlocksPrivilege), "Pocket Blocks", "Use blocks privilege", "Privilege required by in-pocket block interaction checks."), c => c.UsePocketBlocksPrivilege, (c, v) => c.UsePocketBlocksPrivilege = v)
            .Text(Setting(nameof(PocketDimensionsConfig.MutatePocketBlocksPrivilege), "Pocket Blocks", "Modify blocks privilege", "Privilege required by in-pocket block mutation checks."), c => c.MutatePocketBlocksPrivilege, (c, v) => c.MutatePocketBlocksPrivilege = v)

            .Decimal(Setting(nameof(PocketDimensionsConfig.TeleportDelaySeconds), "Teleport", "Teleport delay seconds", "Seconds between activation and teleport."), c => c.TeleportDelaySeconds, (c, v) => c.TeleportDelaySeconds = v, 0, 30)
            .Bool(Setting(nameof(PocketDimensionsConfig.EnableTeleportSounds), "Teleport", "Enable teleport sounds", "Play activation/completion sounds for Waystone and pedestal transfers."), c => c.EnableTeleportSounds, (c, v) => c.EnableTeleportSounds = v)
            .Text(Setting(nameof(PocketDimensionsConfig.TeleportStartSound), "Teleport", "Start sound", "Sound played when teleport activation starts. Empty disables the start sound."), c => c.TeleportStartSound, (c, v) => c.TeleportStartSound = v)
            .Text(Setting(nameof(PocketDimensionsConfig.TeleportCompleteSound), "Teleport", "Complete sound", "Sound played after a successful teleport. Empty disables the completion sound."), c => c.TeleportCompleteSound, (c, v) => c.TeleportCompleteSound = v)
            .Decimal(Setting(nameof(PocketDimensionsConfig.TeleportSoundVolume), "Teleport", "Sound volume", "Teleport sound volume multiplier."), c => c.TeleportSoundVolume, (c, v) => c.TeleportSoundVolume = (float)v, 0, 4)
            .Decimal(Setting(nameof(PocketDimensionsConfig.TeleportSoundRange), "Teleport", "Sound range", "Approximate teleport sound audible range in blocks."), c => c.TeleportSoundRange, (c, v) => c.TeleportSoundRange = (float)v, 0, 128)
            .Bool(Setting(nameof(PocketDimensionsConfig.PrepareChunksDuringTeleportDelay), "Teleport", "Prepare during delay", "Prepare destination infrastructure before the delay starts when possible."), c => c.PrepareChunksDuringTeleportDelay, (c, v) => c.PrepareChunksDuringTeleportDelay = v)
            .ValidateWith(Validate)
            .Build();
    }

    private static BasicConfigSettingMetadata Setting(string key, string group, string label, string description, BasicConfigReloadBehavior reloadBehavior = BasicConfigReloadBehavior.Live)
    {
        return new BasicConfigSettingMetadata(key, group, label, description, reloadBehavior);
    }

    private static IReadOnlyList<string> Validate(PocketDimensionsConfig config)
    {
        var errors = new List<string>();
        if (config.DefaultSizeChunks > config.MaxSizeChunks)
        {
            errors.Add("Default size chunks must be less than or equal to max size chunks.");
        }

        return errors;
    }
}
