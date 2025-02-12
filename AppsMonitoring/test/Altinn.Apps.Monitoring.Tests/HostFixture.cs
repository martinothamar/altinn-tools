using Altinn.Apps.Monitoring.Application;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Altinn.Apps.Monitoring.Tests;

internal sealed class HostFixture : WebApplicationFactory<Program>
{
    public PostgreSqlContainer PostgreSqlContainer { get; }

    private HostFixture(PostgreSqlContainer postgreSqlContainer)
    {
        PostgreSqlContainer = postgreSqlContainer;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{nameof(AppConfiguration)}:{nameof(AppConfiguration.DbConnectionString)}"] =
                        PostgreSqlContainer.GetConnectionString(),
                }
            );
        });
        builder.ConfigureServices(ConfigureServices);

        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await PostgreSqlContainer.DisposeAsync();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(Orchestrator));
        if (descriptor != null)
            services.Remove(descriptor);
    }

    private static string FindSolutionDir()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length != 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new Exception("Solution directory not found");
    }

    public static async Task<HostFixture> Create()
    {
        var solutionDir = FindSolutionDir();

        var cancellationToken = TestContext.Current.CancellationToken;

        PostgreSqlContainer? postgreSqlContainer = null;
        HostFixture? fixture = null;
        try
        {
            var initFile = new FileInfo(Path.Combine(solutionDir, "infra", "postgres_init.sql"));
            Assert.True(initFile.Exists, "Postgres init file not found at: " + initFile.FullName);
            postgreSqlContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16")
                .WithUsername("platform_monitoring_admin")
                .WithPassword("Password")
                .WithAutoRemove(false)
                .WithCleanUp(false)
                .WithResourceMapping(initFile, "/docker-entrypoint-initdb.d/")
                .Build();

            await postgreSqlContainer.StartAsync(cancellationToken);

            fixture = new HostFixture(postgreSqlContainer);

            using var client = fixture.CreateClient();
            Assert.Equal("Healthy", await client.GetStringAsync("/health", cancellationToken));

            return fixture;
        }
        catch (Exception)
        {
            if (fixture != null)
                await fixture.DisposeAsync();
            if (postgreSqlContainer != null)
                await postgreSqlContainer.DisposeAsync();

            throw;
        }
    }
}
