using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Infrastructure;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ColtonStack.Server.Endpoints;

/// <summary>
/// Register/list/remove webhook endpoints that receive chat events.
/// Pure CRUD, so it's all Dapper.Contrib — not a line of SQL in the file.
/// </summary>
public static class WebhookEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/webhooks");

        api.MapGet("/", async Task<IResult> (IDbConnectionFactory connections, CancellationToken cancellationToken) =>
        {
            await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.GetAllAsync<WebhookRow>();
            IReadOnlyList<WebhookRegistrationDto> webhooks = [.. rows
                .OrderBy(row => row.Id)
                .Select(row => new WebhookRegistrationDto(row.Id, row.Url, row.IsActive, row.CreatedAtUtc))];
            return TypedResults.Ok(webhooks);
        });

        api.MapPost("/", async Task<IResult> (RegisterWebhookRequest request, IDbConnectionFactory connections, CancellationToken cancellationToken) =>
        {
            var url = request.Url.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https"))
            {
                return TypedResults.BadRequest(new { error = "A valid http(s) URL is required." });
            }

            await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
            var row = new WebhookRow
            {
                Url = url,
                Secret = request.Secret,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            await connection.InsertAsync(row);

            var webhook = new WebhookRegistrationDto(row.Id, row.Url, row.IsActive, row.CreatedAtUtc);
            return TypedResults.Created($"/api/webhooks/{row.Id}", webhook);
        });

        api.MapDelete("/{id:long}", async Task<IResult> (long id, IDbConnectionFactory connections, CancellationToken cancellationToken) =>
        {
            await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
            var row = await connection.GetAsync<WebhookRow>(id);
            if (row is null)
            {
                return TypedResults.NotFound();
            }

            await connection.DeleteAsync(row);
            return TypedResults.NoContent();
        });
    }
}
