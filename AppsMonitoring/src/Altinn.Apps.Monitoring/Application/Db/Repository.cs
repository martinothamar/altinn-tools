using Altinn.Apps.Monitoring.Domain;
using NodaTime;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Apps.Monitoring.Application.Db;

internal sealed class Repository(NpgsqlDataSource dataSource)
{
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public async ValueTask<Instant?> GetLatestErrorGeneratedTime(
        ServiceOwner serviceOwner,
        CancellationToken cancellationToken
    )
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT MAX(time_generated) FROM monitoring.errors WHERE service_owner = @service_owner"
        );

        command.Parameters.AddWithValue("service_owner", serviceOwner.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is DBNull or null ? null : (Instant)result;
    }

    public async ValueTask InsertErrors(IReadOnlyList<ErrorEntity> errors, CancellationToken cancellationToken)
    {
        // Insert errors into database
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

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
    }
}
