using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;

namespace Altinn.Apps.Monitoring.Application.Azure;

internal sealed record AzureClients
{
    public ArmClient ArmClient { get; }

    public LogsQueryClient LogsQueryClient { get; }

    public static DefaultAzureCredential CreateCredential()
    {
        // return new AzureCliCredential();
        return new DefaultAzureCredential(
            new DefaultAzureCredentialOptions
            {
                ExcludeAzureDeveloperCliCredential = true,
                ExcludeAzurePowerShellCredential = true,
                ExcludeInteractiveBrowserCredential = true,
                ExcludeSharedTokenCacheCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeEnvironmentCredential = true,
                ExcludeManagedIdentityCredential = true,
                // ExcludeAzureCliCredential = true,
            }
        );
    }

    public AzureClients()
    {
        ArmClient = new(CreateCredential());
        LogsQueryClient = new(CreateCredential());
    }
}
