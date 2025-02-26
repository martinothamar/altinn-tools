using System.Text.Json;
using Altinn.Apps.Monitoring.Application;
using Altinn.Apps.Monitoring.Application.Db;
using Altinn.Apps.Monitoring.Application.Slack;
using Altinn.Apps.Monitoring.Domain;
using Altinn.Apps.Monitoring.Tests.Application.Db;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NodaTime.Text;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Altinn.Apps.Monitoring.Tests.Application.Slack;

public class SlackAlerterTests
{
    private const string OkPayload = """
        {
            "ok": true,
            "channel": "C01UJ9G",
            "ts": "1634160000.000100"
        }
        """;

    private const string ErrorPayload = """
        {
            "ok": false,
            "error": "Something went wrong"
        }
        """;

    private static readonly IRequestBuilder _slackChatPostMessageRequest = Request
        .Create()
        .WithPath("/api/chat.postMessage")
        .WithHeader("Authorization", "Bearer Secret!")
        .UsingPost();

    // IXunitSerializable makes each test case appear in test explorer
    // Source: https://github.com/xunit/xunit/issues/429#issuecomment-108187109
    // We put this delegate dictionary here to avoid serialization, and only put
    // serializable stuff in the memberdata/test cases below
    private static readonly Dictionary<string, Action<WireMockServer>> _mockServerConfigurations = new()
    {
        ["200-ok"] = server =>
            server
                .Given(_slackChatPostMessageRequest)
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(OkPayload)),
        ["200-error"] = server =>
            server
                .Given(_slackChatPostMessageRequest)
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(ErrorPayload)),
        ["500-error"] = server =>
            server.Given(_slackChatPostMessageRequest).RespondWith(Response.Create().WithStatusCode(500)),
        ["429-ratelimited"] = server =>
            server.Given(_slackChatPostMessageRequest).RespondWith(Response.Create().WithStatusCode(429)),
        ["500-error-then-ok"] = server =>
        {
            server
                .Given(_slackChatPostMessageRequest)
                .InScenario("recovery-from-500")
                .WillSetStateTo("error")
                .RespondWith(Response.Create().WithStatusCode(500));
            server
                .Given(_slackChatPostMessageRequest)
                .InScenario("recovery-from-500")
                .WhenStateIs("error")
                .WillSetStateTo("ok")
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(OkPayload));
        },
        ["429-ratelimited-then-ok"] = server =>
        {
            server
                .Given(_slackChatPostMessageRequest)
                .InScenario("recovery-from-429")
                .WillSetStateTo("error")
                .RespondWith(Response.Create().WithStatusCode(429));
            server
                .Given(_slackChatPostMessageRequest)
                .InScenario("recovery-from-429")
                .WhenStateIs("error")
                .WillSetStateTo("ok")
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(OkPayload));
        },
        ["500-error-4-times-then-ok"] = server =>
        {
            server
                .Given(_slackChatPostMessageRequest)
                .InScenario("recovery-from-500-4-times")
                .WillSetStateTo("error", 4)
                .RespondWith(Response.Create().WithStatusCode(500));
            server
                .Given(_slackChatPostMessageRequest)
                .InScenario("recovery-from-500-4-times")
                .WhenStateIs("error")
                .WillSetStateTo("ok")
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(OkPayload));
        },
    };

    public static TheoryData<string, int> SlackApiParameters =>
        new TheoryData<string, int>()
        {
            { "200-ok", 0 },
            { "200-error", 0 },
            { "500-error", 0 },
            { "429-ratelimited", 0 },
            // We only fail once here, so HttpClient retries (up to 3) will make it recover
            { "500-error-then-ok", 0 },
            { "429-ratelimited-then-ok", 0 },
            // HttpClient should retry 3 times internally,
            // but then during the second poll iteration we should succeed.
            // So we expecte 2 alerter events (where the latter succeeds)
            { "500-error-4-times-then-ok", 1 },
        };

    [Theory]
    [MemberData(nameof(SlackApiParameters))]
    public async Task Alerts_Eventually_Successfully_For_Non_Seeded_Telemetry(string @case, int waitForRetryEvents)
    {
        ServiceOwner[] serviceOwners = [ServiceOwner.Parse("skd")];
        await using var fixture = await OrchestratorFixture.Create(
            (services, fixture) =>
            {
                _mockServerConfigurations[@case](fixture.MockServer);

                var timeProvider = new FakeTimeProvider(Instant.FromUtc(2025, 2, 20, 12, 0, 0).ToDateTimeOffset());
                services.AddSingleton<TimeProvider>(timeProvider);
                services.Configure<AppConfiguration>(config =>
                {
                    config.DisableAlerter = false;
                    config.DisableSlackAlerts = false;
                    config.SlackAccessToken = "Secret!";
                    config.SlackChannel = "C01UJ9G";
                    config.SlackHost =
                        fixture.MockServer.Url ?? throw new InvalidOperationException("Mock server URL is null");

                    config.DisableSeeder = false;
                    config.SeedSqliteDbPath = Path.Combine("data", "mini.db");
                });

                services.Configure<FakeConfig>(config =>
                {
                    config.ServiceOwnersDiscovery = _ => serviceOwners;
                    config.TelemetryGenerator = sp =>
                    {
                        var timeProvider = sp.GetRequiredService<TimeProvider>();
                        var options = sp.GetRequiredService<IOptions<AppConfiguration>>().Value;
                        long id = 1;

                        return
                        [
                            TestData.GenerateMiniDbTrace(
                                serviceOwners[0],
                                ref id,
                                InstantPattern.ExtendedIso.Parse("2025-02-15T14:51:04.906736Z").Value,
                                timeProvider
                            ), // Should match a record from the seed DB, expect no alert from this
                            TestData.GenerateMiniDbTrace(
                                serviceOwners[0],
                                ref id,
                                InstantPattern.ExtendedIso.Parse("2025-02-15T14:56:04.906736Z").Value,
                                timeProvider
                            ), // Different span ID, should not dedupe, so we expect alert for this one
                        ];
                    };
                });
            }
        );
        var (hostFixture, startSignal, adapterSemaphore, pollInterval, latency, queries, cancellationToken) = fixture;

        var timeProvider = hostFixture.TimeProvider;
        var repository = hostFixture.Repository;
        var orchestratorResults = hostFixture.Orchestrator.Results;
        var alerterResults = hostFixture.Alerter.Results;

        List<ServiceOwnerQueryResult> queryResults = new();
        List<AlerterEvent> alerterEvents = new();
        {
            // Let orchestrator start work
            _ = fixture.Start();

            await fixture.WaitForQueryResults(queryResults, cancellationToken);
        }
        {
            // We can just fetch all telemetry, since we haven't alerted anything yet,
            // only the seeded ones need to be filtered out
            var telemetry = await repository.ListTelemetry(cancellationToken: cancellationToken);

            for (int i = 0; i < telemetry.Count; i++)
            {
                var item = telemetry[i];
                if (item.Seeded)
                    continue; // Seeded telemetry has already been alerted in old tool

                // Since we don't have mitigation yet, we only expect 1 event
                // per telemetry item
                for (int j = 0; j < waitForRetryEvents + 1; j++)
                {
                    AlerterEvent? result;
                    while (!alerterResults.TryRead(out result))
                    {
                        await Task.Delay(10, cancellationToken);
                        timeProvider.Advance(TimeSpan.FromSeconds(2)); // Initial HttpClient retry delay is 2s
                    }
                    alerterEvents.Add(result);
                }
            }
        }

        await Verify(NewSnapshot(queryResults, alerterEvents, repository, cancellationToken))
            .ScrubMember<TelemetryEntity>(e => e.Data)
            .DontScrubDateTimes()
            .DontIgnoreEmptyCollections()
            .UseParameters(@case, waitForRetryEvents);
    }

    private sealed record State(IReadOnlyList<TelemetryEntity> Telemetry, IReadOnlyList<AlertEntity> Alerts);

    private static async ValueTask<object> NewSnapshot(
        IReadOnlyList<ServiceOwnerQueryResult> queryResults,
        IReadOnlyList<AlerterEvent> alerterEvents,
        Repository repository,
        CancellationToken cancellationToken
    )
    {
        var (telemetry, alerts) = await GetState(repository, cancellationToken);
        var indexRecommendations = await repository.ListIndexRecommendations(cancellationToken: cancellationToken);
        return new
        {
            QueryResults = queryResults,
            AlerterEvents = alerterEvents,
            State = new State(telemetry, alerts),
            IndexRecommendations = indexRecommendations,
        };
    }

    private static async Task<(IReadOnlyList<TelemetryEntity> Telemetry, IReadOnlyList<AlertEntity> Alerts)> GetState(
        Repository repository,
        CancellationToken cancellationToken
    )
    {
        var telemetry = await repository.ListTelemetry(cancellationToken: cancellationToken);
        var alerts = await repository.ListAlerts(cancellationToken: cancellationToken);
        return (telemetry, alerts);
    }

    [Fact]
    public async Task Deserialization_Of_Slack_Ok_Response_Succeeds()
    {
        var json = OkPayload;

        var response = JsonSerializer.Deserialize<SlackAlerter.SlackResponse>(json);
        await Verify(response);
    }

    [Fact]
    public async Task Deserialization_Of_Slack_Error_Response_Succeeds()
    {
        var json = ErrorPayload;

        var response = JsonSerializer.Deserialize<SlackAlerter.SlackResponse>(json);
        await Verify(response);
    }
}
