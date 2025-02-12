using Altinn.Apps.Monitoring.Application.Db;

namespace Altinn.Apps.Monitoring.Tests.Application.Db;

internal static class TestData
{
    public static ErrorTraceData GenerateErrorTraceData()
    {
        return new ErrorTraceData
        {
            AltinnErrorId = 1,
            InstanceOwnerPartyId = 2,
            InstanceId = Guid.NewGuid(),
            TraceId = "trace-id",
            SpanId = "span-id",
            ParentSpanId = "parent-span-id",
            TraceName = "trace-name",
            SpanName = "span-name",
            Success = false,
            Result = "result",
            Duration = Duration.FromMilliseconds(100),
            Attributes = new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" },
        };
    }

    public static ErrorEntity GenerateErrorEntity(TimeProvider? timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;

        return new ErrorEntity
        {
            Id = 0,
            ServiceOwner = "service-owner",
            AppName = "app-name",
            AppVersion = "8.0.0",
            TimeGenerated = timeProvider.GetCurrentInstant(),
            TimeIngested = timeProvider.GetCurrentInstant().Plus(Duration.FromMinutes(1)),
            Data = GenerateErrorTraceData(),
        };
    }
}
