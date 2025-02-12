using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Domain;
using NodaTime;

namespace Altinn.Apps.Monitoring.Application;

public interface IServiceOwnerLogsAdapter
{
    ValueTask<IReadOnlyList<IReadOnlyList<ErrorEntity>>> Query(
        ServiceOwner serviceOwner,
        string query,
        Instant from,
        Instant? to = null,
        CancellationToken cancellationToken = default
    );
}
