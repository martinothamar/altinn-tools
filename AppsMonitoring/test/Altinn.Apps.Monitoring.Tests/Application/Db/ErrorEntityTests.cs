using Altinn.Apps.Monitoring.Application.Db;
using NodaTime;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Apps.Monitoring.Tests.Application.Db;

public class ErrorEntityTests
{
    [Fact]
    public async Task Test_Trace_Data_Serialization()
    {
        var data = new ErrorTraceData
        {
            AltinnErrorId = 1,
            InstanceOwnerPartyId = 2,
            InstanceId = Guid.NewGuid(),
            TraceId = "trace-id",
            SpanId = "span-id",
            ParentSpanId = "parent-span-id",
        };

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

    [Fact]
    public async Task Test_Persistence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var fixture = await HostFixture.Create();

        var dataSource = fixture.Services.GetRequiredService<NpgsqlDataSource>();

        var clock = SystemClock.Instance;

        List<ErrorEntity> errors =
        [
            new ErrorEntity
            {
                Id = 0,
                ServiceOwner = "service-owner",
                AppName = "app-name",
                AppVersion = "8.0.0",
                TimeGenerated = clock.GetCurrentInstant(),
                TimeIngested = clock.GetCurrentInstant().Plus(Duration.FromMinutes(1)),
                Data = new ErrorTraceData
                {
                    AltinnErrorId = 1,
                    InstanceOwnerPartyId = 2,
                    InstanceId = Guid.NewGuid(),
                    TraceId = "trace-id",
                    SpanId = "span-id",
                    ParentSpanId = "parent-span-id",
                },
            },
        ];

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var import = await connection.BeginBinaryImportAsync(
            "COPY monitoring.errors (service_owner, app_name, app_version, time_generated, time_ingested, data) FROM STDIN (FORMAT binary)",
            cancellationToken
        );

        foreach (var error in errors)
        {
            await import.StartRowAsync(cancellationToken);
            await import.WriteAsync(error.ServiceOwner, NpgsqlDbType.Text, cancellationToken);
            await import.WriteAsync(error.AppName, NpgsqlDbType.Text, cancellationToken);
            await import.WriteAsync(error.AppVersion, NpgsqlDbType.Text, cancellationToken);
            await import.WriteAsync(error.TimeGenerated, NpgsqlDbType.TimestampTz, cancellationToken);
            await import.WriteAsync(error.TimeIngested, NpgsqlDbType.TimestampTz, cancellationToken);
            await import.WriteAsync(error.Data, NpgsqlDbType.Jsonb, cancellationToken);
        }

        await import.CompleteAsync(cancellationToken);

        await using var selectCommand = new NpgsqlCommand("SELECT * FROM monitoring.errors", connection);
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);

        var readErrors = new List<ErrorEntity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var error = new ErrorEntity
            {
                Id = reader.GetInt64(0),
                ServiceOwner = reader.GetString(1),
                AppName = reader.GetString(2),
                AppVersion = reader.GetString(3),
                TimeGenerated = reader.GetFieldValue<Instant>(4),
                TimeIngested = reader.GetFieldValue<Instant>(5),
                Data = reader.GetFieldValue<ErrorData>(6),
            };

            readErrors.Add(error);
        }

        var obj = new { Pre = errors, Post = readErrors };
        await Verify(obj).AutoVerify();
    }
}
