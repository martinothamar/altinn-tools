using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.OperationalInsights;
using CsvHelper;
using Spectre.Console;

Directory.CreateDirectory("data");

var client = new ArmClient(new AzureCliCredential());

var cancellationToken = CancellationToken.None;

var subscriptions = await client.GetSubscriptions().GetAllAsync().ToArrayAsync();

int DaysToSearch = 30;
TimeSpan PollInterval = TimeSpan.FromHours(1);

await Task.WhenAll(subscriptions.Select(async subscription => 
{
    // if (!subscription.Data.DisplayName.Contains("skd", StringComparison.OrdinalIgnoreCase))
    //     return;

    if (!subscription.Data.DisplayName.StartsWith("altinn", StringComparison.OrdinalIgnoreCase))
        return;
    if (!subscription.Data.DisplayName.EndsWith("prod", StringComparison.OrdinalIgnoreCase))
        return;

    var split = subscription.Data.DisplayName.Split('-');
    if (split.Length != 3)
        return;

    var serviceOwner = split[1];
    if (serviceOwner.Any(char.IsLower))
        return;
    
    var serviceOwnerLower = serviceOwner.ToLowerInvariant();
    var rgs = await subscription.GetResourceGroups().GetAllAsync(cancellationToken: cancellationToken).ToArrayAsync();
    var rg = rgs.SingleOrDefault(rg => rg.Data.Name == $"monitor-{serviceOwnerLower}-prod-rg");
    if (rg is null)
        return;

    var workspaces = await rg.GetOperationalInsightsWorkspaces().GetAllAsync(cancellationToken: cancellationToken).ToArrayAsync();
    var workspace = workspaces.SingleOrDefault(ws => ws.Data.Name == $"application-{serviceOwnerLower}-prod-law");
    if (workspace is null)
        return;

    var client = new LogsQueryClient(new AzureCliCredential());
    var workspaceId = workspace.Id;

    var query = $"""
        AppDependencies
        | where TimeGenerated > now(-{DaysToSearch}d)
        | where Success == false
        | where Target == "platform.altinn.no"
        | where (Name startswith "POST /storage" and Name endswith "/events" and OperationName startswith "PUT Process/NextElement");

        AppDependencies
        | where TimeGenerated > now(-{DaysToSearch}d)
        | where Success == false
        | where Target == "platform.altinn.no"
        | where (Name startswith "POST /events" and OperationName startswith "PUT Process/NextElement");
    """;

    var timer = new PeriodicTimer(PollInterval);
    do 
    {
        Response<LogsQueryResult> results = await client.QueryResourceAsync(
            workspaceId,
            query,
            new QueryTimeRange(TimeSpan.FromDays(DaysToSearch)),
            cancellationToken: cancellationToken
        );

        var tables = results.Value.AllTables;
        var total = string.Join(", ", tables.Select((t, i) => $"error {i + 1}={(t.Rows.Count > 0 ? $"[red]{t.Rows.Count}[/]" : $"[green]{t.Rows.Count}[/]")}"));
        AnsiConsole.MarkupLine( $"([bold]{serviceOwnerLower}[/]) - {total}");
        
        int tableCount = 0;
        for (int i = 0; i < tables.Count; i++)
        {
            var table = tables[i];
            if (table.Rows.Count == 0)
                continue;

            tableCount++;
            var records = table.Rows.Select(r => 
            {
                return new DependencyRecord(
                    r.GetDateTimeOffset(nameof(DependencyRecord.TimeGenerated)) ?? default,
                    r.GetString(nameof(DependencyRecord.Id)),
                    r.GetString(nameof(DependencyRecord.Target)),
                    r.GetString(nameof(DependencyRecord.DependencyType)),
                    r.GetString(nameof(DependencyRecord.Name)),
                    r.GetString(nameof(DependencyRecord.Data)),
                    r.GetBoolean(nameof(DependencyRecord.Success)),
                    r.GetString(nameof(DependencyRecord.ResultCode)),
                    r.GetDouble(nameof(DependencyRecord.DurationMs)),
                    r.GetString(nameof(DependencyRecord.PerformanceBucket)),
                    r.GetString(nameof(DependencyRecord.Properties)),
                    r.GetString(nameof(DependencyRecord.OperationName)),
                    r.GetString(nameof(DependencyRecord.OperationId)),
                    r.GetString(nameof(DependencyRecord.ParentId)),
                    r.GetString(nameof(DependencyRecord.AppVersion)),
                    r.GetString(nameof(DependencyRecord.AppRoleName))
                );
            }).ToArray();

            {
                var filename = $"data/{serviceOwnerLower}-error-{i + 1}.json";
                File.Delete(filename);
                await using var writer = File.OpenWrite(filename);
                await JsonSerializer.SerializeAsync(writer, records, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
            }

            {
                var filename = $"data/{serviceOwnerLower}-error-{i + 1}.csv";
                File.Delete(filename);
                await using var writer = new StreamWriter(filename);
                await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                await csv.WriteRecordsAsync(records, cancellationToken);
            }
        }
        
        AnsiConsole.MarkupLine($"([bold]{serviceOwnerLower}[/]) - wrote [blue]{tableCount}[/] to 'data/', sleeping for [blue]{PollInterval}[/]...");
    }
    while (await timer.WaitForNextTickAsync(cancellationToken));
}));


public sealed record DependencyRecord(
    DateTimeOffset TimeGenerated, 
    string Id, 
    string Target, 
    string DependencyType, 
    string Name, 
    string Data, 
    bool? Success, 
    string ResultCode,
    double? DurationMs,
    string PerformanceBucket,
    string Properties,
    string OperationName,
    string OperationId,
    string ParentId,
    string AppVersion,
    string AppRoleName
);

