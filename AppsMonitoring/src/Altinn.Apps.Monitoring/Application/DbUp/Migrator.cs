using System.Data;
using System.Reflection;
using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Options;

namespace Altinn.Apps.Monitoring.Application.DbUp;

internal sealed class Migrator(ILogger<Migrator> logger, IOptions<AppConfiguration> appConfiguration) : IHostedService
{
    private readonly ILogger<Migrator> _logger = logger;
    private readonly AppConfiguration _appConfiguration = appConfiguration.Value;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = _appConfiguration.DbConnectionString;
            var upgrader = DeployChanges
                .To.PostgresqlDatabase(connectionString)
                .WithScriptsAndCodeEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .LogTo(_logger)
                .WithTransaction()
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                _logger.LogError(
                    result.Error,
                    "Failed to upgrade database, using script: {ErrorScript}",
                    result.ErrorScript.Name
                );
                throw new Exception("Failed to upgrade database", result.Error);
            }

            foreach (var script in result.Scripts)
                _logger.LogInformation("Executed script: {Script}", script.Name);

            _logger.LogInformation("Database upgrade successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed seeding database");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class Script0001Initial : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        return """
                CREATE TABLE monitoring.telemetry (
                    id BIGSERIAL PRIMARY KEY,
                    ext_id TEXT NOT NULL,
                    service_owner TEXT NOT NULL,
                    app_name TEXT NOT NULL,
                    app_version TEXT NOT NULL,
                    time_generated TIMESTAMPTZ NOT NULL,
                    time_ingested TIMESTAMPTZ NOT NULL,
                    dupe_count BIGINT NOT NULL,
                    seeded BOOLEAN NOT NULL,
                    data JSONB NOT NULL,
                    UNIQUE (service_owner, ext_id)
                );

                CREATE TABLE monitoring.queries (
                    id BIGSERIAL PRIMARY KEY,
                    service_owner TEXT NOT NULL,
                    name TEXT NOT NULL,
                    hash TEXT NOT NULL,
                    queried_until TIMESTAMPTZ NOT NULL,
                    UNIQUE (service_owner, hash)
                );
            """;
    }
}
