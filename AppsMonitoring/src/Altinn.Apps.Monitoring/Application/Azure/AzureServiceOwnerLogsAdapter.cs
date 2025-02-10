using Altinn.Apps.Monitoring.Domain;
using Azure;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;

namespace Altinn.Apps.Monitoring.Application.Azure;

internal sealed class AzureServiceOwnerLogsAdapter(AzureClients clients, AzureServiceOwnerResources serviceOwnerResources) : IServiceOwnerLogsAdapter
{
    private readonly LogsQueryClient _logsClient = clients.LogsQueryClient;
    private readonly AzureServiceOwnerResources _serviceOwnerResources = serviceOwnerResources;

    public async ValueTask<IReadOnlyList<Table>> Query(ServiceOwner serviceOwner, string query, DateTimeOffset from, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        var resources = await _serviceOwnerResources.GetResources(serviceOwner, cancellationToken);
        if (resources is null)
            return [];

        to ??= DateTimeOffset.UtcNow;

        var (_, _, workspace) = resources;

        Response<LogsQueryResult> results = await _logsClient.QueryResourceAsync(
            workspace.Id,
            query,
            new QueryTimeRange(to.Value - from),
            cancellationToken: cancellationToken
        );

        if (results is null)
            return [];

        var tables = new Table[results.Value.AllTables.Count];
        for (int i = 0; i < tables.Length; i++)
        {
            var table = results.Value.AllTables[i];
            var columns = table.Columns.Select(col => col.Name).ToArray();
            var rows = table.Rows.Select(row => row.ToArray()).ToArray();
            tables[i] = new Table(columns, rows);
        }

        return tables;
    }
}
