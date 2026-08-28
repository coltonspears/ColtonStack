using ColtonStack.Contracts;
using ColtonStack.Server.Hubs;
using ColtonStack.Server.Infrastructure;
using ColtonStack.Server.Webhooks;
using Dapper;
using Microsoft.AspNetCore.SignalR;

namespace ColtonStack.Server.Services;

public sealed class MessageService(
    IDbConnectionFactory connectionFactory,
    IAuditService auditService,
    IHubContext<ChatHub, IChatHubClient> hubContext,
    IWebhookOutbox webhookOutbox) : IMessageService
{
    private const string MessageQuerySql = """
        SELECT m.Id, m.ChannelId, m.UserId,
               u.DisplayName AS AuthorName, u.AvatarColor AS AuthorColor,
               m.Text, m.CreatedAtUtc
        FROM Messages m
        JOIN Users u ON u.Id = m.UserId
        """;

    public async Task<IReadOnlyList<MessageDto>> GetRecentAsync(
        long channelId,
        long afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var channelExists = await connection.ExecuteScalarAsync<long?>(
            "SELECT Id FROM Channels WHERE Id = @channelId",
            new { channelId }).ConfigureAwait(false);
        if (channelExists is null)
        {
            throw new ChannelNotFoundException(channelId);
        }

        var messages = await connection.QueryAsync<MessageDto>(
            $"""
            {MessageQuerySql}
            WHERE m.ChannelId = @channelId AND m.Id > @afterId
            ORDER BY m.Id
            LIMIT @limit
            """,
            new { channelId, afterId, limit }).ConfigureAwait(false);
        return [.. messages];
    }

    public async Task<MessageDto> SendAsync(
        long channelId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var channelExists = await connection.ExecuteScalarAsync<long?>(
            "SELECT Id FROM Channels WHERE Id = @channelId",
            new { channelId }).ConfigureAwait(false);
        if (channelExists is null)
        {
            throw new ChannelNotFoundException(channelId);
        }

        var self = await connection.QuerySingleAsync<UserDto>(
            "SELECT Id, DisplayName, AvatarColor, IsSelf FROM Users WHERE IsSelf = 1 LIMIT 1").ConfigureAwait(false);

        var messageId = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO Messages (ChannelId, UserId, Text, CreatedAtUtc)
            VALUES (@channelId, @UserId, @Text, @CreatedAtUtc);
            SELECT last_insert_rowid();
            """,
            new
            {
                channelId,
                UserId = self.Id,
                request.Text,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            }).ConfigureAwait(false);

        var message = await connection.QuerySingleAsync<MessageDto>(
            $"{MessageQuerySql} WHERE m.Id = @messageId",
            new { messageId }).ConfigureAwait(false);

        // Composed pipeline — each step is an injected service the record knows nothing about.
        await auditService.RecordAsync(
            entityType: "message",
            entityId: message.Id,
            action: "created",
            actor: message.AuthorName,
            entity: message,
            entityJsonTypeInfo: ColtonStackJsonContext.Default.MessageDto,
            cancellationToken).ConfigureAwait(false);

        await hubContext.Clients
            .Group(ChatHub.GroupNameFor(channelId))
            .MessagePostedAsync(message)
            .ConfigureAwait(false);

        await webhookOutbox.EnqueueAsync(
            new WebhookJob("message.posted", message),
            cancellationToken).ConfigureAwait(false);

        return message;
    }
}
