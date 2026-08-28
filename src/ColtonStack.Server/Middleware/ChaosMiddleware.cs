using Microsoft.AspNetCore.Http;

namespace ColtonStack.Server.Middleware;

/// <summary>
/// Demo switch: when enabled, ~40% of API requests fail with 503 so the client's resilience
/// pipeline (retry with exponential backoff + jitter, then circuit breaker) is visible live.
/// Deliberately never touches <c>/api/chaos</c> itself, <c>/health</c>, or the SignalR hub,
/// so you can always turn the chaos back off.
/// </summary>
public sealed class ChaosMiddleware(RequestDelegate next)
{
    private static int _enabled;

    public static bool Enabled
    {
        get => Volatile.Read(ref _enabled) == 1;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (Enabled && IsChaosCandidate(context.Request.Path) && Random.Shared.NextDouble() < 0.4)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "1";
            await context.Response.WriteAsJsonAsync(
                new { error = "chaos: simulated service outage" },
                context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool IsChaosCandidate(PathString path) =>
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/api/chaos", StringComparison.OrdinalIgnoreCase);
}
