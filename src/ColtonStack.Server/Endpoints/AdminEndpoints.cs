using ColtonStack.Contracts;
using ColtonStack.Server.Middleware;
using ColtonStack.Server.Services;
using ColtonStack.Server.Simulation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ColtonStack.Server.Endpoints;

/// <summary>Demo controls: the audit trail reader, the chaos switch and the chat simulator.</summary>
public static class AdminEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/audit", async Task<IResult> (int limit, IAuditService audit, CancellationToken cancellationToken) =>
        {
            var entries = await audit.GetRecentAsync(limit <= 0 ? 50 : Math.Min(limit, 500), cancellationToken);
            return TypedResults.Ok(entries);
        });

        api.MapPost("/chaos/{enabled:bool}", (bool enabled) =>
        {
            ChaosMiddleware.Enabled = enabled;
            return TypedResults.Ok(new { chaosEnabled = enabled });
        });

        api.MapGet("/simulation", (SimulationState state) =>
            TypedResults.Ok(new SimulationStateDto(state.Enabled)));

        api.MapPost("/simulation/{enabled:bool}", (bool enabled, SimulationState state) =>
        {
            state.Enabled = enabled;
            return TypedResults.Ok(new SimulationStateDto(state.Enabled));
        });
    }
}
