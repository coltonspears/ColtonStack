using ColtonStack.Contracts;
using ColtonStack.Server.Hubs;
using ColtonStack.Server.Infrastructure;
using Dapper;
using Microsoft.AspNetCore.SignalR;

namespace ColtonStack.Server.Services;

public sealed class ChannelService(
    IDbConnectionFactory connectionFactory,
    IAuditService auditService,
    IHubContext<ChatHub, IChatHubClient> hubContext) : IChannelService
{
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
        """;

    public async Task<IReadOnlyList<ChannelSummaryDto>> GetSummariesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ChannelSummaryRow>(
            $"{SummaryQuerySql} GROUP BY c.Id, c.Name, c.Topic ORDER BY c.Name").ConfigureAwait(false);
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

        var nameTaken = await connection.ExecuteScalarAsync<long?>(
            "SELECT Id FROM Channels WHERE Name = @name COLLATE NOCASE",
            new { name }).ConfigureAwait(false);
        if (nameTaken is not null)
        {
            throw new DuplicateChannelException(name);
        }

        var channelId = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO Channels (Name, Topic) VALUES (@name, @topic);
            SELECT last_insert_rowid();
            """,
            new { name, topic = request.Topic.Trim() }).ConfigureAwait(false);

        var row = await connection.QuerySingleAsync<ChannelSummaryRow>(
            $"{SummaryQuerySql} WHERE c.Id = @channelId GROUP BY c.Id, c.Name, c.Topic",
            new { channelId }).ConfigureAwait(false);
        var channel = row.ToDto();

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
