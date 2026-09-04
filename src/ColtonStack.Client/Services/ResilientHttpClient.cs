using System.Net.Http;
using ColtonStack.Client.Configuration;
using ColtonStack.Client.Messages;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace ColtonStack.Client.Services;

/// <summary>
/// One place that knows how the client talks to the ColtonStack server: base address from
/// options, and a resilience pipeline (retry with exponential backoff and jitter, circuit
/// breaker, per-attempt timeout) that reports through the messenger so the status bar can
/// show it. The core API client and every extension client (e.g. Pokémon) call this — nobody
/// re-implements retry logic.
/// </summary>
public static class ResilientHttpClient
{
    /// <summary>Registers <typeparamref name="TClient"/> as a typed HttpClient against the ColtonStack server with the shared pipeline.</summary>
    public static IHttpClientBuilder AddColtonStackHttpClient<TClient>(this IServiceCollection services, string pipelineName)
        where TClient : class
    {
        var http = services.AddHttpClient<TClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ColtonStackOptions>>().Value;
            client.BaseAddress = new Uri(options.ServerUrl);
            client.Timeout = Timeout.InfiniteTimeSpan; // timeouts are the pipeline's job
        });

        http.AddResilienceHandler(pipelineName, ConfigurePipeline);
        return http;
    }

    private static void ConfigurePipeline(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, ResilienceHandlerContext context)
    {
        var messenger = context.ServiceProvider.GetRequiredService<IMessenger>();

        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            OnRetry = arguments =>
            {
                messenger.Send(new HttpRetryMessage(arguments.AttemptNumber, string.Empty));
                return default;
            },
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 8,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15),
            OnOpened = _ =>
            {
                messenger.Send(new HttpRetryMessage(0, "Circuit OPEN — the server keeps failing; pausing requests for 15s"));
                return default;
            },
            OnClosed = _ =>
            {
                messenger.Send(new HttpRetryMessage(0, "Circuit closed — server recovered"));
                return default;
            },
        });

        pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(10) });
    }
}
