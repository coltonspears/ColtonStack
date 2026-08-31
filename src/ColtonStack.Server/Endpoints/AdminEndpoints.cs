using ColtonStack.Contracts;
using ColtonStack.Server.Middleware;
using ColtonStack.Server.Services;
using ColtonStack.Server.Simulation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ColtonStack.Server.Endpoints;

/// <summary>Demo controls: the chaos switch and the chat simulator. (The audit reader moved into the audit server extension.)</summary>
public static class AdminEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

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
