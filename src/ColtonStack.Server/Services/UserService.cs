using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Infrastructure;
using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Services;

/// <summary>Dapper.Contrib CRUD over <see cref="UserRow"/>; the table is a handful of demo rows, so in-memory filtering is the honest choice.</summary>
public sealed class UserService(
    IDbConnectionFactory connectionFactory,
    IAuditService auditService) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.GetAllAsync<UserRow>().ConfigureAwait(false);
        return [.. rows
            .OrderByDescending(row => row.IsSelf)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)];
    }

    public async Task<UserDto> GetSelfAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(await GetSelfRowAsync(connection).ConfigureAwait(false));
    }

    public async Task<UserDto?> FindAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.GetAsync<UserRow>(userId).ConfigureAwait(false);
        return row is null ? null : ToDto(row);
    }

    public async Task<UserDto> UpdateSelfAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var self = await GetSelfRowAsync(connection).ConfigureAwait(false);
        self.DisplayName = request.DisplayName.Trim();
        self.AvatarColor = request.AvatarColor;
        await connection.UpdateAsync(self).ConfigureAwait(false);

        var updated = ToDto(self);
        await auditService.RecordAsync(
            entityType: "user",
            entityId: updated.Id,
            action: "updated",
            actor: updated.DisplayName,
            entity: updated,
            entityJsonTypeInfo: ColtonStackJsonContext.Default.UserDto,
            cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private static async Task<UserRow> GetSelfRowAsync(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var rows = await connection.GetAllAsync<UserRow>().ConfigureAwait(false);
        return rows.FirstOrDefault(row => row.IsSelf)
            ?? throw new InvalidOperationException("The workspace has no self user — was the database seeded?");
    }

    private static UserDto ToDto(UserRow row) => new(row.Id, row.DisplayName, row.AvatarColor, row.IsSelf);
}
