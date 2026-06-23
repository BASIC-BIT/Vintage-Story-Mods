using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Server;

namespace PocketDimensions;

[ProtoContract]
internal sealed class PocketDimensionsConfig
{
    [ProtoMember(1)]
    public string CreatePrivilege { get; set; } = Privilege.root;

    [ProtoMember(2)]
    public string EnterPrivilege { get; set; } = Privilege.root;

    [ProtoMember(3)]
    public string ExitPrivilege { get; set; } = Privilege.chat;

    [ProtoMember(4)]
    public string UseWaystonePrivilege { get; set; } = Privilege.chat;

    [ProtoMember(5)]
    public string UseElevatorPrivilege { get; set; } = Privilege.root;

    [ProtoMember(6)]
    public string UsePocketBlocksPrivilege { get; set; } = Privilege.root;

    [ProtoMember(7)]
    public string MutatePocketBlocksPrivilege { get; set; } = Privilege.root;

    [ProtoMember(8)]
    public string BindPrivilege { get; set; } = Privilege.root;

    [ProtoMember(9)]
    public string ReleasePrivilege { get; set; } = Privilege.root;

    [ProtoMember(10)]
    public string CreatePocketCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Privilege);

    [ProtoMember(11)]
    public string CreateLayerCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Privilege);

    [ProtoMember(12)]
    public string EditLayerCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Privilege);

    [ProtoMember(13)]
    public string DirectoryVisibilityCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Privilege);

    [ProtoMember(14)]
    public string DirectoryTeleportCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Privilege);

    [ProtoMember(15)]
    public string UseWaystoneCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Public);

    [ProtoMember(16)]
    public string UseElevatorCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Privilege);

    [ProtoMember(17)]
    public string UsePocketBlocksCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Privilege);

    [ProtoMember(18)]
    public string MutatePocketBlocksCapabilityMode { get; set; } = nameof(PocketCapabilityMode.Privilege);

    [ProtoMember(19)]
    public string ElevatorLandingMode { get; set; } = nameof(PocketElevatorLandingMode.RequireElevatorBlock);

    [ProtoMember(20)]
    public int DefaultSizeChunks { get; set; } = 3;

    [ProtoMember(21)]
    public int MaxSizeChunks { get; set; } = 16;

    [ProtoMember(22)]
    public int DefaultSpawnY { get; set; }

    [ProtoMember(23)]
    public string UnbindPrivilege { get; set; } = Privilege.root;

    [ProtoMember(24)]
    public string ConfigPrivilege { get; set; } = Privilege.root;

    [ProtoMember(25)]
    public string PlaceWaystonePrivilege { get; set; } = Privilege.chat;

    [ProtoMember(26)]
    public string BreakWaystonePrivilege { get; set; } = Privilege.chat;

    [ProtoMember(27)]
    public bool AllowWaystoneCrafting { get; set; } = true;

    [ProtoMember(28)]
    public double TeleportDelaySeconds { get; set; } = 1.0;

    [ProtoMember(29)]
    public bool EnableTeleportSounds { get; set; } = true;

    [ProtoMember(30)]
    public string TeleportStartSound { get; set; } = "sounds/effect/translocate-active";

    [ProtoMember(31)]
    public string TeleportCompleteSound { get; set; } = "sounds/effect/translocate-breakdimension";

    [ProtoMember(32)]
    public float TeleportSoundVolume { get; set; } = 0.7f;

    [ProtoMember(33)]
    public float TeleportSoundRange { get; set; } = 16f;

    [ProtoMember(34)]
    public bool PrepareChunksDuringTeleportDelay { get; set; } = true;

    [ProtoMember(35)]
    public IList<string> ReviewedConfigSettingKeys { get; set; } = new List<string>();

    [ProtoMember(36)]
    public string UseReturnPedestalPrivilege { get; set; } = Privilege.chat;

    public void Normalize()
    {
        CreatePrivilege = NormalizePrivilege(CreatePrivilege, Privilege.root);
        EnterPrivilege = NormalizePrivilege(EnterPrivilege, Privilege.root);
        ExitPrivilege = NormalizePrivilege(ExitPrivilege, Privilege.chat);
        UseWaystonePrivilege = NormalizePrivilege(UseWaystonePrivilege, Privilege.chat);
        UseElevatorPrivilege = NormalizePrivilege(UseElevatorPrivilege, UseWaystonePrivilege);
        UsePocketBlocksPrivilege = NormalizePrivilege(UsePocketBlocksPrivilege, Privilege.root);
        MutatePocketBlocksPrivilege = NormalizePrivilege(MutatePocketBlocksPrivilege, Privilege.root);
        BindPrivilege = NormalizePrivilege(BindPrivilege, Privilege.root);
        ReleasePrivilege = NormalizePrivilege(ReleasePrivilege, Privilege.root);
        UnbindPrivilege = NormalizePrivilege(UnbindPrivilege, BindPrivilege);
        ConfigPrivilege = NormalizePrivilege(ConfigPrivilege, Privilege.root);
        PlaceWaystonePrivilege = NormalizePrivilege(PlaceWaystonePrivilege, Privilege.chat);
        BreakWaystonePrivilege = NormalizePrivilege(BreakWaystonePrivilege, Privilege.chat);
        UseReturnPedestalPrivilege = NormalizePrivilege(UseReturnPedestalPrivilege, Privilege.chat);
        CreatePocketCapabilityMode = NormalizeCapabilityMode(CreatePocketCapabilityMode);
        CreateLayerCapabilityMode = NormalizeCapabilityMode(CreateLayerCapabilityMode);
        EditLayerCapabilityMode = NormalizeCapabilityMode(EditLayerCapabilityMode);
        DirectoryVisibilityCapabilityMode = NormalizeCapabilityMode(DirectoryVisibilityCapabilityMode);
        DirectoryTeleportCapabilityMode = NormalizeCapabilityMode(DirectoryTeleportCapabilityMode);
        UseWaystoneCapabilityMode = NormalizeCapabilityMode(UseWaystoneCapabilityMode);
        UseElevatorCapabilityMode = NormalizeCapabilityMode(UseElevatorCapabilityMode);
        UsePocketBlocksCapabilityMode = NormalizeCapabilityMode(UsePocketBlocksCapabilityMode);
        MutatePocketBlocksCapabilityMode = NormalizeCapabilityMode(MutatePocketBlocksCapabilityMode);
        if (!Enum.TryParse<PocketElevatorLandingMode>(ElevatorLandingMode, ignoreCase: true, out var parsed))
        {
            parsed = PocketElevatorLandingMode.RequireElevatorBlock;
        }

        ElevatorLandingMode = parsed.ToString();
        MaxSizeChunks = ClampInt(MaxSizeChunks, 1, 64);
        DefaultSizeChunks = ClampInt(DefaultSizeChunks, 1, MaxSizeChunks);
        DefaultSpawnY = Math.Max(0, DefaultSpawnY);
        TeleportDelaySeconds = ClampDouble(TeleportDelaySeconds, 0, 30);
        TeleportStartSound = TeleportStartSound?.Trim() ?? string.Empty;
        TeleportCompleteSound = TeleportCompleteSound?.Trim() ?? string.Empty;
        TeleportSoundVolume = (float)ClampDouble(TeleportSoundVolume, 0, 4);
        TeleportSoundRange = (float)ClampDouble(TeleportSoundRange, 0, 128);
        ReviewedConfigSettingKeys ??= new List<string>();
    }

    private static string NormalizePrivilege(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeCapabilityMode(string value)
    {
        return Enum.TryParse<PocketCapabilityMode>(value, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : nameof(PocketCapabilityMode.Privilege);
    }

    public PocketCapabilityMode ResolveCapabilityMode(string value)
    {
        return Enum.TryParse<PocketCapabilityMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : PocketCapabilityMode.Privilege;
    }

    public PocketElevatorLandingMode ResolveElevatorLandingMode()
    {
        return Enum.TryParse<PocketElevatorLandingMode>(ElevatorLandingMode, ignoreCase: true, out var parsed)
            ? parsed
            : PocketElevatorLandingMode.RequireElevatorBlock;
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private static double ClampDouble(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return min;
        }

        return value < min ? min : value > max ? max : value;
    }
}
