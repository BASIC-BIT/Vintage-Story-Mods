using ProtoBuf;

namespace thebasics.Configs;

[ProtoContract]
public class TeleportationConfig
{
    [ProtoMember(1)]
    public int MaxHomes { get; set; } = 3;

    [ProtoMember(2)]
    public int HomeWarmupSeconds { get; set; } = 5;

    [ProtoMember(3)]
    public int SpawnWarmupSeconds { get; set; } = 5;

    [ProtoMember(4)]
    public int TpaWarmupSeconds { get; set; } = 5;

    [ProtoMember(5)]
    public int StuckWarmupSeconds { get; set; } = 300;

    [ProtoMember(6)]
    public bool CancelWarmupOnDamage { get; set; } = true;

    [ProtoMember(7)]
    public bool CancelWarmupOnInteraction { get; set; } = true;

    [ProtoMember(8)]
    public int HomeCooldownSeconds { get; set; } = 300;

    [ProtoMember(9)]
    public int SpawnCooldownSeconds { get; set; } = 300;

    [ProtoMember(10)]
    public int StuckCooldownSeconds { get; set; } = 3600;

    [ProtoMember(11)]
    public string StuckCommandPrivilege { get; set; } = "chat";

    [ProtoMember(12)]
    public string StuckAdminNotifyPrivilege { get; set; } = "commandplayer";

    [ProtoMember(13)]
    public int StuckReminderIntervalSeconds { get; set; } = 60;

    [ProtoMember(14)]
    public string StuckBlockedByOnlinePrivilege { get; set; } = "commandplayer";

    [ProtoMember(15)]
    public int TopWarmupSeconds { get; set; } = 5;

    [ProtoMember(16)]
    public int TopCooldownSeconds { get; set; } = 300;

    [ProtoMember(17)]
    public string TopCommandPrivilege { get; set; } = "chat";

    [ProtoMember(18)]
    public int BackWarmupSeconds { get; set; } = 5;

    [ProtoMember(19)]
    public int BackCooldownSeconds { get; set; } = 300;

    [ProtoMember(20)]
    public int BackExpiresAfterSeconds { get; set; } = 300;

    [ProtoMember(21)]
    public string BackCommandPrivilege { get; set; } = "chat";

    [ProtoMember(22)]
    public bool BackRequireTemporalGear { get; set; }

    public void InitializeDefaultsIfNeeded()
    {
        MaxHomes = MaxHomes <= 0 ? 3 : MaxHomes;
        HomeWarmupSeconds = ClampNonNegative(HomeWarmupSeconds);
        SpawnWarmupSeconds = ClampNonNegative(SpawnWarmupSeconds);
        TpaWarmupSeconds = ClampNonNegative(TpaWarmupSeconds);
        StuckWarmupSeconds = ClampNonNegative(StuckWarmupSeconds);
        HomeCooldownSeconds = ClampNonNegative(HomeCooldownSeconds);
        SpawnCooldownSeconds = ClampNonNegative(SpawnCooldownSeconds);
        StuckCooldownSeconds = ClampNonNegative(StuckCooldownSeconds);
        StuckReminderIntervalSeconds = ClampNonNegative(StuckReminderIntervalSeconds);
        StuckCommandPrivilege = string.IsNullOrWhiteSpace(StuckCommandPrivilege) ? "chat" : StuckCommandPrivilege.Trim();
        StuckAdminNotifyPrivilege = string.IsNullOrWhiteSpace(StuckAdminNotifyPrivilege) ? "commandplayer" : StuckAdminNotifyPrivilege.Trim();
        StuckBlockedByOnlinePrivilege = string.IsNullOrWhiteSpace(StuckBlockedByOnlinePrivilege) ? string.Empty : StuckBlockedByOnlinePrivilege.Trim();
        TopWarmupSeconds = ClampNonNegative(TopWarmupSeconds);
        TopCooldownSeconds = ClampNonNegative(TopCooldownSeconds);
        TopCommandPrivilege = string.IsNullOrWhiteSpace(TopCommandPrivilege) ? "chat" : TopCommandPrivilege.Trim();
        BackWarmupSeconds = ClampNonNegative(BackWarmupSeconds);
        BackCooldownSeconds = ClampNonNegative(BackCooldownSeconds);
        BackExpiresAfterSeconds = ClampNonNegative(BackExpiresAfterSeconds);
        BackCommandPrivilege = string.IsNullOrWhiteSpace(BackCommandPrivilege) ? "chat" : BackCommandPrivilege.Trim();
    }

    private static int ClampNonNegative(int value)
    {
        return value < 0 ? 0 : value;
    }
}
