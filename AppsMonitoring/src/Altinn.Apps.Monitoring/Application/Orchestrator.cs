using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Domain;
using Microsoft.Extensions.Options;

namespace Altinn.Apps.Monitoring.Application;

internal sealed class Orchestrator(
    ILogger<Orchestrator> logger,
    IOptionsMonitor<AppConfiguration> appConfiguration,
    IServiceOwnerDiscovery serviceOwnerDiscovery,
    IServiceOwnerLogsAdapter serviceOwnerLogs,
    IHostApplicationLifetime applicationLifetime,
    Repository repository,
    TimeProvider timeProvider
) : IHostedService
{
    private readonly ILogger<Orchestrator> _logger = logger;
    private readonly IOptionsMonitor<AppConfiguration> _appConfiguration = appConfiguration;
    private readonly IServiceOwnerDiscovery _serviceOwnerDiscovery = serviceOwnerDiscovery;
    private readonly IServiceOwnerLogsAdapter _serviceOwnerLogs = serviceOwnerLogs;
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
    private readonly Repository _repository = repository;
    private readonly TimeProvider _timeProvider = timeProvider;

    private Task[] _serviceOwnerThreads;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var serviceOwners = await _serviceOwnerDiscovery.Discover(cancellationToken);

        _serviceOwnerThreads = serviceOwners
            .Select(serviceOwner => ServiceOwnerThread(serviceOwner, cancellationToken))
            .ToArray();
    }

    private async Task ServiceOwnerThread(ServiceOwner serviceOwner, CancellationToken cancellationToken)
    {
        var options = _appConfiguration.CurrentValue;
        var timer = new PeriodicTimer(options.PollInterval, _timeProvider);

        try
        {
            var latestErrorTime = await _repository.GetLatestErrorGeneratedTime(serviceOwner, cancellationToken);
            var searchFrom =
                latestErrorTime
                ?? _timeProvider
                    .GetCurrentInstant()
                    .Minus(Duration.FromDays(options.SearchFromDays))
                    .Minus(Duration.FromSeconds(1));

            _logger.LogInformation("[{ServiceOwner}] starting search from {SearchFrom}", serviceOwner, searchFrom);

            do
            {
                try
                {
                    var searchTimestamp = _timeProvider.GetCurrentInstant();
                    var timeRange = searchTimestamp - searchFrom + Duration.FromMinutes(5);

                    var queries = $"""
                            AppDependencies
                            | where TimeGenerated > todatetime('{searchFrom}')
                            | where Success == false
                            | where Target startswith "platform.altinn.no"
                            | where (Name startswith "POST /storage" and Name endswith "/events" and OperationName startswith "PUT Process/NextElement");

                            AppDependencies
                            | where TimeGenerated > todatetime('{searchFrom}')
                            | where Success == false
                            | where Target startswith "platform.altinn.no"
                            | where (Name startswith "POST /events" and OperationName startswith "PUT Process/NextElement")
                            | join kind=inner AppRequests on OperationId;
                        """;

                    var tables = await _serviceOwnerLogs.Query(
                        serviceOwner,
                        queries,
                        searchFrom,
                        cancellationToken: cancellationToken
                    );

                    var totalRows = tables.Sum(t => t.Count);
                    if (totalRows == 0)
                    {
                        searchFrom = searchTimestamp.Minus(Duration.FromMinutes(1));
                        continue;
                    }

                    _logger.LogInformation(
                        "[{ServiceOwner}] found {TotalRows} rows in {TableCount} tables",
                        serviceOwner,
                        totalRows,
                        tables.Count
                    );

                    var errors = tables.SelectMany(t => t).ToArray();

                    await _repository.InsertErrors(errors, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        ex,
                        "Failed querying service owner {ServiceOwner}, trying again soon...",
                        serviceOwner
                    );
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Service owner thread cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service owner thread failed");
            _applicationLifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(_serviceOwnerThreads);
    }
}
