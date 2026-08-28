using System.Net;
using ColtonStack.Contracts;
using ColtonStack.Server.Endpoints;
using ColtonStack.Server.Hubs;
using ColtonStack.Server.Infrastructure;
using ColtonStack.Server.Middleware;
using ColtonStack.Server.Services;
using ColtonStack.Server.Webhooks;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

// JSON over HTTP uses the shared source-generated context — the same one the client uses.
// No reflection-based serializer anywhere in the system.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ColtonStackJsonContext.Default));

// Composition root: dumb records flow through smart services, wired here.
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqliteConnectionFactory(builder.Configuration["ColtonStack:DatabasePath"] ?? "coltonstack.db"));
builder.Services.AddSingleton<IAuditService, AuditService>();
builder.Services.AddSingleton<IMessageService, MessageService>();
builder.Services.AddSingleton<IChannelService, ChannelService>();
builder.Services.AddSingleton<IWebhookOutbox, WebhookOutbox>();

// Resilience for outbound webhook delivery: per-attempt timeout, exponential retry with
// jitter on transient failures (network errors, timeouts, 5xx), under an overall deadline.
// The same building blocks the WPF client uses for its HTTP calls.
builder.Services.AddResiliencePipeline(WebhookDispatchService.PipelineName, (ResiliencePipelineBuilder<HttpResponseMessage> pipeline) =>
{
    pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(45) });

    pipeline.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(1),
        ShouldHandle = static arguments => ValueTask.FromResult(
            arguments.Outcome.Exception is HttpRequestException or TimeoutRejectedException
            || arguments.Outcome.Result is HttpResponseMessage { StatusCode: HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout }),
    });

    pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(10) });
});

builder.Services.AddHttpClient(WebhookDispatchService.HttpClientName);

// The Generic Host manages startup ordering: schema + seed first, dispatcher draining forever.
builder.Services.AddHostedService<SqliteDatabaseInitializer>();
builder.Services.AddHostedService<WebhookDispatchService>();

builder.Services.AddSignalR();

var app = builder.Build();

app.UseMiddleware<ChaosMiddleware>();

app.MapGet("/health", () => TypedResults.Ok(new { status = "healthy" }));

ChatEndpoints.Map(app);
WebhookEndpoints.Map(app);
AdminEndpoints.Map(app);

app.MapHub<ChatHub>("/hubs/chat");

app.Run();
