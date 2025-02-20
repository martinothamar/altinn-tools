using Altinn.Apps.Monitoring.Application;
using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Domain;
using Altinn.Apps.Monitoring.Tests.Application.Db;
using Microsoft.Extensions.Options;

namespace Altinn.Apps.Monitoring.Tests.Application;

public class OrchestratorTests
{
    public sealed record FakeConfig(TimeSpan Latency, bool TwoServiceOwners);

    private static async Task<(
        HostFixture Fixture,
        TaskCompletionSource StartSignal,
        TimeSpan PollInterval,
        TimeSpan Latency,
        IReadOnlyList<Query> Queries,
        CancellationToken CancellationToken
    )> CreateFixture<TAdapter>(bool twoServiceOwners = false)
        where TAdapter : class, IServiceOwnerLogsAdapter, IServiceOwnerTraceAdapter, IServiceOwnerMetricsAdapter
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pollInterval = TimeSpan.FromMinutes(10);
        var latency = TimeSpan.FromSeconds(5);
        var fixture = await HostFixture.Create(services =>
        {
            services.AddSingleton<FakeConfig>(_ => new(latency, twoServiceOwners));
            services.AddSingleton<IServiceOwnerDiscovery, FakeServiceOwnerDiscovery>();
            services.AddSingleton<IQueryLoader, FakeQueryLoader>();
            services.AddSingleton<TAdapter>();
            services.AddSingleton<IServiceOwnerLogsAdapter>(sp => sp.GetRequiredService<TAdapter>());
            services.AddSingleton<IServiceOwnerTraceAdapter>(sp => sp.GetRequiredService<TAdapter>());
            services.AddSingleton<IServiceOwnerMetricsAdapter>(sp => sp.GetRequiredService<TAdapter>());

            services.Configure<AppConfiguration>(options =>
            {
                options.OrchestratorStartSignal = startSignal;
                options.PollInterval = pollInterval;
            });
        });

        using var _ = await fixture.Start(cancellationToken);

        var queryLoader = fixture.QueryLoader;

        var queries = await queryLoader.Load(cancellationToken);

