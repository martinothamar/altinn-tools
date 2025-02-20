namespace Altinn.Apps.Monitoring.Application;

public sealed class AppConfiguration
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(10);
    public int SearchFromDays { get; set; } = 90;

    public string SlackAccessToken { get; set; } = null!;
    public string SlackChannel { get; set; } = null!;

    public string DbConnectionString { get; set; } = null!;

    internal bool DisableOrchestrator { get; set; }

    internal TaskCompletionSource? OrchestratorStartSignal { get; set; }
}
