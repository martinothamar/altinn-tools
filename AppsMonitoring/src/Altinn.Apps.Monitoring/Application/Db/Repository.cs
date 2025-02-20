using Altinn.Apps.Monitoring.Domain;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Apps.Monitoring.Application.Db;

internal sealed class Repository(ILogger<Repository> logger, NpgsqlDataSource dataSource)
{
    private readonly ILogger<Repository> _logger = logger;
    private readonly NpgsqlDataSource _dataSource = dataSource;

    public async ValueTask<IReadOnlyList<QueryStateEntity>> ListQueryStates(
        ServiceOwner? serviceOwner = null,
        Query? query = null,
        CancellationToken cancellationToken = default
    )
    {
        if (query is not null && serviceOwner is null)
            throw new ArgumentException("Service owner must be specified when querying by query hash");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                SELECT *
                FROM monitoring.queries
            """;
        if (serviceOwner is not null)
        {
            command.CommandText += " WHERE service_owner = @service_owner";
            command.Parameters.AddWithValue("service_owner", serviceOwner.Value.Value);

            if (query is not null)
            {
                command.CommandText += " AND hash = @hash";
                command.Parameters.AddWithValue("hash", query.Hash);
            }
        }

        await command.PrepareAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var queryStates = new List<QueryStateEntity>(16);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new QueryStateEntity
            {
                Id = reader.GetInt64(0),
                ServiceOwner = reader.GetString(1),
                Name = reader.GetString(2),
                Hash = reader.GetString(3),
                QueriedUntil = reader.GetFieldValue<Instant>(4),
            };

            queryStates.Add(item);
        }

        return queryStates;
    }

    public async ValueTask<IReadOnlyList<TelemetryEntity>> ListTelemetry(
        ServiceOwner? serviceOwner = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM monitoring.telemetry";
        if (serviceOwner is not null)
        {
            command.CommandText += " WHERE service_owner = @service_owner";
            command.Parameters.AddWithValue("service_owner", serviceOwner.Value);
        }

        await command.PrepareAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var telemetry = new List<TelemetryEntity>(16);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new TelemetryEntity
            {
                Id = reader.GetInt64(0),
                ExtId = reader.GetString(1),
                ServiceOwner = reader.GetString(2),
                AppName = reader.GetString(3),
                AppVersion = reader.GetString(4),
                TimeGenerated = reader.GetFieldValue<Instant>(5),
                TimeIngested = reader.GetFieldValue<Instant>(6),
                DupeCount = reader.GetInt64(7),
                Data = reader.GetFieldValue<TelemetryData>(8),
            };

            telemetry.Add(item);
        }

        return telemetry;
    }

    public async ValueTask<int> InsertTelemetry(
        ServiceOwner serviceOwner,
        Query query,
        Instant searchTo,
        IReadOnlyList<TelemetryEntity> telemetry,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int written = 0;

        HashSet<string> dupes = [];
        if (telemetry.Count > 0)
        {
            var ingestionTimestamp = telemetry[0].TimeIngested;
            if (ingestionTimestamp == Instant.MinValue)
                throw new InvalidOperationException("Telemetry must have a valid ingestion timestamp");

            // 1. Creat temp table (ON COMMIT DROP)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                        CREATE TEMP TABLE telemetry_import (
                            ext_id TEXT NOT NULL,
                            service_owner TEXT NOT NULL,
                            app_name TEXT NOT NULL,
                            app_version TEXT NOT NULL,
                            time_generated TIMESTAMPTZ NOT NULL,
                            time_ingested TIMESTAMPTZ NOT NULL,
                            data JSONB NOT NULL
                        ) ON COMMIT DROP;
                    """;
                await command.PrepareAsync(cancellationToken);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Copy data into temp table
            {
                await using var import = connection.BeginBinaryImport(
                    "COPY telemetry_import (ext_id, service_owner, app_name, app_version, time_generated, time_ingested, data) FROM STDIN (FORMAT binary)"
                );
                for (int i = 0; i < telemetry.Count; i++)
                {
                    var item = telemetry[i];
                    if (item.TimeIngested != ingestionTimestamp)
                        throw new InvalidOperationException(
                            "All telemetry items must have the same ingestion timestamp"
                        );

                    await import.StartRowAsync(cancellationToken);
                    await import.WriteAsync(item.ExtId, NpgsqlDbType.Text, cancellationToken);
                    await import.WriteAsync(item.ServiceOwner, NpgsqlDbType.Text, cancellationToken);
                    await import.WriteAsync(item.AppName, NpgsqlDbType.Text, cancellationToken);
                    await import.WriteAsync(item.AppVersion, NpgsqlDbType.Text, cancellationToken);
                    await import.WriteAsync(item.TimeGenerated, NpgsqlDbType.TimestampTz, cancellationToken);
                    await import.WriteAsync(item.TimeIngested, NpgsqlDbType.TimestampTz, cancellationToken);
                    await import.WriteAsync(item.Data, NpgsqlDbType.Jsonb, cancellationToken);
                }

                await import.CompleteAsync(cancellationToken);
            }

            // 3. Insert data from temp table into telemetry table, with conflict resolution (do nothing)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                        INSERT INTO monitoring.telemetry (ext_id, service_owner, app_name, app_version, time_generated, time_ingested, dupe_count, data)
                        SELECT ext_id, service_owner, app_name, app_version, time_generated, time_ingested, 0 as dupe_count, data FROM telemetry_import
                        ON CONFLICT (service_owner, ext_id) DO UPDATE SET dupe_count = monitoring.telemetry.dupe_count + 1
                        RETURNING ext_id, time_ingested;
                    """;
                await command.PrepareAsync(cancellationToken);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var extId = reader.GetString(0);
                    var timeIngested = reader.GetFieldValue<Instant>(1);
                    if (timeIngested != ingestionTimestamp)
                        dupes.Add(extId);
                }

                _logger.LogInformation(
                    "Inserted {Written}/{Total} rows into telemetry table, found {DupeCount} duplicates",
                    written,
                    telemetry.Count,
                    dupes.Count
                );
            }
        }

        // 4. update queries table with 'queried_until'
        {
            var to =
                telemetry.Count - dupes.Count > 0
                    ? telemetry.Where(t => !dupes.Contains(t.ExtId)).Max(t => t.TimeGenerated)
                    : searchTo;
            await using var command = connection.CreateCommand();
            command.CommandText = """
                    INSERT INTO monitoring.queries (service_owner, name, hash, queried_until)
                    VALUES (@service_owner, @name, @hash, @queried_until)
                    ON CONFLICT (service_owner, hash) DO UPDATE SET name = EXCLUDED.name, queried_until = EXCLUDED.queried_until;
                """;
            command.Parameters.AddWithValue("service_owner", serviceOwner.Value);
            command.Parameters.AddWithValue("name", query.Name);
            command.Parameters.AddWithValue("hash", query.Hash);
            command.Parameters.AddWithValue("queried_until", to);

            await command.PrepareAsync(cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return written;
    }
}
