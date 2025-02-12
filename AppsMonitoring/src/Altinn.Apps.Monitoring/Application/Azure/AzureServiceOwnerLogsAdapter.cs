using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Domain;
using Azure;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using NodaTime;
using NodaTime.Extensions;

namespace Altinn.Apps.Monitoring.Application.Azure;

internal sealed partial class AzureServiceOwnerLogsAdapter(
    AzureClients clients,
    AzureServiceOwnerResources serviceOwnerResources,
    TimeProvider timeProvider
) : IServiceOwnerLogsAdapter
{
    private readonly LogsQueryClient _logsClient = clients.LogsQueryClient;
    private readonly AzureServiceOwnerResources _serviceOwnerResources = serviceOwnerResources;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<IReadOnlyList<IReadOnlyList<ErrorEntity>>> Query(
        ServiceOwner serviceOwner,
        string query,
        Instant from,
        Instant? to = null,
        CancellationToken cancellationToken = default
    )
    {
        var resources = await _serviceOwnerResources.GetResources(serviceOwner, cancellationToken);
        if (resources is null)
            return [];

        to ??= _timeProvider.GetCurrentInstant();

        var (_, _, workspace) = resources;

        var searchTimestamp = _timeProvider.GetCurrentInstant();

        Response<LogsQueryResult> results = await _logsClient.QueryResourceAsync(
            workspace.Id,
            query,
            new QueryTimeRange((to.Value - from).ToTimeSpan()),
            cancellationToken: cancellationToken
        );

        var tables = results?.Value?.AllTables;
        if (tables is null)
            return [];
        var totalRows = tables.Sum(table => table.Rows.Count);

        int tableCount = 0;
        var errors = new List<IReadOnlyList<ErrorEntity>>(totalRows);
        var instanceIdRegex = InstanceIdInUrlRegex();
        for (int i = 0; i < tables.Count; i++)
        {
            var table = tables[i];
            if (table.Rows.Count == 0)
                continue;

            var tableErrors = new List<ErrorEntity>(table.Rows.Count);
            errors.Add(tableErrors);

            tableCount++;

            var indexes = table.Columns.Index();
            int nameIdx = -1;
            int operationNameIdx = -1;
            int operationIdIdx = -1;
            int urlIdx = -1;
            int timeGeneratedIdx = -1;
            int idIdx = -1;
            int targetIdx = -1;
            int dependencyTypeIdx = -1;
            int dataIdx = -1;
            int successIdx = -1;
            int resultCodeIdx = -1;
            int durationIdx = -1;
            int appRoleNameIdx = -1;
            int appVersionIdx = -1;
            int parentIdIdx = -1;
            int performanceBucketIdx = -1;
            int propertiesIdx = -1;

            foreach (var (rowIndex, column) in indexes)
            {
                if (column.Name == "Name")
                    nameIdx = rowIndex;
                else if (column.Name == "OperationName")
                    operationNameIdx = rowIndex;
                else if (column.Name == "OperationId")
                    operationIdIdx = rowIndex;
                else if (column.Name == "TimeGenerated")
                    timeGeneratedIdx = rowIndex;
                else if (column.Name == "Id")
                    idIdx = rowIndex;
                else if (column.Name == "Target")
                    targetIdx = rowIndex;
                else if (column.Name == "DependencyType")
                    dependencyTypeIdx = rowIndex;
                else if (column.Name == "Data")
                    dataIdx = rowIndex;
                else if (column.Name == "Success")
                    successIdx = rowIndex;
                else if (column.Name == "ResultCode")
                    resultCodeIdx = rowIndex;
                else if (column.Name == "DurationMs")
                    durationIdx = rowIndex;
                else if (column.Name == "Url")
                    urlIdx = rowIndex;
                else if (column.Name == "AppRoleName")
                    appRoleNameIdx = rowIndex;
                else if (column.Name == "AppVersion")
                    appVersionIdx = rowIndex;
                else if (column.Name == "ParentId")
                    parentIdIdx = rowIndex;
                else if (column.Name == "PerformanceBucket")
                    performanceBucketIdx = rowIndex;
                else if (column.Name == "Properties")
                    propertiesIdx = rowIndex;
            }

            for (int j = 0; j < table.Rows.Count; j++)
            {
                var row = table.Rows[j];

                var name = ReadString(row, nameIdx);

                string? rootRequestUrl = null;
                int? instanceOwnerPartyId = null;
                Guid? instanceId = null;
                if (urlIdx != -1)
                {
                    // Url column is present in requests table but not the dependency table
                    // so in this case the root trace/request has been joined in
                    rootRequestUrl = ReadString(row, urlIdx);
                    var match = instanceIdRegex.Match(rootRequestUrl);
                    if (match.Success)
                    {
                        instanceOwnerPartyId = int.Parse(match.Groups[1].Value);
                        instanceId = Guid.Parse(match.Groups[2].Value);
                    }
                }
                else
                {
                    // When we don't have the root request joined in,
                    // lets see if we can extract the instance id from the name
                    var match = instanceIdRegex.Match(name);
                    if (match.Success)
                    {
                        instanceOwnerPartyId = int.Parse(match.Groups[1].Value);
                        instanceId = Guid.Parse(match.Groups[2].Value);
                    }
                }

                var timeGenerated = ReadInstant(row, timeGeneratedIdx);

                tableErrors.Add(
                    new ErrorEntity
                    {
                        Id = 0,
                        ServiceOwner = serviceOwner.Value,
                        AppName = ReadString(row, appRoleNameIdx),
                        AppVersion = ReadString(row, appVersionIdx),
                        TimeGenerated = timeGenerated,
                        TimeIngested = searchTimestamp,
                        Data = new ErrorTraceData
                        {
                            AltinnErrorId = i,
                            InstanceOwnerPartyId = instanceOwnerPartyId,
                            InstanceId = instanceId,
                            TraceId = ReadString(row, operationIdIdx),
                            SpanId = ReadString(row, idIdx),
                            ParentSpanId = ReadString(row, parentIdIdx),
                            TraceName = ReadString(row, operationNameIdx),
                            SpanName = name,
                            Success = ReadBool(row, successIdx),
                            Result = ReadString(row, resultCodeIdx),
                            Duration = Duration.FromMilliseconds(ReadDouble(row, durationIdx)),
                            Attributes = new Dictionary<string, string>
                            {
                                ["Target"] = ReadString(row, targetIdx),
                                ["DependencyType"] = ReadString(row, dependencyTypeIdx),
                                ["Data"] = ReadString(row, dataIdx),
                                ["PerformanceBucket"] = ReadString(row, performanceBucketIdx),
                                ["Properties"] = ReadString(row, propertiesIdx),
                            },
                        },
                    }
                );
            }
        }

        return errors;
    }

    static string ReadString(LogsTableRow row, int idx, [CallerArgumentExpression("idx")] string? idxName = null)
    {
        if (idx == -1)
            throw new Exception($"Column {idxName} not found in table");

        return row.GetString(idx)
            ?? throw new Exception($"Unexpected value for {idxName} ({idx}): {row[idx].GetType().Name}");
    }

    static bool ReadBool(LogsTableRow row, int idx, [CallerArgumentExpression("idx")] string? idxName = null)
    {
        if (idx == -1)
            throw new Exception($"Column {idxName} not found in table");

        return row.GetBoolean(idx)
            ?? throw new Exception($"Unexpected value for {idxName} ({idx}): {row[idx].GetType().Name}");
    }

    static double ReadDouble(LogsTableRow row, int idx, [CallerArgumentExpression("idx")] string? idxName = null)
    {
        if (idx == -1)
            throw new Exception($"Column {idxName} not found in table");

        return row.GetDouble(idx)
            ?? throw new Exception($"Unexpected value for {idxName} ({idx}): {row[idx].GetType().Name}");
    }

    static Instant ReadInstant(LogsTableRow row, int idx, [CallerArgumentExpression("idx")] string? idxName = null)
    {
        if (idx == -1)
            throw new Exception($"Column {idxName} not found in table");

        return row.GetDateTimeOffset(idx)?.ToInstant()
            ?? throw new Exception($"Unexpected value for {idxName} ({idx}): {row[idx].GetType().Name}");
    }

    [GeneratedRegex(@"(\d+)\/([0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12})")]
    private static partial Regex InstanceIdInUrlRegex();
}
