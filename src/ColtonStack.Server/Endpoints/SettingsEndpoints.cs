using ColtonStack.Contracts;
using ColtonStack.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ColtonStack.Server.Endpoints;

/// <summary>
/// <c>GET /api/settings</c> and <c>PUT /api/settings/{key}</c>. The key shape is enforced with
/// the same <see cref="SettingKey"/> rule the client applies before sending.
/// </summary>
public static class SettingsEndpoints
{
    public const int MaxValueLength = 4_000;

    public static void Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/settings");

        api.MapGet("/", async Task<IResult> (ISettingsService settings, CancellationToken cancellationToken) =>
            TypedResults.Ok(await settings.GetAllAsync(cancellationToken)));

        api.MapPut("/{key}", async Task<IResult> (string key, SetSettingRequest request, ISettingsService settings, CancellationToken cancellationToken) =>
        {
            if (!SettingKey.IsValid(key))
            {
                return TypedResults.BadRequest(new { error = $"'{key}' is not a valid settings key (lower-case dotted segments, max {SettingKey.MaxLength} chars)." });
            }

            if (request.Value.Length > MaxValueLength)
            {
                return TypedResults.BadRequest(new { error = $"Setting values are limited to {MaxValueLength} characters." });
            }

            return TypedResults.Ok(await settings.SetAsync(key, request.Value, cancellationToken));
        });
    }
}
