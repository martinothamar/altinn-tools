namespace Altinn.Apps.Monitoring.Application.Db;

public enum AlertState
{
    Pending,
    Alerted,
    Mitigated,
}

public sealed record AlertEntity
{
    public required long Id { get; init; }

    public required AlertState State { get; init; }

    public required long TelemetryId { get; init; }

    public required string? ExtId { get; init; }
}
