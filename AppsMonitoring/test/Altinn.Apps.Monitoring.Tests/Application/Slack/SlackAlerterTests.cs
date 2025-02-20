using System.Text.Json;
using Altinn.Apps.Monitoring.Application;
using Altinn.Apps.Monitoring.Application.Slack;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Altinn.Apps.Monitoring.Tests.Application.Slack;

public class SlackAlerterTests
{
    [Fact]
    public async Task Test_Alerts()
    {
        var fixture = await HostFixture.Create(
            (services, fixture) =>
            {
                fixture
                    .MockServer.Given(Request.Create().WithPath("/api/chat.postMessage").UsingPost())
                    .RespondWith(Response.Create().WithStatusCode(200).WithBody(@"{ ""msg"": ""Hello world!"" }"));

                services.Configure<AppConfiguration>(config =>
                {
                    config.DisableAlerter = false;
                    config.SlackHost =
                        fixture.MockServer.Url ?? throw new InvalidOperationException("Mock server URL is null");
                });
            }
        );
        var alerter = fixture.Services.GetRequiredService<SlackAlerter>();
        // TODO - do useful stuff
    }

    [Fact]
    public async Task Test_Deserialization_Of_Ok_Response()
    {
        var json = """
            {
                "ok": true,
                "channel": "C01UJ9G",
                "ts": "1634160000.000100"
            }
            """;

        var response = JsonSerializer.Deserialize<SlackAlerter.SlackResponse>(json);
        await Verify(response).AutoVerify();
    }

    [Fact]
    public async Task Test_Deserialization_Of_Error_Response()
    {
        var json = """
            {
                "ok": false,
                "error": "Something went wrong"
            }
            """;

        var response = JsonSerializer.Deserialize<SlackAlerter.SlackResponse>(json);
        await Verify(response).AutoVerify();
    }
}
