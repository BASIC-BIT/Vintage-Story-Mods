namespace AgentControl;

public sealed class AgentControlConfig
{
    public string PipeName { get; set; } = "vintage-story-agentcontrol";
    public int QueueCapacity { get; set; } = 4;
    public int MaxActionsPerExecution { get; set; } = 32;
    public int MaxRequestBytes { get; set; } = 65_536;
    public int MaxActionDurationMs { get; set; } = 10_000;
    public int MaxBatchDurationMs { get; set; } = 30_000;
    public int RecentChatCapacity { get; set; } = 100;
    public bool GrantMutationOnEnable { get; set; } = true;
    public string AuditContentMode { get; set; } = "redacted";
}
