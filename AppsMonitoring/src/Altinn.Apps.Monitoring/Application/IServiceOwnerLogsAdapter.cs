using Altinn.Apps.Monitoring.Domain;

namespace Altinn.Apps.Monitoring.Application;

public interface IServiceOwnerLogsAdapter
{
    ValueTask<IReadOnlyList<Table>> Query(ServiceOwner serviceOwner, string query, DateTimeOffset from, DateTimeOffset? to = null, CancellationToken cancellationToken = default);
}

public sealed record Table(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object>> Rows);
