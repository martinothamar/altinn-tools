using Altinn.Apps.Monitoring.Application.Azure;
using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Application.DbUp;
using Altinn.Apps.Monitoring.Application.Slack;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OpenTelemetry;

namespace Altinn.Apps.Monitoring.Application;

internal static class DIExtensions
{
    public static IHostApplicationBuilder AddApplication(this IHostApplicationBuilder builder)
    {
        builder.AddConfig();
        builder.AddOtel();

        builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        // Hosted service registration
        // 1. Migrate db
        builder.Services.AddHostedService<Migrator>();
        // 2. Seed db
        builder.Services.TryAddSingleton<Seeder>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<Seeder>());
        // 3. Run the orchestrator (main loop)
        builder.Services.TryAddSingleton<Orchestrator>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<Orchestrator>());
        // 4. Slack alerting
        builder.Services.TryAddSingleton<IAlerter, SlackAlerter>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<IAlerter>());

        // Database services
        var connString = builder.Configuration.GetSection(nameof(AppConfiguration))[
            nameof(AppConfiguration.DbConnectionString)
        ];
        if (string.IsNullOrWhiteSpace(connString))
            throw new InvalidOperationException("Missing connection string in configuration");
        builder.Services.AddNpgsqlDataSource(
            connString,
            (sp, builder) =>
                builder
                    .UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                    .EnableDynamicJson()
                    .ConfigureJsonOptions(Db.Config.JsonOptions)
                    .UseNodaTime()
                    .ConfigureTracing(o => { })
        );
        builder.Services.TryAddSingleton<Repository>();
        builder.Services.TryAddSingleton<DistributedLocking>();

        // Azure services/infra
        builder.Services.AddHybridCache();
        builder.Services.TryAddSingleton<AzureClients>();
        builder.Services.TryAddSingleton<AzureServiceOwnerResources>();
        builder.Services.TryAddSingleton<IServiceOwnerDiscovery, AzureServiceOwnerDiscovery>();
        builder.Services.TryAddSingleton<AzureServiceOwnerMonitorAdapter>();
        builder.Services.TryAddSingleton<IServiceOwnerLogsAdapter>(sp =>
            sp.GetRequiredService<AzureServiceOwnerMonitorAdapter>()
        );
        builder.Services.TryAddSingleton<IServiceOwnerTraceAdapter>(sp =>
            sp.GetRequiredService<AzureServiceOwnerMonitorAdapter>()
        );
        builder.Services.TryAddSingleton<IServiceOwnerMetricsAdapter>(sp =>
            sp.GetRequiredService<AzureServiceOwnerMonitorAdapter>()
        );
        builder.Services.TryAddSingleton<IQueryLoader, StaticQueryLoader>();

        return builder;
    }

    private static IHostApplicationBuilder AddConfig(this IHostApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: true);
        builder
            .Services.AddOptions<AppConfiguration>()
            .BindConfiguration(nameof(AppConfiguration))
            .Validate(config =>
            {
                if (config.PollInterval <= TimeSpan.Zero)
                    return false;
                if (config.SearchFromDays <= 0)
                    return false;
                if (string.IsNullOrWhiteSpace(config.AltinnEnvironment))
                    return false;
                if (config.AltinnEnvironment is not "at24" or "tt02" or "prod")
                    return false;
                if (string.IsNullOrWhiteSpace(config.DbConnectionString))
                    return false;

                return true;
            })
            .ValidateOnStart();

        if (!builder.Environment.IsDevelopment())
        {
            var keyVaultName = builder.Configuration.GetSection(nameof(AppConfiguration))[
                nameof(AppConfiguration.KeyVaultName)
            ];
            if (string.IsNullOrWhiteSpace(keyVaultName))
                throw new InvalidOperationException("Missing key vault name in configuration");

            builder.Configuration.AddAzureKeyVault(
                new Uri($"https://{keyVaultName}.vault.azure.net/"),
                new ManagedIdentityCredential(),
                new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromMinutes(5) }
            );
        }

        return builder;
    }

    private static IHostApplicationBuilder AddOtel(this IHostApplicationBuilder builder)
    {
        var otel = builder.Services.AddOpenTelemetry();

        otel.WithMetrics(metrics => metrics.AddMeter("System.Runtime").AddNpgsqlInstrumentation());
        otel.WithTracing(traces => traces.AddNpgsql());

        if (builder.Environment.IsDevelopment())
        {
            otel.UseOtlpExporter();
        }
        else
        {
            otel.UseAzureMonitor(options =>
            {
                options.Credential = new ManagedIdentityCredential();
            });
        }
        return builder;
    }
}
