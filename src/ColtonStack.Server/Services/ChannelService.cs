using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Hubs;
using ColtonStack.Server.Infrastructure;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.SignalR;

namespace ColtonStack.Server.Services;

public sealed class ChannelService(
    IDbConnectionFactory connectionFactory,
    IAuditService auditService,
    IHubContext<ChatHub, IChatHubClient> hubContext) : IChannelService
{
    // The one query here that earns hand-written SQL: an aggregate over two tables.
    // COALESCE keeps column types stable for channels with no messages yet (0 / '' = none),
    // because a bare NULL expression arrives from SQLite typed as BLOB and breaks row materialization.
    private const string SummaryQuerySql = """
        SELECT c.Id,
               c.Name,
               c.Topic,
               COUNT(m.Id)                            AS MessageCount,
               COALESCE(MAX(m.Id), 0)                 AS LastMessageId,
               COALESCE(MAX(m.CreatedAtUtc), '')      AS LastMessageAtUtc,
               (SELECT recent.Text
                FROM Messages recent
                WHERE recent.ChannelId = c.Id
                ORDER BY recent.Id DESC
                LIMIT 1)                              AS LastMessagePreview
        FROM Channels c
        LEFT JOIN Messages m ON m.ChannelId = c.Id
        GROUP BY c.Id, c.Name, c.Topic
        ORDER BY c.Name
        """;

    public async Task<IReadOnlyList<ChannelSummaryDto>> GetSummariesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ChannelSummaryRow>(SummaryQuerySql).ConfigureAwait(false);
        return [.. rows.Select(row => row.ToDto())];
    }

    public async Task<ChannelSummaryDto> CreateAsync(CreateChannelRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException("Channel name must not be empty.", nameof(request));
        }

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // A workspace has a handful of channels, so the duplicate check happens in memory;
        // the UNIQUE constraint on Name remains the real guard underneath.
        var existing = await connection.GetAllAsync<ChannelRow>().ConfigureAwait(false);
        if (existing.Any(channel => string.Equals(channel.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicateChannelException(name);
        }

        // InsertAsync writes the generated key back into row.Id, and a brand-new channel has no
        // messages by definition — so the DTO is built right here, no re-query.
        var row = new ChannelRow { Name = name, Topic = request.Topic.Trim() };
        await connection.InsertAsync(row).ConfigureAwait(false);

        var channel = new ChannelSummaryDto(
            row.Id, row.Name, row.Topic,
            MessageCount: 0,
            LastMessageId: null,
            LastMessageAtUtc: null,
            LastMessagePreview: null);

        await auditService.RecordAsync(
            entityType: "channel",
            entityId: channel.Id,
            action: "created",
            actor: "system",
            entity: channel,
            entityJsonTypeInfo: ColtonStackJsonContext.Default.ChannelSummaryDto,
            cancellationToken).ConfigureAwait(false);

        await hubContext.Clients.All.ChannelCreatedAsync(channel).ConfigureAwait(false);

        return channel;
    }
}
