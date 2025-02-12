using Altinn.Apps.Monitoring.Application.Db;

namespace Altinn.Apps.Monitoring.Tests.Application.Db;

public class ErrorEntityTests
{
    [Fact]
    public async Task Test_Trace_Data_Serialization()
    {
        var data = TestData.GenerateErrorTraceData();

        var json = data.Serialize();

        await VerifyJson(json).AutoVerify();

        var deserializedData = ErrorData.Deserialize(json);
        var deserializedTraceData = Assert.IsType<ErrorTraceData>(deserializedData);
        Assert.Equivalent(data, deserializedTraceData);
    }

    [Fact]
    public async Task Test_Logs_Data_Serialization()
    {
        var data = new ErrorLogsData
        {
            AltinnErrorId = 1,
            TraceId = "trace-id",
            SpanId = "span-id",
            Message = "some message",
        };

        var json = data.Serialize();

        await VerifyJson(json).AutoVerify();

        var deserializedData = ErrorData.Deserialize(json);
        var deserializedLogsData = Assert.IsType<ErrorLogsData>(deserializedData);
        Assert.Equivalent(data, deserializedLogsData);
    }
}
