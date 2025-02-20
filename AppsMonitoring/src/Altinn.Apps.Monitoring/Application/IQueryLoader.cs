using System.IO.Hashing;
using System.Text;

namespace Altinn.Apps.Monitoring.Application;

public enum QueryType
{
    Traces,
    Logs,
    Metrics,
}

public sealed record Query
{
    public string Name { get; }
    public QueryType Type { get; }
    public string QueryTemplate { get; }
    public string Hash { get; }

    public string Format(Instant searchFrom, Instant searchTo) =>
        // Default instant string format is ISO 8601
        string.Format(QueryTemplate, searchFrom.ToString(), searchTo.ToString());

    public Query(string name, QueryType type, string queryTemplate)
    {
        Name = name;
        Type = type;
        QueryTemplate = queryTemplate;
        Hash = HashQuery(queryTemplate);
    }

    private static string HashQuery(string queryTemplate)
    {
        var hash = XxHash128.Hash(Encoding.UTF8.GetBytes(queryTemplate));
        return Convert.ToBase64String(hash);
    }
}

public interface IQueryLoader
{
    ValueTask<IReadOnlyList<Query>> Load(CancellationToken cancellationToken);
}

public sealed class StaticQueryLoader : IQueryLoader
{
    public ValueTask<IReadOnlyList<Query>> Load(CancellationToken cancellationToken)
    {
        Query[] queries =
        [
            new(
                "Failed Storage instance events",
                QueryType.Traces,
                """
                    AppDependencies
                    | where TimeGenerated > todatetime('{searchFrom}') and TimeGenerated <= todatetime('{searchTo}')
                    | where Success == false
                    | where Target startswith "platform.altinn.no"
                    | where (Name startswith "POST /storage" and Name endswith "/events" and OperationName startswith "PUT Process/NextElement");
                """
            ),
            new(
                "Failed Altinn events",
                QueryType.Traces,
                """
                    AppDependencies
                    | where TimeGenerated > todatetime('{searchFrom}') and TimeGenerated <= todatetime('{searchTo}')
                    | where Success == false
                    | where Target startswith "platform.altinn.no"
                    | where (Name startswith "POST /events" and OperationName startswith "PUT Process/NextElement")
                    | join kind=inner AppRequests on OperationId;
                """
            ),
        ];

        return new(queries);
    }
}
