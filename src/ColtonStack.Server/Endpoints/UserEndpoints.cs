using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Infrastructure;
using ColtonStack.Server.Services;
using Dapper.Contrib.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ColtonStack.Server.Endpoints;

/// <summary>
/// Workspace members and the current user's profile. Pure CRUD, so it's all Dapper.Contrib —
/// including the profile save, which is a real UPDATE derived from <see cref="UserRow"/>.
/// Validation uses FluentValidation rules shared with the client (see <see cref="UpdateProfileRequestValidator"/>).
/// </summary>
public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/users");

        api.MapGet("/", async Task<IResult> (IDbConnectionFactory connections, CancellationToken cancellationToken) =>
        {
            await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.GetAllAsync<UserRow>();
            IReadOnlyList<UserDto> users = [.. rows
                .OrderByDescending(row => row.IsSelf)
                .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(row => new UserDto(row.Id, row.DisplayName, row.AvatarColor, row.IsSelf))];
            return TypedResults.Ok(users);
        });

        api.MapPut("/me", async Task<IResult> (
            UpdateProfileRequest request,
            IValidator<UpdateProfileRequest> validator,
            IDbConnectionFactory connections,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return TypedResults.BadRequest(new { error = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)) });
            }

            var displayName = request.DisplayName.Trim();

            await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
            var self = (await connection.GetAllAsync<UserRow>()).First(user => user.IsSelf);
            self.DisplayName = displayName;
            self.AvatarColor = request.AvatarColor;
            await connection.UpdateAsync(self);

            var updated = new UserDto(self.Id, self.DisplayName, self.AvatarColor, IsSelf: true);
            await audit.RecordAsync(
                entityType: "user",
                entityId: updated.Id,
                action: "updated",
                actor: updated.DisplayName,
                entity: updated,
                entityJsonTypeInfo: ColtonStackJsonContext.Default.UserDto,
                cancellationToken);
            return TypedResults.Ok(updated);
        });
    }
}
