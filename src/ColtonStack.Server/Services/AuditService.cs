using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Infrastructure;
using Dapper;
using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Services;

/// <summary>Implementation of <see cref="IAuditService"/>: writes the <c>AuditLog</c> table.</summary>
public sealed partial class AuditService(
    IDbConnectionFactory connectionFactory,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task RecordAsync<TEntity>(
        string entityType,
        long entityId,
        string action,
        string actor,
        TEntity entity,
        JsonTypeInfo<TEntity> entityJsonTypeInfo,
        CancellationToken cancellationToken)
    {
        var row = new AuditRow
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = actor,
            TimestampUtc = DateTimeOffset.UtcNow,
            PayloadJson = JsonSerializer.Serialize(entity, entityJsonTypeInfo),
        };

        try
        {
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await connection.InsertAsync(row).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Auditing must never take the main operation down — log loudly and move on.
            AuditWriteFailed(ex, entityType, entityId);
        }
    }

    public async Task<IReadOnlyList<AuditEntryDto>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // The audit log grows without bound, so this page stays a real query — the newest N
        // rows come back without materializing the table.
        var entries = await connection.QueryAsync<AuditEntryDto>(
            """
            SELECT Id, EntityType, EntityId, Action, Actor, TimestampUtc, PayloadJson
            FROM AuditLog
            ORDER BY Id DESC
            LIMIT @limit
            """,
            new { limit }).ConfigureAwait(false);
        return [.. entries];
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to write audit entry for {EntityType}/{EntityId}")]
    private partial void AuditWriteFailed(Exception exception, string entityType, long entityId);
}
