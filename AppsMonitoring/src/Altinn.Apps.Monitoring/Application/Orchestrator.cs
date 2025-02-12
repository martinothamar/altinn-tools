using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Domain;
using Microsoft.Extensions.Options;
using NodaTime;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Apps.Monitoring.Application;

internal sealed class Orchestrator(
    ILogger<Orchestrator> logger,
    IOptionsMonitor<AppConfiguration> appConfiguration,
    IServiceOwnerDiscovery serviceOwnerDiscovery,
    IServiceOwnerLogsAdapter serviceOwnerLogs,
    IHostApplicationLifetime applicationLifetime,
    NpgsqlDataSource dataSource
) : IHostedService
{
    private readonly ILogger<Orchestrator> _logger = logger;
    private readonly IOptionsMonitor<AppConfiguration> _appConfiguration = appConfiguration;
    private readonly IServiceOwnerDiscovery _serviceOwnerDiscovery = serviceOwnerDiscovery;
    private readonly IServiceOwnerLogsAdapter _serviceOwnerLogs = serviceOwnerLogs;
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
    private readonly NpgsqlDataSource _dataSource = dataSource;

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
        var timer = new PeriodicTimer(options.PollInterval);
        try
        {
            do
            {
                try
                {
                    List<ErrorEntity> errors = [];

                    await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

                    await using var import = await connection.BeginBinaryImportAsync(
                        "COPY monitoring.errors (service_owner, app_name, app_version, time_generated, time_ingested, data) FROM STDIN (FORMAT binary)",
                        cancellationToken
                    );

                    foreach (var error in errors)
                    {
                        await import.StartRowAsync(cancellationToken);
                        await import.WriteAsync(error.ServiceOwner, NpgsqlDbType.Text, cancellationToken);
                        await import.WriteAsync(error.AppName, NpgsqlDbType.Text, cancellationToken);
                        await import.WriteAsync(error.AppVersion, NpgsqlDbType.Text, cancellationToken);
                        await import.WriteAsync(error.TimeGenerated, NpgsqlDbType.TimestampTz, cancellationToken);
                        await import.WriteAsync(error.TimeIngested, NpgsqlDbType.TimestampTz, cancellationToken);
                        await import.WriteAsync(error.Data, NpgsqlDbType.Jsonb, cancellationToken);
                    }

                    await import.CompleteAsync(cancellationToken);
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
