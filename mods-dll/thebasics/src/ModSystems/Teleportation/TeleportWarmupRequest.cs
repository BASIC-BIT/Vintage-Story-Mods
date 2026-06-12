using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Teleportation;

public sealed class TeleportWarmupRequest
{
    public IServerPlayer Player { get; set; }

    public int WarmupSeconds { get; set; }

    public bool CancelOnDamage { get; set; } = true;

    public bool CancelOnInteraction { get; set; } = true;

    public string StartMessage { get; set; }

    public int ReminderIntervalSeconds { get; set; }

    public System.Func<int, string> ReminderMessage { get; set; }

    public System.Func<IServerPlayer, TextCommandResult> Execute { get; set; }

    public Action<IServerPlayer, string> OnCancelled { get; set; }
}
