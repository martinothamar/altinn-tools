using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;

namespace Altinn.Apps.Monitoring.Application.Db;

internal static class Config
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

internal sealed class ErrorEntity
{
    public required long Id { get; init; }
    public required string ServiceOwner { get; init; }
    public required string AppName { get; init; }
    public required string AppVersion { get; init; }
    public required Instant TimeGenerated { get; init; }
    public required Instant TimeIngested { get; init; }
    public required ErrorData Data { get; init; }
}

[JsonDerivedType(typeof(ErrorTraceData), typeDiscriminator: "trace")]
[JsonDerivedType(typeof(ErrorLogsData), typeDiscriminator: "logs")]
internal abstract class ErrorData
{
    public required int AltinnErrorId { get; init; }

    public static ErrorData Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ErrorData>(json, Config.JsonOptions) ?? throw new JsonException();
    }

    public static ErrorData Deserialize(byte[] json)
    {
        return JsonSerializer.Deserialize<ErrorData>(json, Config.JsonOptions) ?? throw new JsonException();
    }

    public string Serialize()
    {
        return JsonSerializer.Serialize(this, Config.JsonOptions);
    }

    public byte[] SerializeToUtf8Bytes()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this, Config.JsonOptions);
    }
}

internal sealed class ErrorTraceData : ErrorData
{
    public required int? InstanceOwnerPartyId { get; init; }
    public required Guid? InstanceId { get; init; }
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public required string? ParentSpanId { get; init; }
}

internal sealed class ErrorLogsData : ErrorData
{
    public required string? TraceId { get; init; }
    public required string? SpanId { get; init; }
    public required string Message { get; init; }
}
