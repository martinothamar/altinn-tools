using Altinn.Apps.Monitoring.Application;
using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Domain;

namespace Altinn.Apps.Monitoring.Tests.Application.Db;

public class RepositoryTests
{
    [Fact]
    public async Task Test_Insert_Telemetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var fixture = await HostFixture.Create(services =>
        {
            services.Configure<AppConfiguration>(config =>
            {
                config.DisableOrchestrator = true;
            });
        });

        using var _ = await fixture.Start(cancellationToken);

        var timeProvider = fixture.TimeProvider;
        var repository = fixture.Services.GetRequiredService<Repository>();

        var changes = new List<Change>();

        var query = new Query("query-name", QueryType.Traces, "query-template");
        {
            // First write from clean DB
            var start = timeProvider.GetCurrentInstant();
            TelemetryEntity[] telemetry = [TestData.GenerateTelemetryEntity(timeProvider: timeProvider)];
            var serviceOwner = ServiceOwner.Parse(telemetry[0].ServiceOwner);
            var searchFrom = start.Minus(Duration.FromMinutes(20));
            var searchTo = start.Minus(Duration.FromMinutes(10));

            var telemetryBefore = await repository.ListTelemetry(cancellationToken: cancellationToken);
            var queryStateBefore = await repository.ListQueryStates(cancellationToken: cancellationToken);

            await repository.InsertTelemetry(serviceOwner, query, searchTo, telemetry, cancellationToken);

            var telemetryAfter = await repository.ListTelemetry(cancellationToken: cancellationToken);
            var queryStateAfter = await repository.ListQueryStates(cancellationToken: cancellationToken);

            timeProvider.Advance(Duration.FromMinutes(10).ToTimeSpan());

            var end = timeProvider.GetCurrentInstant();

            changes.Add(
                new(
                    "First write from clean DB",
                    start,
                    end,
                    new State(telemetryBefore, queryStateBefore),
                    new Input(serviceOwner.Value, telemetry),
                    new State(telemetryAfter, queryStateAfter)
                )
            );
        }

        {
            // Second write with existing data (test for idempotency)
            var start = timeProvider.GetCurrentInstant();
            var telemetryBefore = await repository.ListTelemetry(cancellationToken: cancellationToken);
            var serviceOwner = ServiceOwner.Parse(telemetryBefore[0].ServiceOwner);
            var searchFrom = start.Minus(Duration.FromMinutes(25));
            var searchTo = start.Minus(Duration.FromMinutes(10));

            var queryStateBefore = await repository.ListQueryStates(cancellationToken: cancellationToken);

            var telemetry = telemetryBefore.Select(t => t with { Id = 0, TimeIngested = start }).ToArray();
            await repository.InsertTelemetry(serviceOwner, query, searchTo, telemetry, cancellationToken);

            var telemetryAfter = await repository.ListTelemetry(cancellationToken: cancellationToken);
            var queryStateAfter = await repository.ListQueryStates(cancellationToken: cancellationToken);

            timeProvider.Advance(Duration.FromMinutes(10).ToTimeSpan());

            var end = timeProvider.GetCurrentInstant();

            changes.Add(
                new(
                    "Second write with existing data (test for idempotency)",
                    start,
                    end,
                    new State(telemetryBefore, queryStateBefore),
                    new Input(serviceOwner.Value, telemetry),
                    new State(telemetryAfter, queryStateAfter)
                )
            );
        }

        {
            // Same data, different service owner
            var start = timeProvider.GetCurrentInstant();
            var telemetryBefore = await repository.ListTelemetry(cancellationToken: cancellationToken);
            var serviceOwner = ServiceOwner.Parse("sot");
            var searchFrom = start.Minus(Duration.FromMinutes(30));
            var searchTo = start.Minus(Duration.FromMinutes(10));

            var queryStateBefore = await repository.ListQueryStates(cancellationToken: cancellationToken);

            var telemetry = telemetryBefore
                .Select(t => t with { Id = 0, TimeIngested = start, ServiceOwner = serviceOwner.Value })
                .ToArray();
            await repository.InsertTelemetry(serviceOwner, query, searchTo, telemetry, cancellationToken);

            var telemetryAfter = await repository.ListTelemetry(cancellationToken: cancellationToken);
            var queryStateAfter = await repository.ListQueryStates(cancellationToken: cancellationToken);

            timeProvider.Advance(Duration.FromMinutes(10).ToTimeSpan());

            var end = timeProvider.GetCurrentInstant();

            changes.Add(
                new(
                    "Same data, different service owner (should result in write)",
                    start,
                    end,
                    new State(telemetryBefore, queryStateBefore),
                    new Input(serviceOwner.Value, telemetry),
                    new State(telemetryAfter, queryStateAfter)
                )
            );
        }

        await Verify(changes).AutoVerify().DontScrubDateTimes().DontIgnoreEmptyCollections();
    }

    private sealed record Change(
        string Desc,
        Instant Start,
        Instant End,
        State StateBefore,
        Input Input,
        State StateAfter
    );

    private sealed record State(IReadOnlyList<TelemetryEntity> Telemetry, IReadOnlyList<QueryStateEntity> Queries);

    private sealed record Input(string ServiceOwner, IReadOnlyList<TelemetryEntity> Telemetry);
}
