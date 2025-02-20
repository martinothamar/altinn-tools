namespace Altinn.Apps.Monitoring.Application.Db;

public enum Subscriber
{
    Alerter,
}

public sealed record TelemetrySubscriptionEntity
{
    public required Subscriber Subscriber { get; init; }

    public required long Offset { get; init; }
}
