using Altinn.Apps.Monitoring.Application.Azure;
using Altinn.Apps.Monitoring.Application.DbUp;
using Npgsql;

namespace Altinn.Apps.Monitoring.Application;

internal static class DIExtensions
{
    public static IHostApplicationBuilder AddApplication(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<AppConfiguration>(builder.Configuration.GetSection(nameof(AppConfiguration)));
        builder.Services.AddHostedService<Seeder>();

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
        builder.Services.AddSingleton<AzureClients>();
        builder.Services.AddSingleton<AzureServiceOwnerResources>();
        builder.Services.AddSingleton<IServiceOwnerDiscovery, AzureServiceOwnerDiscovery>();
        builder.Services.AddSingleton<IServiceOwnerLogsAdapter, AzureServiceOwnerLogsAdapter>();

        builder.Services.AddHostedService<Orchestrator>();
        return builder;
    }
}
