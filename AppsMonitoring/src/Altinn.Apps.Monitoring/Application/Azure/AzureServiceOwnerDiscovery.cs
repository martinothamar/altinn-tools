using System.Collections.Concurrent;
using Altinn.Apps.Monitoring.Domain;
using Azure.ResourceManager;

namespace Altinn.Apps.Monitoring.Application.Azure;

internal sealed class AzureServiceOwnerDiscovery(AzureClients clients, AzureServiceOwnerResources serviceOwnerResources)
    : IServiceOwnerDiscovery
{
    private readonly ArmClient _armClient = clients.ArmClient;
    private readonly AzureServiceOwnerResources _serviceOwnerResources = serviceOwnerResources;

    public async ValueTask<IReadOnlyList<ServiceOwner>> Discover(CancellationToken cancellationToken)
    {
        var serviceOwners = new ConcurrentBag<ServiceOwner>();
        var env = "prod"; // TODO: from env?
        await Parallel.ForEachAsync(
            _armClient.GetSubscriptions().GetAllAsync(cancellationToken),
            async (subscription, cancellationToken) =>
            {
                if (!subscription.Data.DisplayName.StartsWith("altinn", StringComparison.OrdinalIgnoreCase))
                    return;
                if (!subscription.Data.DisplayName.EndsWith(env, StringComparison.OrdinalIgnoreCase))
                    return;

                var split = subscription.Data.DisplayName.Split('-');
                if (split.Length != 3)
                    return;

                var serviceOwnerValue = split[1];
                if (serviceOwnerValue.Any(char.IsLower))
                    return;

                var serviceOwner = ServiceOwner.Parse(serviceOwnerValue);
                var resources = await _serviceOwnerResources.GetResources(serviceOwner, cancellationToken);
                if (resources is null)
                    return;

                serviceOwners.Add(serviceOwner);
            }
        );

        return serviceOwners.ToArray();
    }
}
