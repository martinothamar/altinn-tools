using Altinn.Apps.Monitoring.Application.Db;

namespace Altinn.Apps.Monitoring.Tests.Application.Db;

internal static class TestData
{
    public static TraceData GenerateTelemetryTraceData(
        int? altinnErrorId = null,
        int? instanceOwnerPartyId = null,
        Guid? instanceId = null,
        string? traceId = null,
        string? spanId = null,
        string? parentSpanId = null,
        string? traceName = null,
        string? spanName = null,
        bool? success = null,
        string? result = null,
        Duration? duration = null,
        Dictionary<string, string?>? attributes = null
    )
    {
        return new TraceData
        {
            AltinnErrorId = altinnErrorId ?? 1,
            InstanceOwnerPartyId = instanceOwnerPartyId ?? 2,
            InstanceId = instanceId ?? Guid.Parse("12525089-e367-4c64-bc8a-eb1b901553c9"),
            TraceId = traceId ?? "trace-id",
            SpanId = spanId ?? "span-id",
            ParentSpanId = parentSpanId ?? "parent-span-id",
            TraceName = traceName ?? "trace-name",
            SpanName = spanName ?? "span-name",
            Success = success ?? false,
            Result = result ?? "result",
            Duration = duration ?? Duration.FromMilliseconds(100),
            Attributes = attributes ?? new() { ["key1"] = "value1", ["key2"] = "value2" },
        };
    }

    public static TelemetryEntity GenerateTelemetryEntity(
        string? extId = null,
        string? serviceOwner = null,
        string? appName = null,
        string? appVersion = null,
        Instant? timeGenerated = null,
        Instant? timeIngested = null,
        int? dupeCount = null,
        bool? seeded = null,
        Func<TelemetryData>? dataGenerator = null,
        TimeProvider? timeProvider = null
    )
    {
        timeProvider ??= TimeProvider.System;

        return new TelemetryEntity
        {
            Id = 0,
            ExtId = extId ?? "ext-id",
            ServiceOwner = serviceOwner ?? "so",
            AppName = appName ?? "app-name",
            AppVersion = appVersion ?? "8.0.0",
            TimeGenerated = timeGenerated ?? timeProvider.GetCurrentInstant().Minus(Duration.FromMinutes(15)),
            TimeIngested = timeIngested ?? timeProvider.GetCurrentInstant(),
            DupeCount = dupeCount ?? 0,
            Seeded = seeded ?? false,
            Data = dataGenerator?.Invoke() ?? GenerateTelemetryTraceData(),
        };
    }
}