        return (fixture, startSignal, pollInterval, latency, queries, cancellationToken);
    }

    private static async Task<(
        IReadOnlyList<TelemetryEntity> Telemetry,
        IReadOnlyList<QueryStateEntity> Queries
    )> GetState(Repository repository, CancellationToken cancellationToken)
    {
        var telemetry = await repository.ListTelemetry(cancellationToken: cancellationToken);
        var queries = await repository.ListQueryStates(cancellationToken: cancellationToken);
        return (telemetry, queries);
    }

    private static Change NewChange(
        string desc,
        Instant start,
        Instant end,
        (IReadOnlyList<TelemetryEntity> Telemetry, IReadOnlyList<QueryStateEntity> Queries) stateBefore,
        IReadOnlyList<ServiceOwnerQueryResult> inputTelemetry,
        (IReadOnlyList<TelemetryEntity> Telemetry, IReadOnlyList<QueryStateEntity> Queries) stateAfter
    )
    {
        // Reset data to make snapshots a little less noisy
        var telemetryBefore = stateBefore.Telemetry.Select(t => t with { Data = null! }).ToArray();
        var telemetryAfter = stateAfter.Telemetry.Select(t => t with { Data = null! }).ToArray();
        var input = inputTelemetry
            .Select(r =>
            {
                var telemetry = r.Telemetry.Select(t => t with { Data = null! }).ToArray();
                return r with { Telemetry = telemetry };
            })
            .ToArray();
        return new Change(
            desc,
            start,
            end,
            new State(telemetryBefore, stateBefore.Queries),
            new(input),
            new State(telemetryAfter, stateAfter.Queries)
        );
    }

    [Fact]
    public async Task Test_Query_Progression_No_Items()
    {
        var (fixture, startSignal, pollInterval, latency, queries, cancellationToken) =
            await CreateFixture<FakeEmptyTelemetryAdapter>(twoServiceOwners: true);
        var timeProvider = fixture.TimeProvider;
        var repository = fixture.Repository;
        var results = fixture.Orchestrator.Results;

        var changes = new List<Change>();
        IReadOnlyList<TelemetryEntity> telemetryAfter;
        IReadOnlyList<QueryStateEntity> queryStateAfter;
        var total = queries.Count * 2; // Two service owners
        {
            // Initial loop iterations (discovery and querying)
            var start = timeProvider.GetCurrentInstant();
            var (telemetryBefore, queryStateBefore) = await GetState(repository, cancellationToken);
            startSignal.SetResult();

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken); // Let all threads reach query delay
            timeProvider.Advance(latency * queries.Count); // Let adapters progress

            List<ServiceOwnerQueryResult> queryResults = new();
            for (int i = 0; i < total; i++)
            {
                var result = await results.ReadAsync(cancellationToken);
                queryResults.Add(result);
            }

            (telemetryAfter, queryStateAfter) = await GetState(repository, cancellationToken);
            var end = timeProvider.GetCurrentInstant();
            changes.Add(
                NewChange(
                    "Iteration 1 - no records",
                    start,
                    end,
                    (telemetryBefore, queryStateBefore),
                    queryResults,
                    (telemetryAfter, queryStateAfter)
                )
            );
        }

        {
            timeProvider.Advance(pollInterval - (latency * queries.Count));
            var start = timeProvider.GetCurrentInstant();
            var (telemetryBefore, queryStateBefore) = (telemetryAfter, queryStateAfter);

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken); // Let all threads reach query delay
            timeProvider.Advance(latency * queries.Count); // Let adapters progress

            List<ServiceOwnerQueryResult> queryResults = new();
            for (int i = 0; i < total; i++)
            {
                var result = await results.ReadAsync(cancellationToken);
                queryResults.Add(result);
            }

            (telemetryAfter, queryStateAfter) = await GetState(repository, cancellationToken);
            var end = timeProvider.GetCurrentInstant();
            changes.Add(
                NewChange(
                    "Iteration 2 - no records",
                    start,
                    end,
                    (telemetryBefore, queryStateBefore),
                    queryResults,
                    (telemetryAfter, queryStateAfter)
                )
            );
        }

        await Verify(changes).AutoVerify().DontScrubDateTimes().DontIgnoreEmptyCollections();
    }

    [Fact]
    public async Task Test_Query_Progression_With_Items()
    {
        var (fixture, startSignal, pollInterval, latency, queries, cancellationToken) =
            await CreateFixture<FakeTelemetryAdapter>(twoServiceOwners: false);
        var timeProvider = fixture.TimeProvider;
        var repository = fixture.Repository;
        var results = fixture.Orchestrator.Results;

        var changes = new List<Change>();
        IReadOnlyList<TelemetryEntity> telemetryAfter;
        IReadOnlyList<QueryStateEntity> queryStateAfter;
        var total = queries.Count; // One service owners
        {
            // Initial loop iterations (discovery and querying)
            var start = timeProvider.GetCurrentInstant();
            var (telemetryBefore, queryStateBefore) = await GetState(repository, cancellationToken);
            startSignal.SetResult();

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken); // Let all threads reach query delay
            timeProvider.Advance(latency * queries.Count); // Let adapters progress

            List<ServiceOwnerQueryResult> queryResults = new();
            for (int i = 0; i < total; i++)
            {
                var result = await results.ReadAsync(cancellationToken);
                queryResults.Add(result);
            }

            (telemetryAfter, queryStateAfter) = await GetState(repository, cancellationToken);
            var end = timeProvider.GetCurrentInstant();
            changes.Add(
                NewChange(
                    "Iteration 1 - 2 records",
                    start,
                    end,
                    (telemetryBefore, queryStateBefore),
                    queryResults,
                    (telemetryAfter, queryStateAfter)
                )
            );
        }

        {
            timeProvider.Advance(pollInterval - (latency * queries.Count));
            var start = timeProvider.GetCurrentInstant();
            var (telemetryBefore, queryStateBefore) = (telemetryAfter, queryStateAfter);

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken); // Let all threads reach query delay
            timeProvider.Advance(latency * queries.Count); // Let adapters progress

            List<ServiceOwnerQueryResult> queryResults = new();
            for (int i = 0; i < total; i++)
            {
                var result = await results.ReadAsync(cancellationToken);
                queryResults.Add(result);
            }

            (telemetryAfter, queryStateAfter) = await GetState(repository, cancellationToken);
            var end = timeProvider.GetCurrentInstant();
            changes.Add(
                NewChange(
                    "Iteration 2 - 2 records",
                    start,
                    end,
                    (telemetryBefore, queryStateBefore),
                    queryResults,
                    (telemetryAfter, queryStateAfter)
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

    private sealed record Input(IReadOnlyList<ServiceOwnerQueryResult> ReportedResults);

    private sealed class FakeServiceOwnerDiscovery(FakeConfig config) : IServiceOwnerDiscovery
    {
        private readonly bool _twoServiceOwners = config.TwoServiceOwners;

        public ValueTask<IReadOnlyList<ServiceOwner>> Discover(CancellationToken cancellationToken)
        {
            IReadOnlyList<ServiceOwner> serviceOwners = _twoServiceOwners
                ? [ServiceOwner.Parse("one"), ServiceOwner.Parse("two")]
                : [ServiceOwner.Parse("one")];
            return new(serviceOwners);
        }
    }

    private sealed class FakeQueryLoader : IQueryLoader
    {
        public ValueTask<IReadOnlyList<Query>> Load(CancellationToken cancellationToken)
        {
            IReadOnlyList<Query> queries = [new Query("query", QueryType.Traces, "template-{searchFrom}-{searchTo}")];
            return new(queries);
        }
    }

    private sealed class FakeEmptyTelemetryAdapter(FakeConfig config, TimeProvider timeProvider)
        : IServiceOwnerLogsAdapter,
            IServiceOwnerTraceAdapter,
            IServiceOwnerMetricsAdapter
    {
        private readonly TimeSpan _latency = config.Latency;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async ValueTask<IReadOnlyList<IReadOnlyList<TelemetryEntity>>> Query(
            ServiceOwner serviceOwner,
            Query query,
            Instant from,
            Instant to,
            CancellationToken cancellationToken
        )
        {
            await Task.Delay(_latency, _timeProvider, cancellationToken);
            return [];
        }
    }

    private sealed class FakeTelemetryAdapter
        : IServiceOwnerLogsAdapter,
            IServiceOwnerTraceAdapter,
            IServiceOwnerMetricsAdapter
    {
        private readonly TimeSpan _latency;
        private readonly TimeProvider _timeProvider;

        public FakeTelemetryAdapter(IOptions<AppConfiguration> config, TimeProvider timeProvider, FakeConfig fakeConfig)
        {
            long id = 0;
            var options = config.Value;
            _latency = fakeConfig.Latency;
            _timeProvider = timeProvider;

            var shouldStartFromExclusive = timeProvider
                .GetCurrentInstant()
                .Minus(Duration.FromDays(options.SearchFromDays));
            var shouldEndAtInclusive = timeProvider.GetCurrentInstant().Minus(Duration.FromMinutes(10));

            TelemetryEntity GenerateTrace(string serviceOwner, Instant timeGenerated) =>
                TestData.GenerateTelemetryEntity(
                    extId: $"{id++}",
                    serviceOwner: serviceOwner,
                    timeGenerated: timeGenerated,
                    timeIngested: Instant.MinValue,
                    dataGenerator: () => TestData.GenerateTelemetryTraceData(),
                    timeProvider: timeProvider
                );

            var secondIterationStartExclusive = shouldEndAtInclusive;
            var secondIterationEndInclusive = secondIterationStartExclusive.Plus(Duration.FromMinutes(10));

            _traces =
            [
                GenerateTrace("one", shouldStartFromExclusive.Minus(Duration.FromSeconds(1))), // This should be outside range
                // 1. Results for the first iteration of queries for "one"
                GenerateTrace("one", shouldStartFromExclusive), // This should be just within range
                GenerateTrace("one", shouldEndAtInclusive), // We should include now - 10 minuts
                // 2. Results for the second iteration of queries for "one"
                GenerateTrace("one", secondIterationStartExclusive.Plus(Duration.FromSeconds(1))), // This should be outside range for first iteration, but included in the second
                GenerateTrace("one", secondIterationEndInclusive), // This should be just within range
            ];
        }

        private readonly TelemetryEntity[] _traces;

        public async ValueTask<IReadOnlyList<IReadOnlyList<TelemetryEntity>>> Query(
            ServiceOwner serviceOwner,
            Query query,
            Instant from,
            Instant to,
            CancellationToken cancellationToken
        )
        {
            await Task.Delay(_latency, _timeProvider, cancellationToken);
            switch (query.Type)
            {
                case QueryType.Traces:
                {
                    var table = _traces
                        .Where(t =>
                            t.ServiceOwner == serviceOwner.Value && t.TimeGenerated > from && t.TimeGenerated <= to
                        )
                        .ToArray();
                    return [table];
                }
                default:
                    throw new Exception("Invalid query type: " + query.Type);
            }
        }
    }
}
