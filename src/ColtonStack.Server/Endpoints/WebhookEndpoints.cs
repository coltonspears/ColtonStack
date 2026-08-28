using ColtonStack.Contracts;
using ColtonStack.Server.Infrastructure;
using ColtonStack.Server.Webhooks;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ColtonStack.Server.Endpoints;

/// <summary>Register/list/remove webhook endpoints that receive chat events.</summary>
public static class WebhookEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/webhooks");

        api.MapGet("/", async Task<IResult> (IDbConnectionFactory connections, CancellationToken cancellationToken) =>
        {
            await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
            var webhooks = await connection.QueryAsync<WebhookRegistrationDto>(
                "SELECT Id, Url, IsActive, CreatedAtUtc FROM Webhooks ORDER BY Id");
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
            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO Webhooks (Url, Secret, IsActive, CreatedAtUtc)
                VALUES (@url, @secret, 1, @createdAtUtc);
                SELECT last_insert_rowid();
                """,
                new { url, secret = request.Secret, createdAtUtc = DateTimeOffset.UtcNow });

            var webhook = await connection.QuerySingleAsync<WebhookRegistrationDto>(
                "SELECT Id, Url, IsActive, CreatedAtUtc FROM Webhooks WHERE Id = @id",
                new { id });
            return TypedResults.Created($"/api/webhooks/{id}", webhook);
        });

        api.MapDelete("/{id:long}", async Task<IResult> (long id, IDbConnectionFactory connections, CancellationToken cancellationToken) =>
        {
            await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
            var deleted = await connection.ExecuteAsync("DELETE FROM Webhooks WHERE Id = @id", new { id });
            return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
        });
    }
}
