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
    public int StuckWarmupSeconds { get; set; } = 60;

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
        StuckCommandPrivilege = string.IsNullOrWhiteSpace(StuckCommandPrivilege) ? "chat" : StuckCommandPrivilege.Trim();
        StuckAdminNotifyPrivilege = string.IsNullOrWhiteSpace(StuckAdminNotifyPrivilege) ? "commandplayer" : StuckAdminNotifyPrivilege.Trim();
    }

    private static int ClampNonNegative(int value)
    {
        return value < 0 ? 0 : value;
    }
}
