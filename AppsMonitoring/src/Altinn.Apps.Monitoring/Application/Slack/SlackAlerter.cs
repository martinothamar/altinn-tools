using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Apps.Monitoring.Application.Db;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Altinn.Apps.Monitoring.Application.Slack;

internal sealed class SlackAlerter(
    ILogger<SlackAlerter> logger,
    IOptionsMonitor<AppConfiguration> config,
    IHostApplicationLifetime lifetime,
    TimeProvider timeProvider,
    DistributedLocking locking,
    Repository repository
) : IAlerter
{
    private readonly HttpClient _httpClient = new HttpClient(
        new ResilienceHandler(
            new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(
                    new HttpRetryStrategyOptions { BackoffType = DelayBackoffType.Exponential, MaxRetryAttempts = 3 }
                )
                .Build()
        )
        {
            InnerHandler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) },
        }
    );

    private readonly ILogger<SlackAlerter> _logger = logger;
    private readonly IOptionsMonitor<AppConfiguration> _config = config;
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly DistributedLocking _locking = locking;
    private readonly Repository _repository = repository;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _thread;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = _config.CurrentValue;
        if (config.DisableAlerter)
        {
            _logger.LogInformation("Alerter disabled");
            return Task.CompletedTask;
        }

        var slackAccessToken = config.SlackAccessToken;
        _httpClient.BaseAddress = new(config.SlackHost);
        _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", slackAccessToken);

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetime.ApplicationStopping
        );
        cancellationToken = _cancellationTokenSource.Token;
        _thread = Task.Run(() => Thread(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task Thread(CancellationToken cancellationToken)
    {
        var config = _config.CurrentValue;
        try
        {
            await using var handle = await locking.Lock(DistributedLockName.Alerter, cancellationToken);
            if (handle.HandleLostToken.CanBeCanceled)
            {
                _logger.LogInformation("Will monitor for lost alerter lock");
                handle.HandleLostToken.Register(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return; // We are already shutting down
                    _logger.LogError("Alerter lock lost, stopping application");
                    _lifetime.StopApplication();
                });
            }

            using var timer = new PeriodicTimer(config.PollInterval, _timeProvider);

            const Subscriber subscriber = Subscriber.Alerter;
            do
            {
                // TODO: we need a better method to reflect the "inbox" for this service
                // We want
                // * Telemetry items that don't have alerts
                // * Telemetry items that have alerts that are < Mitigated
                // * Alerts that are not mitigated
                var telemetry = await _repository.ListTelemetryFromSubscription(subscriber, cancellationToken);
                if (telemetry.Count == 0)
                    continue;

                var alerts = await _repository.ListAlerts(telemetry.Select(t => t.Id).ToArray(), cancellationToken);
                var alertsByTelemetryId = alerts.ToDictionary(a => a.TelemetryId);

                foreach (var item in telemetry)
                {
                    if (!alertsByTelemetryId.TryGetValue(item.Id, out var alert))
                    {
                        alert = new AlertEntity
                        {
                            Id = 0,
                            State = AlertState.Pending,
                            TelemetryId = item.Id,
                            ExtId = null,
                        };
                    }

                    while (alert.State < AlertState.Mitigated)
                    {
                        var updatedAlert = await Alert(item, alert, cancellationToken);
                        if (updatedAlert is null)
                            break; // Try again later
                        await _repository.SaveAlert(updatedAlert, cancellationToken);
                    }
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Alerter thread cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alerter thread failed");
            _lifetime.StopApplication();
        }
    }

    private async Task<AlertEntity?> Alert(TelemetryEntity item, AlertEntity alert, CancellationToken cancellationToken)
    {
        switch (alert.State)
        {
            case AlertState.Pending:
            {
                switch (item.Data)
                {
                    case TraceData data:
                    {
                        var text = $"""
                            *ALERT* `{item.TimeGenerated}`:
                            - App: *{item.ServiceOwner}*/*{item.AppName}*/*{item.AppVersion}*
                            - Feil: *{data.SpanName}* (status *{data.Result}*, *{data.Duration.TotalMilliseconds:0.00}ms*)
                            - Instansen: *{data.InstanceOwnerPartyId}*/*{data.InstanceId}*
                            - Operation ID: *{data.TraceId}*
                            """;
                        using var response = await _httpClient.PostAsJsonAsync(
                            "/api/chat.postMessage",
                            new
                            {
                                channel = _config.CurrentValue.SlackChannel,
                                text = text,
                                mrkdwn = true,
                                // thread_ts
                            },
                            cancellationToken
                        );

                        var slackResponse = await response.Content.ReadFromJsonAsync<SlackResponse>(cancellationToken);

                        if (slackResponse is SlackErrorResponse error)
                        {
                            _logger.LogError("Failed to send alert to Slack: {Error}", error.Error);
                            return null;
                        }
                        else if (slackResponse is SlackOkResponse ok)
                        {
                            _logger.LogInformation(
                                "Alert sent to Slack: '{Ts}' for telemetry ID '{TelemetryId}'",
                                ok.Ts,
                                item.Id
                            );
                            return alert with { State = AlertState.Alerted, ExtId = ok.Ts };
                        }
                        else
                            throw new InvalidOperationException($"Unknown Slack response: {slackResponse}");
                    }
                    default:
                        throw new InvalidOperationException($"Unknown telemetry data: {item.Data}");
                }
            }
            case AlertState.Alerted:
            {
                if (alert.ExtId is null)
                    throw new InvalidOperationException("Missing Slack thread timestamp");
                // TODO: mitigate
                return null;
            }
            case AlertState.Mitigated:
                return null; // Nothing left to do
            default:
                throw new InvalidOperationException($"Unknown alert state: {alert.State}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _thread ?? Task.CompletedTask;
    }

    [JsonConverter(typeof(SlackResponseJsonConverter))]
    internal abstract record SlackResponse
    {
        [JsonPropertyName("ok")]
        public required bool Ok { get; init; }
    }

    internal sealed record SlackOkResponse : SlackResponse
    {
        [JsonPropertyName("ts")]
        public required string? Ts { get; init; }

        [JsonPropertyName("channel")]
        public required string? Channel { get; init; }
    }

    internal sealed record SlackErrorResponse : SlackResponse
    {
        [JsonPropertyName("error")]
        public required string? Error { get; init; }
    }

    internal sealed class SlackResponseJsonConverter : JsonConverter<SlackResponse>
    {
        public override SlackResponse? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            var originalReader = reader;

            bool? ok = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString() ?? throw new JsonException();
                    if (propertyName == "ok")
                    {
                        reader.Read();
                        ok = reader.GetBoolean();
                        break;
                    }
                }
            }

            if (ok == null)
                throw new JsonException();

            SlackResponse? result = ok.Value
                ? JsonSerializer.Deserialize<SlackOkResponse>(ref originalReader, options)
                : JsonSerializer.Deserialize<SlackErrorResponse>(ref originalReader, options);

            reader = originalReader;

            return result;
        }

        public override void Write(Utf8JsonWriter writer, SlackResponse value, JsonSerializerOptions options)
        {
            if (value is SlackOkResponse ok)
                JsonSerializer.Serialize(writer, ok, options);
            else if (value is SlackErrorResponse error)
                JsonSerializer.Serialize(writer, error, options);
            else
                throw new JsonException();
        }
    }
}
