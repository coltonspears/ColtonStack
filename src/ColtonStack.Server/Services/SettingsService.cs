using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Infrastructure;
using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Services;

/// <summary>Dapper.Contrib CRUD over <see cref="SettingRow"/>; every write is audited like any other save.</summary>
public sealed class SettingsService(
    IDbConnectionFactory connectionFactory,
    IAuditService auditService) : ISettingsService
{
    public async Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.GetAllAsync<SettingRow>().ConfigureAwait(false);
        return [.. rows.OrderBy(row => row.Key, StringComparer.Ordinal).Select(ToDto)];
    }

    public async Task<SettingDto?> FindAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.GetAsync<SettingRow>(key).ConfigureAwait(false);
        return row is null ? null : ToDto(row);
    }

    public async Task<SettingDto> SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var existing = await connection.GetAsync<SettingRow>(key).ConfigureAwait(false);
        var row = existing ?? new SettingRow { Key = key };
        row.Value = value;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            await connection.InsertAsync(row).ConfigureAwait(false);
        }
        else
        {
            await connection.UpdateAsync(row).ConfigureAwait(false);
        }

        var dto = ToDto(row);
        await auditService.RecordAsync(
            entityType: "setting",
            entityId: 0,
            action: existing is null ? "created" : "updated",
            actor: "me",
            entity: dto,
            entityJsonTypeInfo: ColtonStackJsonContext.Default.SettingDto,
            cancellationToken).ConfigureAwait(false);
        return dto;
    }

    private static SettingDto ToDto(SettingRow row) => new(row.Key, row.Value, row.UpdatedAtUtc);
}
