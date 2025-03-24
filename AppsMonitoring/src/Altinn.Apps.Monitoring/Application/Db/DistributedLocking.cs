using System.Data;
using Npgsql;

namespace Altinn.Apps.Monitoring.Application.Db;

internal enum DistributedLockName : long
{
    DbMigrator = 1,
    DbSeeder = 2,
    Application = 3,
}

internal sealed class DistributedLocking(
    ILogger<DistributedLocking> logger,
    [FromKeyedServices(Config.UserMode)] NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    Telemetry telemetry
)
{
    private readonly ILogger<DistributedLocking> _logger = logger;
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly Telemetry _telemetry = telemetry;

    public async ValueTask<IDistributedLockHandle> AcquireLock(
        DistributedLockName lockName,
        CancellationToken cancellationToken
    )
    {
        using var activity = _telemetry.Activities.StartActivity("DistributedLocking.AcquireLock");
        activity?.SetTag("lock.name", lockName.ToString());

        while (true)
        {
            var handle = await TryAcquireLock(lockName, cancellationToken);
            if (handle is not null)
                return handle;

            await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, cancellationToken);
        }
    }

    public async ValueTask<IDistributedLockHandle?> TryAcquireLock(
        DistributedLockName lockName,
        CancellationToken cancellationToken
    )
    {
        using var activity = _telemetry.Activities.StartActivity("DistributedLocking.TryAcquireLock");
        activity?.SetTag("lock.name", lockName.ToString());

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            connection.StateChange += (_, e) =>
            {
                _logger.LogInformation(
                    "Distributed lock ({LockName}) connection state changed: {FromState}->{ToState}",
                    lockName,
                    e.OriginalState,
                    e.CurrentState
                );
            };
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT pg_try_advisory_lock(@lockName);";
            cmd.Parameters.AddWithValue("lockName", (long)lockName);
            var hasLockObj = await cmd.ExecuteScalarAsync(cancellationToken);
            if (hasLockObj is not bool hasLock)
            {
                _logger.LogError("Unexpected result from acquiring lock");
                throw new Exception("Couldn't acquire lock");
            }

            if (!hasLock)
            {
                _logger.LogInformation("Failed to acquire lock {LockName}", lockName);
                return null;
            }

            var handle = new DistributedLockHandle(this, lockName, connection);
            connection = null;
            activity?.SetTag("lock.acquired", true);
            _logger.LogInformation("Acquired lock {LockName}", lockName);
            return handle;
        }
        finally
        {
            if (connection is not null)
            {
                activity?.SetTag("lock.acquired", false);
                await connection.DisposeAsync();
            }
        }
    }

    internal interface IDistributedLockHandle : IAsyncDisposable
    {
        // CancellationToken HandleLostToken { get; }
    }

    private sealed class DistributedLockHandle(
        DistributedLocking parent,
        DistributedLockName lockName,
        NpgsqlConnection connection
    ) : IDistributedLockHandle
    {
        private readonly DistributedLocking _parent = parent;
        private readonly DistributedLockName _lockName = lockName;
        private readonly NpgsqlConnection _connection = connection;

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_connection.State != ConnectionState.Open)
                {
                    _parent._logger.LogWarning("Connection is not open when disposing lock handle");
                    return;
                }

                await using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT pg_advisory_unlock(@lockName);";
                cmd.Parameters.AddWithValue("lockName", (long)_lockName);
                var wasReleasedObj = await cmd.ExecuteScalarAsync();
                if (wasReleasedObj is not bool wasReleased)
                {
                    _parent._logger.LogError("Unexpected result from releasing lock");
                    throw new Exception("Couldn't release lock");
                }
                if (!wasReleased)
                {
                    _parent._logger.LogWarning("Failed to release lock {LockName} (it was not held)", _lockName);
                    throw new Exception("Couldn't release lock");
                }
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
