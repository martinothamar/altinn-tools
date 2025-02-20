using Altinn.Apps.Monitoring.Application.Azure;
using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Application.DbUp;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Altinn.Apps.Monitoring.Application;

internal static class DIExtensions
{
    public static IHostApplicationBuilder AddApplication(this IHostApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: true);

        builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        builder.Services.Configure<AppConfiguration>(builder.Configuration.GetSection(nameof(AppConfiguration)));
        builder.Services.AddHostedService<Migrator>();

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
        );

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
        builder.Services.TryAddSingleton<Repository>();

        builder.Services.TryAddSingleton<Orchestrator>();
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<Orchestrator>());
        return builder;
    }
}
