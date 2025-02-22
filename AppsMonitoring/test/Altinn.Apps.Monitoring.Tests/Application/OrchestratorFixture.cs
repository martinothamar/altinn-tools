using Altinn.Apps.Monitoring.Application;
using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Domain;
using Microsoft.Extensions.Options;

namespace Altinn.Apps.Monitoring.Tests.Application;

internal sealed record FakeConfig
{
    public TimeSpan Latency { get; set; }
    public SemaphoreSlim AdapterSemaphore { get; set; } = null!;
    public Func<IServiceProvider, IReadOnlyList<ServiceOwner>>? ServiceOwnersDiscovery { get; set; }
    public Func<IServiceProvider, TelemetryEntity[]>? TelemetryGenerator { get; set; }
}

internal sealed record OrchestratorFixture(
    HostFixture HostFixture,
    TaskCompletionSource StartSignal,
    SemaphoreSlim AdapterSemaphore,
    TimeSpan PollInterval,
    TimeSpan Latency,
    IReadOnlyList<Query> Queries,
    CancellationToken CancellationToken
) : IAsyncDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;

    public async ValueTask DisposeAsync()
    {
        await HostFixture.DisposeAsync();
        AdapterSemaphore.Dispose();
        _cancellationTokenSource?.Dispose();
    }

    public static async Task<OrchestratorFixture> Create(Action<IServiceCollection, HostFixture> configureServices)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SemaphoreSlim? adapterSemaphore = null;
        HostFixture? hostFixture = null;
        CancellationTokenSource? cancellationTokenSource = null;
        try
        {
            adapterSemaphore = new SemaphoreSlim(0);
            var pollInterval = TimeSpan.FromMinutes(10);
            var latency = TimeSpan.FromSeconds(5);
            hostFixture = await HostFixture.Create(
                (services, fixture) =>
                {
                    services.Configure<FakeConfig>(config =>
                    {
                        config.Latency = latency;
                        config.AdapterSemaphore = adapterSemaphore;
                    });
                    services.AddSingleton<IServiceOwnerDiscovery, FakeServiceOwnerDiscovery>();
                    services.AddSingleton<IQueryLoader, FakeQueryLoader>();
                    services.AddSingleton<FakeTelemetryAdapter>();
                    services.AddSingleton<IServiceOwnerLogsAdapter>(sp =>
                        sp.GetRequiredService<FakeTelemetryAdapter>()
                    );
                    services.AddSingleton<IServiceOwnerTraceAdapter>(sp =>
                        sp.GetRequiredService<FakeTelemetryAdapter>()
                    );
                    services.AddSingleton<IServiceOwnerMetricsAdapter>(sp =>
                        sp.GetRequiredService<FakeTelemetryAdapter>()
                    );

                    services.Configure<AppConfiguration>(options =>
                    {
                        options.DisableOrchestrator = false;
                        options.OrchestratorStartSignal = startSignal;
                        options.PollInterval = pollInterval;
                    });

                    configureServices(services, fixture);
                }
            );
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                hostFixture.Lifetime.ApplicationStopping
            );
            // Since we are trying to make tests more deterministic and fast,
            // we are doing a lot of thread synchronization which is pretty error prone
            // So to make sure we never just hang indefinitely, we are setting a timeout on all
            // tests using this fixture
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));
            cancellationToken = cancellationTokenSource.Token;

            using var _ = await hostFixture.Start(cancellationToken);

            var queryLoader = hostFixture.QueryLoader;

            var queries = await queryLoader.Load(cancellationToken);

            var fixture = new OrchestratorFixture(
                hostFixture,
                startSignal,
                adapterSemaphore,
                pollInterval,
                latency,
                queries,
                cancellationToken
            );
            fixture._cancellationTokenSource = cancellationTokenSource;
            return fixture;
        }
        catch
        {
            cancellationTokenSource?.Dispose();
            if (hostFixture is not null)
                await hostFixture.DisposeAsync();
            adapterSemaphore?.Dispose();
            throw;
        }
    }

    private sealed class FakeServiceOwnerDiscovery(IOptions<FakeConfig> config, IServiceProvider serviceProvider)
        : IServiceOwnerDiscovery
    {
        private readonly FakeConfig _config = config.Value;
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public ValueTask<IReadOnlyList<ServiceOwner>> Discover(CancellationToken cancellationToken)
        {
            return new(
                _config.ServiceOwnersDiscovery is not null ? _config.ServiceOwnersDiscovery(_serviceProvider) : []
            );
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

    private sealed class FakeTelemetryAdapter
        : IServiceOwnerLogsAdapter,
            IServiceOwnerTraceAdapter,
            IServiceOwnerMetricsAdapter
    {
        private readonly TimeSpan _latency;
        private readonly TimeProvider _timeProvider;
        private readonly SemaphoreSlim _adapterSemaphore;

        public FakeTelemetryAdapter(
            IServiceProvider serviceProvider,
            TimeProvider timeProvider,
            IOptions<FakeConfig> fakeConfig
        )
        {
            _latency = fakeConfig.Value.Latency;
            _timeProvider = timeProvider;
            _adapterSemaphore = fakeConfig.Value.AdapterSemaphore;

            _telemetry = fakeConfig.Value.TelemetryGenerator is not null
                ? fakeConfig.Value.TelemetryGenerator(serviceProvider)
                : [];
        }

        private readonly TelemetryEntity[] _telemetry;

        public async ValueTask<IReadOnlyList<IReadOnlyList<TelemetryEntity>>> Query(
            ServiceOwner serviceOwner,
            Query query,
            Instant from,
            Instant to,
            CancellationToken cancellationToken
        )
        {
            // Signal to notify the test that the adapter has been called
            // and that time can advance
            _adapterSemaphore.Release();
            await Task.Delay(_latency, _timeProvider, cancellationToken);
            switch (query.Type)
            {
                case QueryType.Traces:
                {
                    var table = _telemetry
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
