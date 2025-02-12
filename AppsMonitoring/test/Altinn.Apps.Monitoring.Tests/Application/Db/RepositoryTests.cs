using Altinn.Apps.Monitoring.Application.Db;
using Npgsql;

namespace Altinn.Apps.Monitoring.Tests.Application.Db;

public class RepositoryTests
{
    [Fact]
    public async Task Test_Persistence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var fixture = await HostFixture.Create();

        var dataSource = fixture.Services.GetRequiredService<NpgsqlDataSource>();
        var timeProvider = fixture.Services.GetRequiredService<TimeProvider>();
        var repository = fixture.Services.GetRequiredService<Repository>();

        ErrorEntity[] errors = [TestData.GenerateErrorEntity(timeProvider)];

        await repository.InsertErrors(errors, cancellationToken);

        await using var selectCommand = dataSource.CreateCommand("SELECT * FROM monitoring.errors");
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);

        var readErrors = new List<ErrorEntity>(errors.Length);
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
