using System.Net;
using System.Text.Json;
using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Infrastructure;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Registry;
using Polly.Timeout;

namespace ColtonStack.Server.Webhooks;

/// <summary>
/// Drains the webhook queue in the background. Each delivery goes through a resilience
/// pipeline (timeout + exponential retry with jitter), so a flaky receiver gets multiple
/// chances without ever delaying the chat operation that produced the event.
/// </summary>
public sealed partial class WebhookDispatchService(
    IWebhookOutbox outbox,
    IDbConnectionFactory connectionFactory,
    IHttpClientFactory httpClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<WebhookDispatchService> logger) : BackgroundService
{
    public const string HttpClientName = "coltonstack-webhooks";
    public const string PipelineName = "webhook-delivery";

    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline =
        pipelineProvider.GetPipeline<HttpResponseMessage>(PipelineName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WebhookDispatcherStarted();

        await foreach (var job in outbox.DequeueAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await DispatchAsync(job, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                WebhookDeliveryFailed(ex, job.EventType, job.Message.Id);
            }
        }

        WebhookDispatcherStopped();
    }

    private async Task DispatchAsync(WebhookJob job, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var webhooks = (await connection.GetAllAsync<WebhookRow>().ConfigureAwait(false))
            .Where(webhook => webhook.IsActive);

        var payload = new WebhookPayload(job.EventType, job.Message, DateTimeOffset.UtcNow);
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, ColtonStackJsonContext.Default.WebhookPayload);

        foreach (var webhook in webhooks)
        {
            // The signature covers the exact bytes on the wire, so sign the serialized body itself.
            var signature = WebhookSigner.Sign(webhook.Secret, body);

            try
            {
                using var response = await _pipeline.ExecuteAsync(
                    async token =>
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                        {
                            Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } },
                        };
                        request.Headers.Add(WebhookSigner.HeaderName, signature);

                        var client = httpClientFactory.CreateClient(HttpClientName);
                        return await client.SendAsync(request, token).ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    WebhookDelivered(webhook.Url, job.EventType);
                }
                else
                {
                    WebhookRejected(response.StatusCode, webhook.Url, job.EventType);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException)
            {
                // One unreachable endpoint must not block delivery to the others.
                // Retries were already exhausted inside the pipeline — this is the final failure.
                WebhookEndpointUnreachable(ex, webhook.Url);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook dispatcher started")]
    private partial void WebhookDispatcherStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook dispatcher stopped")]
    private partial void WebhookDispatcherStopped();

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook delivered to {Url} ({EventType})")]
    private partial void WebhookDelivered(string url, string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook endpoint {Url} returned {StatusCode} for {EventType} after retries")]
    private partial void WebhookRejected(HttpStatusCode statusCode, string url, string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook endpoint {Url} is unreachable — gave up after retries")]
    private partial void WebhookEndpointUnreachable(Exception exception, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Webhook dispatch of {EventType} for message {MessageId} failed")]
    private partial void WebhookDeliveryFailed(Exception exception, string eventType, long messageId);
}
