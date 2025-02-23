using System.Globalization;
using System.IO.Hashing;
using System.Text;
using Microsoft.Extensions.Options;

namespace Altinn.Apps.Monitoring.Application;

internal enum QueryType
{
    Traces = 1,
    Logs = 2,
    Metrics = 3,
}

#pragma warning disable CA1724 // Type name conflicts with namespace name
internal sealed record Query
#pragma warning restore CA1724 // Type name conflicts with namespace name
{
    public string Name { get; }
    public QueryType Type { get; }
    public string QueryTemplate { get; }
    public string Hash { get; }

    public string Format(Instant searchFrom, Instant searchTo) =>
        // Default instant string format is ISO 8601
        string.Format(CultureInfo.InvariantCulture, QueryTemplate, searchFrom.ToString(), searchTo.ToString());

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

internal interface IQueryLoader
{
    ValueTask<IReadOnlyList<Query>> Load(CancellationToken cancellationToken);
}

internal sealed class StaticQueryLoader(ILogger<StaticQueryLoader> logger, IOptionsMonitor<AppConfiguration> config)
    : IQueryLoader
{
    private readonly ILogger<StaticQueryLoader> _logger = logger;
    private readonly IOptionsMonitor<AppConfiguration> _config = config;

    public ValueTask<IReadOnlyList<Query>> Load(CancellationToken cancellationToken)
    {
        var config = _config.CurrentValue;
        var target = config.AltinnEnvironment switch
        {
            "prod" => "platform.altinn.no",
            _ => $"platform.{config.AltinnEnvironment}.altinn.no",
        };
        _logger.LogInformation("Loading queries for target: {Target}", target);

        Query[] queries =
        [
            new(
                "Failed Storage instance events",
                QueryType.Traces,
                // * 'OperationName' is present for classic Azure App Insights SDK (the same name as the root span name)
                //   but for OpenTelemetry the root span name is not present on children spans, so we need to use 'OperationName1' (from the join)
                // * 'Target' sometimes has garbage at the end, so we use 'startswith'
                $$"""
                    AppDependencies
                    | where TimeGenerated > todatetime('{searchFrom}') and TimeGenerated <= todatetime('{searchTo}')
                    | where Success == false
                    | where Target startswith "{{target}}"
                    | where Name startswith "POST /storage/api/v1/instances/" and Name endswith "/events"
                    | join kind=inner AppRequests on OperationId
                    | where OperationName startswith "PUT Process/NextElement" or OperationName1 endswith "/process/next";
                """
            ),
            new(
                "Failed Altinn events",
                QueryType.Traces,
                // * 'OperationName' is present for classic Azure App Insights SDK (the same name as the root span name)
                //   but for OpenTelemetry the root span name is not present on children spans, so we need to use 'OperationName1' (from the join)
                // * 'Target' sometimes has garbage at the end, so we use 'startswith'
                $$"""
                    AppDependencies
                    | where TimeGenerated > todatetime('{searchFrom}') and TimeGenerated <= todatetime('{searchTo}')
                    | where Success == false
                    | where Target startswith "{{target}}"
                    | where Name == "POST /events/api/v1/app"
                    | join kind=inner AppRequests on OperationId
                    | where OperationName startswith "PUT Process/NextElement" or OperationName1 endswith "/process/next";
                """
            ),
        ];

        return new(queries);
    }
}
