using Altinn.Apps.Monitoring.Application.Azure;

namespace Altinn.Apps.Monitoring.Application;

internal static class DIExtensions
{
    public static IHostApplicationBuilder AddApplication(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHybridCache();
        builder.Services.AddSingleton<AzureClients>();
        builder.Services.AddSingleton<IServiceOwnerDiscovery, AzureServiceOwnerDiscovery>();
        builder.Services.AddSingleton<IServiceOwnerLogsAdapter, AzureServiceOwnerLogsAdapter>();
        builder.Services.AddSingleton<AzureServiceOwnerResources>();
        return builder;
    }
}
